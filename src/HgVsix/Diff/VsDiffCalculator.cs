using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using HgVsix.Core.Diff;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;

namespace HgVsix.Diff
{
    // Line diff using the VS differencing service.
    // VS keeps line breaks in its line tokens so both sides are normalized to LF first.
    [Export(typeof(VsDiffCalculator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class VsDiffCalculator
    {
        private static readonly StringDifferenceOptions Options = new StringDifferenceOptions(StringDifferenceTypes.Line, 0, false);

        private readonly ITextDifferencingSelectorService _selector;

        [ImportingConstructor]
        public VsDiffCalculator(ITextDifferencingSelectorService selector)
        {
            _selector = selector;
        }

        // Every line ends with LF so the last line compares like the others
        public static string NormalizeLines(IEnumerable<string> lines)
        {
            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                builder.Append(line).Append('\n');
            }

            return builder.ToString();
        }

        public static string NormalizeSnapshot(ITextSnapshot snapshot)
        {
            var builder = new StringBuilder(snapshot.Length + snapshot.LineCount);
            foreach (var line in snapshot.Lines)
            {
                builder.Append(line.GetText()).Append('\n');
            }

            return builder.ToString();
        }

        public IReadOnlyList<ChangeHunk> Compute(string normalizedBaseline, string normalizedCurrent, ITextSnapshot snapshot)
        {
            if (normalizedBaseline == normalizedCurrent)
            {
                return new ChangeHunk[0];
            }

            var service = _selector.GetTextDifferencingService(snapshot.ContentType) ?? _selector.DefaultTextDifferencingService;
            var differences = service.DiffStrings(normalizedBaseline, normalizedCurrent, Options);
            return HunkBuilder.Build(differences.Differences.Select(d =>
                new LineDifference(d.Left.Start, d.Left.Length, d.Right.Start, d.Right.Length)));
        }
    }
}
