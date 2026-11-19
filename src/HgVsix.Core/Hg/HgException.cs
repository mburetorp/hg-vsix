using System;

namespace HgVsix.Core.Hg
{
    public sealed class HgException : Exception
    {
        public HgException(string message)
            : base(message)
        {
        }

        public HgException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
