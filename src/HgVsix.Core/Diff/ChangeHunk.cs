namespace HgVsix.Core.Diff
{
    public enum ChangeKind
    {
        Added,
        Modified,
        Deleted,
    }

    // Line numbers are zero based. A deletion has NewLineCount 0 and NewStartLine at the boundary.
    public sealed record ChangeHunk(
        ChangeKind Kind,
        int OldStartLine,
        int OldLineCount,
        int NewStartLine,
        int NewLineCount);

    // One raw difference from a line diff in old and new line units
    public readonly struct LineDifference
    {
        public LineDifference(int oldStart, int oldLength, int newStart, int newLength)
        {
            OldStart = oldStart;
            OldLength = oldLength;
            NewStart = newStart;
            NewLength = newLength;
        }

        public int OldStart { get; }

        public int OldLength { get; }

        public int NewStart { get; }

        public int NewLength { get; }
    }
}
