using System;
using System.IO;
using System.Text;
using System.Threading;
using HgVsix.Core.Hg;
using Xunit;

namespace HgVsix.Core.Tests
{
    // Skips the test when no hg executable can be found
    public sealed class HgFactAttribute : FactAttribute
    {
        public HgFactAttribute()
        {
            if (!HgTestRepository.HgAvailable)
            {
                Skip = "hg executable not found";
            }
        }
    }

    // Temporary hg repository driven through the same runner the extension uses
    public sealed class HgTestRepository : IDisposable
    {
        private static readonly Lazy<bool> Available = new Lazy<bool>(() =>
        {
            try
            {
                var runner = CreateRunner();
                return runner.RunAsync(Path.GetTempPath(), new[] { "version", "-q" }, CancellationToken.None).GetAwaiter().GetResult().Success;
            }
            catch (HgException)
            {
                return false;
            }
        });

        public HgTestRepository()
        {
            Root = Path.Combine(Path.GetTempPath(), "HgVsixTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Runner = CreateRunner();
            Hg("init");
        }

        public static bool HgAvailable => Available.Value;

        public string Root { get; }

        public HgProcessRunner Runner { get; }

        public static HgProcessRunner CreateRunner() =>
            new HgProcessRunner(() => HgExecutableResolver.Resolve(null), TimeSpan.FromSeconds(60));

        public string PathOf(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public void Write(string relativePath, string content)
        {
            var path = PathOf(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public void WriteBytes(string relativePath, byte[] content)
        {
            var path = PathOf(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        public string Hg(params string[] arguments)
        {
            var result = Runner.RunAsync(Root, arguments, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Success)
            {
                throw new InvalidOperationException($"hg {string.Join(" ", arguments)} failed: {result.Error}");
            }

            return result.OutputText;
        }

        public void Commit(string message = "commit") => Hg("commit", "-A", "-m", message, "-u", "test <test@example.invalid>");

        public void Dispose()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
