using HgVsix.Core.Hg;
using Xunit;

namespace HgVsix.Core.Tests
{
    public class FileStatusTests
    {
        [Theory]
        [InlineData("C a.cs\n", HgFileState.Clean)]
        [InlineData("M a.cs\n", HgFileState.Modified)]
        [InlineData("A a.cs\n", HgFileState.Added)]
        [InlineData("R a.cs\n", HgFileState.Removed)]
        [InlineData("! a.cs\n", HgFileState.Missing)]
        [InlineData("? a.cs\n", HgFileState.Untracked)]
        [InlineData("I a.cs\n", HgFileState.Ignored)]
        [InlineData("", HgFileState.Unknown)]
        [InlineData("garbage", HgFileState.Unknown)]
        public void ParsesState(string output, HgFileState expected)
        {
            Assert.Equal(expected, FileStatus.Parse(output).State);
        }

        [Fact]
        public void ParsesCopySourceForAddedFile()
        {
            var status = FileStatus.Parse("A src\\new.cs\r\n  src\\old.cs\r\n");

            Assert.Equal(HgFileState.Added, status.State);
            Assert.Equal("src/old.cs", status.CopySource);
        }

        [Fact]
        public void KeepsSpacesThatBelongToTheCopySource()
        {
            Assert.Equal(" notes.txt ", FileStatus.Parse("A copy.txt\n   notes.txt \n").CopySource);
        }

        [Fact]
        public void IgnoresCopyLineForOtherStates()
        {
            Assert.Null(FileStatus.Parse("M a.cs\n  b.cs\n").CopySource);
        }
    }
}
