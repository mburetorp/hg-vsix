using System;
using System.IO;

namespace HgVsix.Core.Hg
{
    // hg rewrites .hg/dirstate on commit, update, add, remove and revert.
    // Its write time and size identify the working directory state without starting hg.
    public static class DirstateStamp
    {
        public static string Read(string root)
        {
            try
            {
                var info = new FileInfo(Path.Combine(root, ".hg", "dirstate"));
                return info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : string.Empty;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }
    }
}
