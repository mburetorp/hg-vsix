using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HgVsix.Core.Hg;
using Xunit;

namespace HgVsix.Core.Tests
{
    public sealed class BaselineProviderTests : System.IDisposable
    {
        private readonly HgTestRepository _repo;
        private readonly BaselineProvider _provider;

        public BaselineProviderTests()
        {
            if (!HgTestRepository.HgAvailable)
            {
                _repo = null!;
                _provider = null!;
                return;
            }

            _repo = new HgTestRepository();
            _provider = new BaselineProvider(new RepositoryLocator(), new RevisionReader(_repo.Runner), () => 1024 * 1024);
        }

        public void Dispose() => _repo?.Dispose();

        private Task<Baseline> Get(string relativePath) => _provider.GetBaselineAsync(_repo.PathOf(relativePath), CancellationToken.None);

        private static string Text(Baseline baseline) => Encoding.UTF8.GetString(baseline.Content!);

        [HgFact]
        public async Task CleanAndModifiedFilesUseParentContent()
        {
            _repo.Write("src/a.cs", "one\ntwo\n");
            _repo.Commit();

            var clean = await Get("src/a.cs");
            Assert.Equal(BaselineState.Available, clean.State);
            Assert.Equal(HgFileState.Clean, clean.Status!.State);
            Assert.Equal("one\ntwo\n", Text(clean));

            _repo.Write("src/a.cs", "one\nTWO\n");
            _provider.InvalidateRepository(_repo.Root);

            var modified = await Get("src/a.cs");
            Assert.Equal(HgFileState.Modified, modified.Status!.State);
            Assert.Equal("one\ntwo\n", Text(modified));
            Assert.Equal(clean.Node, modified.Node);
        }

        [HgFact]
        public async Task AddedFileHasEmptyBaseline()
        {
            _repo.Write("a.txt", "x\n");
            _repo.Commit();
            _repo.Write("new.txt", "fresh\n");
            _repo.Hg("add", "new.txt");

            var baseline = await Get("new.txt");

            Assert.Equal(BaselineState.Available, baseline.State);
            Assert.True(baseline.IsNewFile);
            Assert.Empty(baseline.Content!);
        }

        [HgFact]
        public async Task AddedFileInEmptyRepositoryHasEmptyBaseline()
        {
            _repo.Write("first.txt", "x\n");
            _repo.Hg("add", "first.txt");

            var baseline = await Get("first.txt");

            Assert.Equal(BaselineState.Available, baseline.State);
            Assert.Equal(RevisionReader.NullNode, baseline.Node);
            Assert.Empty(baseline.Content!);
        }

        [HgFact]
        public async Task RenamedFileUsesCopySource()
        {
            _repo.Write("old name.txt", "content\n");
            _repo.Commit();
            _repo.Hg("mv", "old name.txt", "new name.txt");

            var baseline = await Get("new name.txt");

            Assert.Equal(BaselineState.Available, baseline.State);
            Assert.False(baseline.IsNewFile);
            Assert.Equal("old name.txt", baseline.SourcePath);
            Assert.Equal("content\n", Text(baseline));
        }

        [HgFact]
        public async Task UntrackedIgnoredAndOutsideFilesHaveNoBaseline()
        {
            _repo.Write(".hgignore", "syntax: glob\n*.log\n");
            _repo.Commit();
            _repo.Write("loose.txt", "x");
            _repo.Write("build.log", "x");

            Assert.Equal(BaselineState.Untracked, (await Get("loose.txt")).State);
            Assert.Equal(BaselineState.Ignored, (await Get("build.log")).State);

            var outside = await _provider.GetBaselineAsync(Path.Combine(Path.GetTempPath(), "not-a-repo-file.txt"), CancellationToken.None);
            Assert.Equal(BaselineState.NotInRepository, outside.State);
        }

        [HgFact]
        public async Task BinaryAndLargeFilesAreRejected()
        {
            _repo.WriteBytes("image.bin", new byte[] { 1, 0, 2, 3 });
            _repo.Write("big.txt", new string('x', 2 * 1024 * 1024));
            _repo.Commit();

            Assert.Equal(BaselineState.Binary, (await Get("image.bin")).State);
            Assert.Equal(BaselineState.TooLarge, (await Get("big.txt")).State);
        }

