using System;
using System.IO;
using HgVsix.Core.Hg;
using Xunit;

namespace HgVsix.Core.Tests
{
    public sealed class RepositoryLocatorTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "HgVsixTests", Guid.NewGuid().ToString("N"));

        public RepositoryLocatorTests()
        {
            Directory.CreateDirectory(Path.Combine(_root, "outer", ".hg"));
            Directory.CreateDirectory(Path.Combine(_root, "outer", "src", "deep"));
            Directory.CreateDirectory(Path.Combine(_root, "outer", "nested", ".hg"));
            Directory.CreateDirectory(Path.Combine(_root, "plain"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void FindsNearestRepository()
        {
            var locator = new RepositoryLocator();

            var location = locator.Locate(Path.Combine(_root, "outer", "src", "deep", "File.cs"));

            Assert.NotNull(location);
            Assert.Equal(Path.Combine(_root, "outer"), location!.Root);
            Assert.Equal("src/deep/File.cs", location.RelativePath);
        }

        [Fact]
        public void NestedRepositoryWins()
        {
            var location = new RepositoryLocator().Locate(Path.Combine(_root, "outer", "nested", "a.txt"));

            Assert.Equal(Path.Combine(_root, "outer", "nested"), location!.Root);
            Assert.Equal("a.txt", location.RelativePath);
        }

        [Fact]
        public void ReturnsNullOutsideRepository()
        {
            Assert.Null(new RepositoryLocator().Locate(Path.Combine(_root, "plain", "a.txt")));
        }

        [Fact]
        public void IgnoresFilesInsideHgFolder()
        {
            Assert.Null(new RepositoryLocator().Locate(Path.Combine(_root, "outer", ".hg", "hgrc")));
        }

        [Fact]
        public void CachedOnlyLookupNeverProbes()
        {
            var locator = new RepositoryLocator();
            var file = Path.Combine(_root, "outer", "src", "File.cs");
            var outside = Path.Combine(_root, "plain", "a.txt");

            Assert.False(locator.TryLocateCached(file, out var unknown));
            Assert.Null(unknown);

            Assert.NotNull(locator.Locate(file));
            Assert.True(locator.TryLocateCached(file, out var cached));
            Assert.Equal(Path.Combine(_root, "outer"), cached!.Root);

            // Known to be outside a repository
            Assert.Null(locator.Locate(outside));
            Assert.True(locator.TryLocateCached(outside, out var none));
            Assert.Null(none);
        }

        [Fact]
        public void NewSubrepoTakesOverAfterCacheClear()
        {
            var locator = new RepositoryLocator();
            var file = Path.Combine(_root, "outer", "src", "deep", "File.cs");
            Assert.Equal(Path.Combine(_root, "outer"), locator.Locate(file)!.Root);

            Directory.CreateDirectory(Path.Combine(_root, "outer", "src", ".hg"));
            locator.ClearCache();

            var location = locator.Locate(file);
            Assert.Equal(Path.Combine(_root, "outer", "src"), location!.Root);
            Assert.Equal("deep/File.cs", location.RelativePath);
        }

        [Fact]
        public void CacheIsClearedOnRequest()
        {
            var locator = new RepositoryLocator();
            var file = Path.Combine(_root, "plain", "a.txt");
            Assert.Null(locator.Locate(file));

            Directory.CreateDirectory(Path.Combine(_root, "plain", ".hg"));
            Assert.Null(locator.Locate(file));

            locator.ClearCache();
            Assert.NotNull(locator.Locate(file));
        }
    }
}
