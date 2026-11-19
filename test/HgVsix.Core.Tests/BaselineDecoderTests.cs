using System.Text;
using HgVsix.Core.Text;
using Xunit;

namespace HgVsix.Core.Tests
{
    public class BaselineDecoderTests
    {
        [Fact]
        public void StripsUtf8Bom()
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', (byte)'b' };
            Assert.Equal("ab", BaselineDecoder.Decode(bytes, Encoding.GetEncoding("iso-8859-1")));
        }

        [Fact]
        public void DecodesUtf16WithBom()
        {
            var bytes = new byte[] { 0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0 };
            Assert.False(BaselineDecoder.IsBinary(bytes));
            Assert.Equal("hi", BaselineDecoder.Decode(bytes, null));
        }

        [Fact]
        public void UsesPreferredEncodingWithoutBom()
        {
            var bytes = new byte[] { 0xE5 };
            Assert.Equal(((char)0xE5).ToString(), BaselineDecoder.Decode(bytes, Encoding.GetEncoding("iso-8859-1")));
        }

        [Fact]
        public void DefaultsToUtf8()
        {
            var text = new string(new[] { (char)0xE5, (char)0xE4, (char)0xF6 });
            var bytes = Encoding.UTF8.GetBytes(text);
            Assert.Equal(text, BaselineDecoder.Decode(bytes, null));
        }

        [Fact]
        public void DetectsBinary()
        {
            Assert.True(BaselineDecoder.IsBinary(new byte[] { 1, 2, 0, 3 }));
            Assert.False(BaselineDecoder.IsBinary(Encoding.ASCII.GetBytes("text\r\n")));
            Assert.False(BaselineDecoder.IsBinary(new byte[0]));
        }

        [Theory]
        [InlineData("", new[] { "" })]
        [InlineData("a", new[] { "a" })]
        [InlineData("a\n", new[] { "a", "" })]
        [InlineData("a\r\nb\rc\nd", new[] { "a", "b", "c", "d" })]
        [InlineData("\r\n\r\n", new[] { "", "", "" })]
        public void SplitsLinesLikeTheEditor(string text, string[] expected)
        {
            Assert.Equal(expected, BaselineDecoder.SplitLines(text));
        }
    }
}
