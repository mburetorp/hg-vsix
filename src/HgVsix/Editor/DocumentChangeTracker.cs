using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HgVsix.Core.Diff;
using HgVsix.Core.Hg;
using HgVsix.Core.Text;
using HgVsix.Diff;
using HgVsix.Options;
using HgVsix.Refresh;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace HgVsix.Editor
{
    // One per document buffer. Owns the baseline and the latest change map.
    // State is only written on the UI thread. Diffs run on the thread pool.
    internal sealed class DocumentChangeTracker
    {
        private static readonly TimeSpan EditDelay = TimeSpan.FromMilliseconds(300);

        private readonly ITextBuffer _buffer;
        private readonly ITextDocument _document;
        private readonly HgServices _services;
        private readonly VsDiffCalculator _calculator;
        private readonly RefreshCoordinator _coordinator;

        private int _references;
        private bool _disposed;
        private Baseline? _baseline;
        private string? _normalizedBaseline;
        private CancellationTokenSource? _baselineCancellation;
        private CancellationTokenSource? _diffCancellation;
        private string? _lastError;
        private string? _watchedRoot;

        // Survives refreshes that replace the one that requested it
        private bool _recomputePending;

        private DocumentChangeTracker(ITextBuffer buffer, ITextDocument document, HgServices services, VsDiffCalculator calculator, RefreshCoordinator coordinator)
        {
            _buffer = buffer;
            _document = document;
            _services = services;
            _calculator = calculator;
            _coordinator = coordinator;
        }

        public event EventHandler? ChangesUpdated;

        public ChangeMap Map { get; private set; } = ChangeMap.Empty;

        // Snapshot the map line numbers refer to
        public ITextSnapshot? MapSnapshot { get; private set; }

        public ITextBuffer Buffer => _buffer;

        public string FilePath => _document.FilePath;

        // Set before hg is queried so no repository change is missed
        public string? RepositoryRoot => _watchedRoot;

        public string? BaselineNode => _baseline?.Node;

        // Outside a repository or failed last time
        public bool NeedsRecheck => _baseline?.Location == null || _baseline.State == BaselineState.Error;

        public static DocumentChangeTracker Acquire(ITextBuffer buffer, ITextDocument document, HgServices services, VsDiffCalculator calculator, RefreshCoordinator coordinator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var tracker = buffer.Properties.GetOrCreateSingletonProperty(
                typeof(DocumentChangeTracker),
                () => new DocumentChangeTracker(buffer, document, services, calculator, coordinator));

            tracker._references++;
            if (tracker._references == 1)
            {
                tracker.Start();
            }

            return tracker;
        }

        public void Release()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (--_references > 0)
            {
                return;
            }

            _disposed = true;
            _buffer.Properties.RemoveProperty(typeof(DocumentChangeTracker));
            _buffer.ChangedLowPriority -= OnBufferChanged;
            _document.FileActionOccurred -= OnFileActionOccurred;
            _baselineCancellation?.Cancel();
            _diffCancellation?.Cancel();
            _coordinator.Unregister(this);
        }

        // Re-reads the baseline from hg (through the provider cache) and recomputes the map if the baseline changed or recomputeMap is set
        public void RefreshBaseline(bool recomputeMap = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed)
            {
                return;
            }

            _recomputePending |= recomputeMap;
            _baselineCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _baselineCancellation = cancellation;
            var token = cancellation.Token;
            var path = _document.FilePath;
            var encoding = _document.Encoding;
            var enabled = HgVsixSettings.Instance.Enabled;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    // Locating probes the file system so it stays off the UI thread
                    await TaskScheduler.Default;
                    var root = enabled ? _services.Locator.Locate(path)?.Root : null;

                    // Watch before reading. A repository change after this point triggers another refresh.
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                    if (_disposed)
                    {
                        return;
                    }

                    var watcher = _coordinator.UpdateRepository(this, _watchedRoot, root);
                    _watchedRoot = root;

                    await TaskScheduler.Default;
                    if (watcher != null)
                    {
                        await watcher.StartAsync().ConfigureAwait(false);
                    }

                    Baseline? baseline = null;
                    string? normalized = null;
                    if (enabled)
                    {
                        baseline = await _services.Baselines.GetBaselineAsync(path, token).ConfigureAwait(false);
                        if (baseline.IsAvailable && !baseline.IsNewFile)
                        {
                            var text = BaselineDecoder.Decode(baseline.Content!, encoding);
                            normalized = VsDiffCalculator.NormalizeLines(BaselineDecoder.SplitLines(text));
                        }
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                    if (_disposed)
                    {
                        return;
                    }

                    ReportError(baseline);
                    var unchanged = _baseline != null && baseline != null && _baseline.Identity == baseline.Identity;
                    _baseline = baseline;
                    _normalizedBaseline = normalized;

                    if (!unchanged || _recomputePending || MapSnapshot == null)
                    {
                        _recomputePending = false;
                        ScheduleDiff(TimeSpan.Zero);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    OutputLog.Instance.Write($"Baseline refresh failed for {path}: {ex}");
                }
            }).FileAndForget("HgVsix/RefreshBaseline");
        }

        // Refreshes when another repository now owns the file, for example after hg init in a subfolder.
        // Expects the locator cache to be cleared first.
        public void RefreshIfOwnerChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var path = _document.FilePath;
            var enabled = HgVsixSettings.Instance.Enabled;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                var root = enabled ? _services.Locator.Locate(path)?.Root : null;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!_disposed && !string.Equals(root, _watchedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    RefreshBaseline();
                }
            }).FileAndForget("HgVsix/RefreshIfOwnerChanged");
        }

        private void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _buffer.ChangedLowPriority += OnBufferChanged;
            _document.FileActionOccurred += OnFileActionOccurred;
            _coordinator.Register(this);
            RefreshBaseline();
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e) => ScheduleDiff(EditDelay);

        private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var written = (e.FileActionType & (FileActionTypes.ContentSavedToDisk | FileActionTypes.ContentLoadedFromDisk)) != 0;

            // Saving or restoring a missing file changes its status without touching the dirstate
            if ((e.FileActionType & FileActionTypes.DocumentRenamed) != 0 || (written && _baseline?.IsAvailable != true))
            {
                RefreshBaseline();
            }
        }

        private void ScheduleDiff(TimeSpan delay)
        {
            _diffCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            _diffCancellation = cancellation;
            var token = cancellation.Token;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                    if (_disposed)
                    {
                        return;
                    }

                    var baseline = _baseline;
                    var normalizedBaseline = _normalizedBaseline;
                    var snapshot = _buffer.CurrentSnapshot;

                    await TaskScheduler.Default;
                    var map = ComputeMap(baseline, normalizedBaseline, snapshot);

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                    if (_disposed || token.IsCancellationRequested)
                    {
                        return;
                    }

                    Map = map;
                    MapSnapshot = snapshot;
                    ChangesUpdated?.Invoke(this, EventArgs.Empty);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    OutputLog.Instance.Write($"Diff failed for {_document.FilePath}: {ex}");
                }
            }).FileAndForget("HgVsix/Diff");
        }

        private ChangeMap ComputeMap(Baseline? baseline, string? normalizedBaseline, ITextSnapshot snapshot)
        {
            if (baseline == null || !baseline.IsAvailable || snapshot.Length > HgVsixSettings.Instance.MaxFileSizeBytes)
            {
                return ChangeMap.Empty;
            }

            if (baseline.IsNewFile)
            {
                var lineCount = snapshot.LineCount;
                if (lineCount > 1 && snapshot.GetLineFromLineNumber(lineCount - 1).Length == 0)
                {
                    lineCount--;
                }

                if (snapshot.Length == 0)
                {
                    lineCount = 0;
                }

                return new ChangeMap(HunkBuilder.AllAdded(lineCount), snapshot.LineCount);
            }

            var current = VsDiffCalculator.NormalizeSnapshot(snapshot);
            var hunks = _calculator.Compute(normalizedBaseline ?? string.Empty, current, snapshot);
            return new ChangeMap(hunks, snapshot.LineCount);
        }

        private void ReportError(Baseline? baseline)
        {
            var message = baseline?.State == BaselineState.Error ? baseline.Message : null;
            if (message != null && message != _lastError)
            {
                OutputLog.Instance.Write(message);
            }

            _lastError = message;
        }
    }
}
