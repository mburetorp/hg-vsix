using System.Text;

namespace HgVsix.Core.Hg
{
    public sealed class HgResult
    {
        public HgResult(int exitCode, byte[] output, string error, bool outputLimitExceeded = false)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
            OutputLimitExceeded = outputLimitExceeded;
        }

        // Output is empty and hg was stopped
        public bool OutputLimitExceeded { get; }

        public int ExitCode { get; }

        public byte[] Output { get; }

        public string Error { get; }

        public bool Success => ExitCode == 0;

        public string OutputText => Encoding.UTF8.GetString(Output);
    }
}