        [HgFact]
        public async Task OversizedContentIsNotCached()
        {
            _repo.Write("a.txt", new string('x', 1000));
            _repo.Commit();
            long limit = 999;
            var cats = 0;
            var provider = new BaselineProvider(new RepositoryLocator(), new RevisionReader(_repo.Runner), () => limit);
            _repo.Runner.Log += message =>
            {
                if (message.Contains(" cat "))
                {
                    Interlocked.Increment(ref cats);
                }
            };

            Assert.Equal(BaselineState.TooLarge, (await provider.GetBaselineAsync(_repo.PathOf("a.txt"), CancellationToken.None)).State);
            Assert.Equal(1, cats);

            // Read again because the oversized content was not cached
            limit = 1000;
            var baseline = await provider.GetBaselineAsync(_repo.PathOf("a.txt"), CancellationToken.None);
            Assert.Equal(BaselineState.Available, baseline.State);
            Assert.Equal(1000, baseline.Content!.Length);
            Assert.Equal(2, cats);

            // Content within the limit comes from the cache
            Assert.Equal(BaselineState.Available, (await provider.GetBaselineAsync(_repo.PathOf("a.txt"), CancellationToken.None)).State);
            Assert.Equal(2, cats);

            // A lowered limit applies to cached content too
            limit = 10;
            Assert.Equal(BaselineState.TooLarge, (await provider.GetBaselineAsync(_repo.PathOf("a.txt"), CancellationToken.None)).State);
        }

        [HgFact]
        public async Task RunnerStopsAtOutputLimit()
        {
            _repo.Write("big.txt", new string('x', 500_000));
            _repo.Commit();

            var result = await _repo.Runner.RunAsync(_repo.Root, new[] { "cat", "-r", ".", "big.txt" }, CancellationToken.None, 1000);

            Assert.True(result.OutputLimitExceeded);
            Assert.Empty(result.Output);
        }

        [HgFact]
        public async Task ExternalCommitAndUpdateAreDetectedWithoutInvalidation()
        {
            _repo.Write("a.txt", "v1\n");
            _repo.Commit("first");
            var first = await Get("a.txt");

            _repo.Write("a.txt", "v2\n");
            Assert.Equal(HgFileState.Modified, (await Get("a.txt")).Status!.State);
            _repo.Commit("second");

            var second = await Get("a.txt");
            Assert.NotEqual(first.Node, second.Node);
            Assert.Equal("v2\n", Text(second));
            Assert.Equal(HgFileState.Clean, second.Status!.State);

            _repo.Hg("update", "-r", "0");
            var updated = await Get("a.txt");
            Assert.Equal(first.Node, updated.Node);
            Assert.Equal("v1\n", Text(updated));
        }

        [HgFact]
        public async Task RestoredFileIsDetectedWithoutDirstateChange()
        {
            _repo.Write("a.txt", "v1\n");
            _repo.Commit();
            Assert.Equal(BaselineState.Available, (await Get("a.txt")).State);

            File.Delete(_repo.PathOf("a.txt"));
            var missing = await Get("a.txt");
            Assert.Equal(BaselineState.NotTracked, missing.State);
            Assert.Equal(HgFileState.Missing, missing.Status!.State);

            _repo.Write("a.txt", "v1 restored\n");
            var restored = await Get("a.txt");
            Assert.Equal(BaselineState.Available, restored.State);
            Assert.Equal("v1\n", Text(restored));
        }

        [HgFact]
        public async Task SubrepoFilesUseTheSubrepoParent()
        {
            _repo.Write("outer.txt", "outer\n");
            _repo.Hg("init", "sub");
            _repo.Write("sub/inner.txt", "inner v1\n");
            _repo.Hg("-R", "sub", "commit", "-A", "-m", "sub first", "-u", "test");
            _repo.Write(".hgsub", "sub = sub\n");
            _repo.Commit("outer first");
            var outerNode = _repo.Hg("log", "-r", ".", "-T", "{node}");

            var inner = await Get("sub/inner.txt");
            Assert.Equal(BaselineState.Available, inner.State);
            Assert.Equal(_repo.PathOf("sub"), inner.Location!.Root);
            Assert.Equal("inner.txt", inner.Location.RelativePath);
            Assert.NotEqual(outerNode, inner.Node);
            Assert.Equal("inner v1\n", Text(inner));

            // A subrepo commit moves only the subrepo parent
            _repo.Write("sub/inner.txt", "inner v2\n");
            _repo.Hg("-R", "sub", "commit", "-m", "sub second", "-u", "test");
            var committed = await Get("sub/inner.txt");
            Assert.Equal("inner v2\n", Text(committed));
            Assert.NotEqual(inner.Node, committed.Node);

            var outer = await Get("outer.txt");
            Assert.Equal(_repo.Root, outer.Location!.Root);
            Assert.Equal(outerNode, outer.Node);
        }

        [HgFact]
        public async Task MissingHgIsReportedAsError()
        {
            _repo.Write("a.txt", "v1\n");
            _repo.Commit();
            var runner = new HgProcessRunner(() => Path.Combine(_repo.Root, "no-such-hg.exe"), System.TimeSpan.FromSeconds(5));
            var provider = new BaselineProvider(new RepositoryLocator(), new RevisionReader(runner), () => 1024);

            var baseline = await provider.GetBaselineAsync(_repo.PathOf("a.txt"), CancellationToken.None);

            Assert.Equal(BaselineState.Error, baseline.State);
            Assert.NotNull(baseline.Message);
        }
    }
}
