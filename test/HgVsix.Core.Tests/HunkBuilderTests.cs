using HgVsix.Core.Diff;
using Xunit;

namespace HgVsix.Core.Tests
{
    public class HunkBuilderTests
    {
        [Fact]
        public void ClassifiesAddedModifiedAndDeleted()
        {
            var hunks = HunkBuilder.Build(new[]
            {
                new LineDifference(5, 2, 6, 0),
                new LineDifference(0, 0, 0, 1),
                new LineDifference(2, 1, 3, 2),
            });

            Assert.Equal(3, hunks.Count);
            Assert.Equal(new ChangeHunk(ChangeKind.Added, 0, 0, 0, 1), hunks[0]);
            Assert.Equal(new ChangeHunk(ChangeKind.Modified, 2, 1, 3, 2), hunks[1]);
            Assert.Equal(new ChangeHunk(ChangeKind.Deleted, 5, 2, 6, 0), hunks[2]);
        }

        [Fact]
        public void SkipsEmptyDifferences()
        {
            Assert.Empty(HunkBuilder.Build(new[] { new LineDifference(1, 0, 1, 0) }));
        }

        [Fact]
        public void AllAddedCoversEveryLine()
        {
            var hunk = Assert.Single(HunkBuilder.AllAdded(4));
            Assert.Equal(new ChangeHunk(ChangeKind.Added, 0, 0, 0, 4), hunk);
            Assert.Empty(HunkBuilder.AllAdded(0));
        }
    }

    public class ChangeMapTests
    {
        [Fact]
        public void MapsLinesAndDeletionBoundaries()
        {
            var map = new ChangeMap(new[]
            {
                new ChangeHunk(ChangeKind.Added, 0, 0, 0, 1),
                new ChangeHunk(ChangeKind.Modified, 2, 1, 3, 2),
                new ChangeHunk(ChangeKind.Deleted, 5, 2, 6, 0),
                new ChangeHunk(ChangeKind.Deleted, 9, 1, 8, 0),
            }, 8);

            Assert.Equal(LineChange.Added, map.GetLineChange(0));
            Assert.Equal(LineChange.None, map.GetLineChange(1));
            Assert.Equal(LineChange.Modified, map.GetLineChange(3));
            Assert.Equal(LineChange.Modified, map.GetLineChange(4));
            Assert.Equal(LineChange.None, map.GetLineChange(5));
            Assert.True(map.HasDeletionBefore(6));
            Assert.False(map.HasDeletionBefore(5));
            Assert.True(map.HasDeletionBefore(8));
            Assert.Equal(LineChange.None, map.GetLineChange(-1));
            Assert.Equal(LineChange.None, map.GetLineChange(99));
        }

        [Fact]
        public void RangeChangePrefersModifiedAndFindsInnerDeletions()
        {
            var map = new ChangeMap(new[]
            {
                new ChangeHunk(ChangeKind.Added, 0, 0, 1, 1),
                new ChangeHunk(ChangeKind.Modified, 3, 1, 3, 1),
                new ChangeHunk(ChangeKind.Deleted, 6, 1, 5, 0),
            }, 10);

            Assert.Equal(LineChange.Modified, map.GetRangeChange(0, 4, out var deletion1));
            Assert.False(deletion1);
            Assert.Equal(LineChange.None, map.GetRangeChange(4, 7, out var deletion2));
            Assert.True(deletion2);
            Assert.Equal(LineChange.None, map.GetRangeChange(5, 7, out var deletion3));
            Assert.False(deletion3);
        }

        [Fact]
        public void ClampsHunksOutsideTheSnapshot()
        {
            var map = new ChangeMap(new[]
            {
                new ChangeHunk(ChangeKind.Added, 0, 0, 3, 10),
                new ChangeHunk(ChangeKind.Deleted, 0, 1, 50, 0),
            }, 5);

            Assert.Equal(LineChange.Added, map.GetLineChange(4));
            Assert.True(map.HasDeletionBefore(5));
        }
    }
}
