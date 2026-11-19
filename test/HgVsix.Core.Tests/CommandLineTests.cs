using HgVsix.Core.Hg;
using Xunit;

namespace HgVsix.Core.Tests
{
    public class CommandLineTests
    {
        [Theory]
        [InlineData(new[] { "cat", "-r", "." }, "cat -r .")]
        [InlineData(new[] { "path:a b.cs" }, "\"path:a b.cs\"")]
        [InlineData(new[] { "" }, "\"\"")]
        [InlineData(new[] { "a\"b" }, "\"a\\\"b\"")]
        [InlineData(new[] { "dir\\ x\\" }, "\"dir\\ x\\\\\"")]
        [InlineData(new[] { "no\\space" }, "no\\space")]
        public void QuotesLikeCommandLineToArgv(string[] arguments, string expected)
        {
            Assert.Equal(expected, HgProcessRunner.BuildCommandLine(arguments));
        }
    }
}
