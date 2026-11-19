using System;
using System.Collections.Concurrent;
using System.IO;

namespace HgVsix.Core.Hg
{
    // RelativePath uses forward slashes as hg expects in path: patterns
    public sealed record RepositoryLocation(string Root, string RelativePath);

    // Finds the nearest enclosing .hg directory without starting hg
    public sealed class RepositoryLocator
    {
        private readonly ConcurrentDictionary<string, string> _rootByDirectory =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public RepositoryLocation? Locate(string filePath) => Resolve(filePath, false, out _);

        // Never touches the file system. Returns false when the directory has not been looked up yet.
        public bool TryLocateCached(string filePath, out RepositoryLocation? location)
        {
            location = Resolve(filePath, true, out var known);
            return known;
        }

        public void ClearCache() => _rootByDirectory.Clear();

        private RepositoryLocation? Resolve(string filePath, bool cachedOnly, out bool known)
        {
            known = true;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (directory == null)
            {
                return null;
            }

            string? root;
            if (cachedOnly)
            {
                known = _rootByDirectory.TryGetValue(directory, out root);
            }
            else
            {
                root = _rootByDirectory.GetOrAdd(directory, FindRoot);
            }

            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var relative = fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative.Replace('\\', '/');
            if (relative.Length == 0 || relative.Equals(".hg", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith(".hg/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new RepositoryLocation(root, relative);
        }

        // Empty string means no repository
        private static string FindRoot(string directory)
        {
            try
            {
                var current = new DirectoryInfo(directory);
                while (current != null)
                {
                    if (Directory.Exists(Path.Combine(current.FullName, ".hg")))
                    {
                        var full = current.FullName;
                        return current.Parent == null
                            ? full
                            : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    }

                    current = current.Parent;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                // Treat unreadable locations as outside a repository
            }

            return string.Empty;
        }
    }
}
