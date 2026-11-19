namespace HgVsix.Core.Hg
{
    public enum HgFileState
    {
        Unknown,
        Clean,
        Modified,
        Added,
        Removed,
        Missing,
        Untracked,
        Ignored,
    }

    // CopySource is set for added files that hg records as copied or renamed
    public sealed record FileStatus(HgFileState State, string? CopySource = null)
    {
        public static readonly FileStatus Unknown = new FileStatus(HgFileState.Unknown);

        public static FileStatus Parse(string output)
        {
            var lines = output.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0].Length < 2 || lines[0][1] != ' ')
            {
                return Unknown;
            }

            var state = lines[0][0] switch
            {
                'C' => HgFileState.Clean,
                'M' => HgFileState.Modified,
                'A' => HgFileState.Added,
                'R' => HgFileState.Removed,
                '!' => HgFileState.Missing,
                '?' => HgFileState.Untracked,
                'I' => HgFileState.Ignored,
                _ => HgFileState.Unknown,
            };

            string? copySource = null;
            if (state == HgFileState.Added && lines.Length > 1 && lines[1].StartsWith("  ", System.StringComparison.Ordinal))
            {
                // Only the two space prefix is removed. File names may start or end with spaces.
                var source = lines[1].Substring(2);
                if (source.Length > 0)
                {
                    copySource = source.Replace('\\', '/');
                }
            }

            return new FileStatus(state, copySource);
        }
    }
}
