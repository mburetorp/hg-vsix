using System.Collections.Generic;
using System.Linq;

namespace HgVsix.Core.Diff
{
    public static class HunkBuilder
    {
        public static IReadOnlyList<ChangeHunk> Build(IEnumerable<LineDifference> differences)
        {
            var hunks = new List<ChangeHunk>();
            foreach (var d in differences.OrderBy(d => d.NewStart).ThenBy(d => d.OldStart))
            {
                if (d.OldLength == 0 && d.NewLength == 0)
                {
                    continue;
                }

                var kind = d.OldLength == 0
                    ? ChangeKind.Added
                    : d.NewLength == 0 ? ChangeKind.Deleted : ChangeKind.Modified;

                hunks.Add(new ChangeHunk(kind, d.OldStart, d.OldLength, d.NewStart, d.NewLength));
            }

            return hunks;
        }

        // Treats every line of a new file as added
        public static IReadOnlyList<ChangeHunk> AllAdded(int newLineCount) =>
            newLineCount <= 0
                ? new ChangeHunk[0]
                : new[] { new ChangeHunk(ChangeKind.Added, 0, 0, 0, newLineCount) };
    }
}
