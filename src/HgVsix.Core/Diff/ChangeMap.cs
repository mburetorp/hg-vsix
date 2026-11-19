using System;
using System.Collections.Generic;

namespace HgVsix.Core.Diff
{
    public enum LineChange : byte
    {
        None,
        Added,
        Modified,
    }

    // Per line lookup of hunks for fast rendering of visible lines
    public sealed class ChangeMap
    {
        public static readonly ChangeMap Empty = new ChangeMap(Array.Empty<ChangeHunk>(), 0);

        private readonly LineChange[] _lines;
        private readonly bool[] _deletedBefore;

        public ChangeMap(IReadOnlyList<ChangeHunk> hunks, int lineCount)
        {
            Hunks = hunks;
            LineCount = Math.Max(0, lineCount);
            _lines = new LineChange[LineCount];
            _deletedBefore = new bool[LineCount + 1];

            foreach (var hunk in hunks)
            {
                switch (hunk.Kind)
                {
                    case ChangeKind.Deleted:
                        _deletedBefore[Clamp(hunk.NewStartLine, 0, LineCount)] = true;
                        break;
                    default:
                        var value = hunk.Kind == ChangeKind.Added ? LineChange.Added : LineChange.Modified;
                        var end = Math.Min(LineCount, hunk.NewStartLine + hunk.NewLineCount);
                        for (var i = Math.Max(0, hunk.NewStartLine); i < end; i++)
                        {
                            _lines[i] = value;
                        }

                        break;
                }
            }
        }

        public IReadOnlyList<ChangeHunk> Hunks { get; }

        public int LineCount { get; }

        public bool IsEmpty => Hunks.Count == 0;

        public LineChange GetLineChange(int line) =>
            line >= 0 && line < LineCount ? _lines[line] : LineChange.None;

        // Boundary index equal to LineCount means after the last line
        public bool HasDeletionBefore(int line) =>
            line >= 0 && line <= LineCount && _deletedBefore[line];

        // Strongest change in an inclusive line range, used for collapsed regions
        public LineChange GetRangeChange(int firstLine, int lastLine, out bool hasDeletion)
        {
            hasDeletion = false;
            var result = LineChange.None;
            var first = Math.Max(0, firstLine);
            var last = Math.Min(LineCount - 1, lastLine);
            for (var i = first; i <= last; i++)
            {
                if (_lines[i] == LineChange.Modified)
                {
                    result = LineChange.Modified;
                }
                else if (_lines[i] == LineChange.Added && result == LineChange.None)
                {
                    result = LineChange.Added;
                }

                if (i > first && _deletedBefore[i])
                {
                    hasDeletion = true;
                }
            }

            return result;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
