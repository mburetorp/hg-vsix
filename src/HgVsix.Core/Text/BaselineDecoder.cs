using System;
using System.Collections.Generic;
using System.Text;

namespace HgVsix.Core.Text
{
    public static class BaselineDecoder
    {
        private const int BinaryProbeLength = 8000;

        // Same heuristic as git and hg: a NUL byte near the start means binary
        public static bool IsBinary(byte[] data)
        {
            if (HasUtf16Bom(data))
            {
                return false;
            }

            var length = Math.Min(data.Length, BinaryProbeLength);
            for (var i = 0; i < length; i++)
            {
                if (data[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }

        // A BOM wins over the preferred encoding. Without one the preferred encoding is used.
        public static string Decode(byte[] data, Encoding? preferredEncoding)
        {
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                return new UTF8Encoding(false).GetString(data, 3, data.Length - 3);
            }

            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                return new UnicodeEncoding(false, false).GetString(data, 2, data.Length - 2);
            }

            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                return new UnicodeEncoding(true, false).GetString(data, 2, data.Length - 2);
            }

            var encoding = preferredEncoding ?? new UTF8Encoding(false);
            var text = encoding.GetString(data);
            return text.Length > 0 && text[0] == (char)0xFEFF ? text.Substring(1) : text;
        }

        // Splits on CRLF, CR or LF without the line breaks.
        // A trailing line break yields a final empty line like an editor snapshot does.
        public static IReadOnlyList<string> SplitLines(string text)
        {
            var lines = new List<string>();
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r' || c == '\n')
                {
                    lines.Add(text.Substring(start, i - start));
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    start = i + 1;
                }
            }

            lines.Add(text.Substring(start));
            return lines;
        }

        private static bool HasUtf16Bom(byte[] data) =>
            data.Length >= 2 && ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF));
    }
}
