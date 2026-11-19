using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HgVsix.Core.Hg;

namespace HgVsix.Refresh
{
    // Watches .hg for dirstate writes which hg does on commit, update, add, remove and revert.
    // Construction is cheap. StartAsync does the file system work on a background thread.
    internal sealed class RepositoryWatcher : IDisposable
    {
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

        private readonly string _root;
        private readonly Action<string> _onChanged;
        private readonly Timer _timer;
        private readonly object _lock = new object();
        private FileSystemWatcher? _watcher;
        private Task? _start;
        private string? _stamp;
        private bool _disposed;

        public RepositoryWatcher(string root, Action<string> onChanged)
        {
            _root = root;
            _onChanged = onChanged;
            _timer = new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public int References { get; set; }

        // Completes once events are raised. Safe to call from several callers.
        public Task StartAsync()
        {
            lock (_lock)
            {
                return _start ??= Task.Run(Start);
            }
        }

        // True when dirstate changed since the last check or event. False before the watcher started.
        public bool CheckChanged()
        {
            if (_stamp == null)
            {
                return false;
            }

            var stamp = DirstateStamp.Read(_root);
            var changed = stamp != _stamp;
            _stamp = stamp;
            return changed;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _watcher?.Dispose();
            }

            _timer.Dispose();
        }

        private void Start()
        {
            var hgDirectory = Path.Combine(_root, ".hg");
            FileSystemWatcher? watcher = null;
            try
            {
                watcher = new FileSystemWatcher(hgDirectory)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                watcher.Changed += OnFileEvent;
                watcher.Created += OnFileEvent;
                watcher.Renamed += OnFileEvent;
                watcher.Error += (s, e) => Schedule();
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                // Focus checks still catch changes
                OutputLog.Instance.Write($"Could not watch {hgDirectory}: {ex.Message}");
            }

            // Read after enabling events so no change falls between the two
            var stamp = DirstateStamp.Read(_root);
            lock (_lock)
            {
                if (_disposed)
                {
                    watcher?.Dispose();
                    return;
                }

                _watcher = watcher;
                _stamp = stamp;
            }
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            if (e.Name != null && e.Name.StartsWith("dirstate", StringComparison.OrdinalIgnoreCase))
            {
                Schedule();
            }
        }

        private void Schedule()
        {
            try
            {
                _timer.Change(Debounce, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Fire()
        {
            _stamp = DirstateStamp.Read(_root);
            _onChanged(_root);
        }
    }
}
