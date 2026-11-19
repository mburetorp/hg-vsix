using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HgVsix.Core.Text;

namespace HgVsix.Core.Hg
{
    // Resolves the committed version of a file.
    // The parent is cached per repository until the dirstate stamp changes or the cache is invalidated.
    // File status is always read fresh because working file changes do not touch the dirstate.
    // Content is cached by node so it never goes stale.
    public sealed class BaselineProvider
    {
        private readonly RepositoryLocator _locator;
        private readonly RevisionReader _reader;
        private readonly Func<long> _maxBytes;
        private readonly ConcurrentDictionary<string, Task<string>> _parents =
            new ConcurrentDictionary<string, Task<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly LruCache<string, byte[]> _contents = new LruCache<string, byte[]>(64, StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _stamps =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public BaselineProvider(RepositoryLocator locator, RevisionReader reader, Func<long> maxBytes)
        {
            _locator = locator;
            _reader = reader;
            _maxBytes = maxBytes;
        }

        public RepositoryLocator Locator => _locator;

        public async Task<Baseline> GetBaselineAsync(string filePath, CancellationToken cancellationToken)
        {
            var location = _locator.Locate(filePath);
            if (location == null)
            {
                return Baseline.Unavailable(BaselineState.NotInRepository, null, null, null);
            }

            ValidateStamp(location.Root);

            string node;
            FileStatus status;
            try
            {
                node = await GetSharedAsync(_parents, location.Root, () => _reader.GetParentNodeAsync(location.Root, CancellationToken.None), cancellationToken).ConfigureAwait(false);
                status = await _reader.GetFileStatusAsync(location.Root, location.RelativePath, cancellationToken).ConfigureAwait(false);
            }
            catch (HgException ex)
            {
                return Baseline.Unavailable(BaselineState.Error, location, null, null, ex.Message);
            }

            switch (status.State)
            {
                case HgFileState.Untracked:
                    return Baseline.Unavailable(BaselineState.Untracked, location, status, node);
                case HgFileState.Ignored:
                    return Baseline.Unavailable(BaselineState.Ignored, location, status, node);
                case HgFileState.Added when status.CopySource == null:
                    return Baseline.Available(location, status, node, Array.Empty<byte>());
                case HgFileState.Clean:
                case HgFileState.Modified:
                case HgFileState.Added:
                    break;
                default:
                    return Baseline.Unavailable(BaselineState.NotTracked, location, status, node);
            }

            if (node == RevisionReader.NullNode)
            {
                return Baseline.Available(location, status, node, Array.Empty<byte>());
            }

            var sourcePath = status.CopySource ?? location.RelativePath;
            var contentKey = $"{location.Root}|{node}|{sourcePath}";
            var maxBytes = _maxBytes();
            if (!_contents.TryGet(contentKey, out var content))
            {
                byte[]? read;
                try
                {
                    read = await _reader.GetFileContentAsync(location.Root, node, sourcePath, maxBytes, cancellationToken).ConfigureAwait(false);
                }
                catch (HgException ex)
                {
                    return Baseline.Unavailable(BaselineState.Error, location, status, node, ex.Message);
                }

                // Oversized content is neither buffered nor cached
                if (read == null)
                {
                    return Baseline.Unavailable(BaselineState.TooLarge, location, status, node);
                }

                content = read;
                _contents.Set(contentKey, content);
            }

            // The limit may have been lowered after caching
            if (content.Length > maxBytes)
            {
                return Baseline.Unavailable(BaselineState.TooLarge, location, status, node);
            }

            if (BaselineDecoder.IsBinary(content))
            {
                return Baseline.Unavailable(BaselineState.Binary, location, status, node);
            }

            return Baseline.Available(location, status, node, content);
        }

        // Forgets the parent of one repository
        public void InvalidateRepository(string root)
        {
            _stamps.TryRemove(root, out _);
            _parents.TryRemove(root, out _);
        }

        public void InvalidateAll()
        {
            _stamps.Clear();
            _parents.Clear();
            _locator.ClearCache();
        }

        // Drops a parent that was read under a different dirstate.
        // Keeps the cache correct even when no watcher saw the change.
        private void ValidateStamp(string root)
        {
            var stamp = DirstateStamp.Read(root);
            if (_stamps.TryGetValue(root, out var previous) && previous == stamp)
            {
                return;
            }

            InvalidateRepository(root);
            _stamps[root] = stamp;
        }

        // Concurrent callers share one hg call. Failed calls are not cached.
        private static async Task<T> GetSharedAsync<T>(ConcurrentDictionary<string, Task<T>> cache, string key, Func<Task<T>> factory, CancellationToken cancellationToken)
        {
            var task = cache.GetOrAdd(key, _ => Task.Run(factory));
            try
            {
                return await WaitAsync(task, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (task.IsFaulted || task.IsCanceled)
            {
                ((ICollection<KeyValuePair<string, Task<T>>>)cache).Remove(new KeyValuePair<string, Task<T>>(key, task));
                if (task.Exception?.InnerException is HgException hg)
                {
                    throw hg;
                }

                throw new HgException(task.Exception?.InnerException?.Message ?? "hg call was canceled", task.Exception?.InnerException ?? new OperationCanceledException());
            }
        }

        private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return await task.ConfigureAwait(false);
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }
    }
}
