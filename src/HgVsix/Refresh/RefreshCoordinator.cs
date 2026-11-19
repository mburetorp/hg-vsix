using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using HgVsix.Editor;
using HgVsix.Options;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;

namespace HgVsix.Refresh
{
    // Connects external repository changes, VS focus and settings changes to the open document trackers.
    // Public members run on the UI thread.
    [Export(typeof(RefreshCoordinator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class RefreshCoordinator
    {
        private readonly HgServices _services;
        private readonly HashSet<DocumentChangeTracker> _trackers = new HashSet<DocumentChangeTracker>();
        private readonly Dictionary<string, RepositoryWatcher> _watchers = new Dictionary<string, RepositoryWatcher>(StringComparer.OrdinalIgnoreCase);
        private bool _hooked;

        [ImportingConstructor]
        public RefreshCoordinator(HgServices services)
        {
            _services = services;
        }

        public void Register(DocumentChangeTracker tracker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_hooked)
            {
                _hooked = true;
                if (Application.Current != null)
                {
                    Application.Current.Activated += OnApplicationActivated;
                }

                HgVsixSettings.Instance.Changed += OnSettingsChanged;
            }

            _trackers.Add(tracker);
        }

        public void Unregister(DocumentChangeTracker tracker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_trackers.Remove(tracker))
            {
                UpdateRepository(tracker, tracker.RepositoryRoot, null);
            }
        }

        // Keeps one watcher per repository with open documents.
        // Returns the watcher for newRoot which the caller starts off the UI thread.
        public RepositoryWatcher? UpdateRepository(DocumentChangeTracker tracker, string? oldRoot, string? newRoot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (string.Equals(oldRoot, newRoot, StringComparison.OrdinalIgnoreCase))
            {
                return newRoot != null && _watchers.TryGetValue(newRoot, out var current) ? current : null;
            }

            if (oldRoot != null && _watchers.TryGetValue(oldRoot, out var old) && --old.References <= 0)
            {
                old.Dispose();
                _watchers.Remove(oldRoot);
            }

            if (newRoot == null || !_trackers.Contains(tracker))
            {
                return null;
            }

            if (!_watchers.TryGetValue(newRoot, out var watcher))
            {
                watcher = new RepositoryWatcher(newRoot, OnRepositoryChanged);
                _watchers.Add(newRoot, watcher);
            }

            watcher.References++;
            return watcher;
        }

        // Called on a timer thread
        private void OnRepositoryChanged(string root)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshRepository(root);
            }).FileAndForget("HgVsix/RepositoryChanged");
        }

        private void RefreshRepository(string root)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _services.Baselines.InvalidateRepository(root);
            foreach (var tracker in _trackers.Where(t => string.Equals(t.RepositoryRoot, root, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                tracker.RefreshBaseline();
            }
        }

        // Catches changes the watcher missed and repositories created or removed while VS was in the background.
        // File system checks run off the UI thread.
        private void OnApplicationActivated(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var watchers = _watchers.ToList();
            var trackers = _trackers.ToList();

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                var changedRoots = watchers.Where(pair => pair.Value.CheckChanged()).Select(pair => pair.Key).ToList();
                _services.Locator.ClearCache();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var root in changedRoots)
                {
                    RefreshRepository(root);
                }

                foreach (var tracker in trackers)
                {
                    if (tracker.NeedsRecheck)
                    {
                        tracker.RefreshBaseline();
                    }
                    else
                    {
                        tracker.RefreshIfOwnerChanged();
                    }
                }
            }).FileAndForget("HgVsix/ApplicationActivated");
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _services.Baselines.InvalidateAll();
            foreach (var tracker in _trackers.ToList())
            {
                // Size limit and enabled state affect the map even when the baseline is unchanged
                tracker.RefreshBaseline(recomputeMap: true);
            }
        }
    }
}
