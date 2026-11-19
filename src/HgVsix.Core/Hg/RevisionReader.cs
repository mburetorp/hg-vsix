using System.Threading;
using System.Threading.Tasks;

namespace HgVsix.Core.Hg
{
    // Thin wrappers around the read-only hg commands used by the extension
    public sealed class RevisionReader
    {
        public const string NullNode = "0000000000000000000000000000000000000000";

        private readonly HgProcessRunner _runner;

        public RevisionReader(HgProcessRunner runner)
        {
            _runner = runner;
        }

        // Returns the full hash of the first working directory parent
        public async Task<string> GetParentNodeAsync(string root, CancellationToken cancellationToken)
        {
            var result = await _runner.RunAsync(root, new[] { "log", "-r", ".", "-T", "{node}" }, cancellationToken).ConfigureAwait(false);
            var node = result.OutputText.Trim();
            if (!result.Success || node.Length != 40)
            {
                throw new HgException($"hg log failed in {root}: {result.Error.Trim()}");
            }

            return node;
        }

        public async Task<FileStatus> GetFileStatusAsync(string root, string relativePath, CancellationToken cancellationToken)
        {
            var result = await _runner.RunAsync(root, new[] { "status", "-A", "-C", "--", "path:" + relativePath }, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                throw new HgException($"hg status failed for {relativePath}: {result.Error.Trim()}");
            }

            return FileStatus.Parse(result.OutputText);
        }

        // Returns null when the file is larger than maxBytes
        public async Task<byte[]?> GetFileContentAsync(string root, string node, string relativePath, long maxBytes, CancellationToken cancellationToken)
        {
            var result = await _runner.RunAsync(root, new[] { "cat", "-r", node, "--", "path:" + relativePath }, cancellationToken, maxBytes).ConfigureAwait(false);
            if (result.OutputLimitExceeded)
            {
                return null;
            }

            if (!result.Success)
            {
                throw new HgException($"hg cat failed for {relativePath}@{ShortNode(node)}: {result.Error.Trim()}");
            }

            return result.Output;
        }

        public static string ShortNode(string node) => node.Length > 12 ? node.Substring(0, 12) : node;
    }
}
