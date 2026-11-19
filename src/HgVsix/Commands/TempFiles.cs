using System;
using System.Collections.Generic;
using System.IO;

namespace HgVsix.Commands
{
    // Baseline files for the diff window under %TEMP%\HgVsix
    internal static class TempFiles
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "HgVsix");
        private static readonly List<string> SessionDirectories = new List<string>();

        // Keeps the original file name so the diff window picks the right language
        public static string Write(string fileName, string shortNode, byte[] content)
        {
            var directory = Path.Combine(Root, shortNode + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, content);
            File.SetAttributes(path, FileAttributes.ReadOnly);
            lock (SessionDirectories)
            {
                SessionDirectories.Add(directory);
            }

            return path;
        }

        public static void DeleteSessionFiles()
        {
            lock (SessionDirectories)
            {
                foreach (var directory in SessionDirectories)
                {
                    TryDelete(directory);
                }

                SessionDirectories.Clear();
            }
        }

        // Removes leftovers from earlier sessions
        public static void DeleteStale(TimeSpan age)
        {
            try
            {
                if (!Directory.Exists(Root))
                {
                    return;
                }

                foreach (var directory in Directory.GetDirectories(Root))
                {
                    if (DateTime.UtcNow - Directory.GetCreationTimeUtc(directory) > age)
                    {
                        TryDelete(directory);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        private static void TryDelete(string directory)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(directory, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Still open in a diff window
            }
        }
    }
}
