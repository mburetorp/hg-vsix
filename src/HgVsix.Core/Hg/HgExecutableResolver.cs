using System;
using System.IO;

namespace HgVsix.Core.Hg
{
    public static class HgExecutableResolver
    {
        public const string TortoiseHgDefaultPath = @"C:\Program Files\TortoiseHg\hg.exe";

        // Order: configured path, hg on PATH, TortoiseHg default, plain "hg"
        public static string Resolve(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var trimmed = configuredPath!.Trim().Trim('"');
                if (File.Exists(trimmed))
                {
                    return trimmed;
                }
            }

            var fromPath = FindOnPath("hg.exe") ?? FindOnPath("hg.cmd") ?? FindOnPath("hg.bat");
            if (fromPath != null)
            {
                return fromPath;
            }

            if (File.Exists(TortoiseHgDefaultPath))
            {
                return TortoiseHgDefaultPath;
            }

            return "hg";
        }

        private static string? FindOnPath(string fileName)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (var dir in path!.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                try
                {
                    var candidate = Path.Combine(dir.Trim().Trim('"'), fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry
                }
            }

            return null;
        }
    }
}
