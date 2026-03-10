using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

public sealed class StoragePathsTests
{
    [Fact]
    public void GetManifestPath_ReturnsManifestJson()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "test-cache");
        var path = StoragePaths.GetManifestPath(cacheDir);
        Assert.Equal(Path.Combine(cacheDir, "manifest.json"), path);
    }

    [Fact]
    public void GetFilesDir_ReturnsFilesSubdirectory()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "test-cache");
        var path = StoragePaths.GetFilesDir(cacheDir);
        Assert.Equal(Path.Combine(cacheDir, "files"), path);
    }

    [Fact]
    public void GetFileFragmentPath_ReturnsCorrectPath()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "test-cache");
        var path = StoragePaths.GetFileFragmentPath(cacheDir, "src-main-cs");
        Assert.Equal(Path.Combine(cacheDir, "files", "src-main-cs.json"), path);
    }

    [Fact]
    public void GetSearchIndexPath_ReturnsSearchIndexJson()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "test-cache");
        var path = StoragePaths.GetSearchIndexPath(cacheDir);
        Assert.Equal(Path.Combine(cacheDir, "search-index.json"), path);
    }

    [Theory]
    [InlineData("src/main.cs", "src-main.cs")]
    [InlineData("src\\main.cs", "src-main.cs")]
    [InlineData("./src/main.cs", "src-main.cs")]
    [InlineData("../outside/file.cs", "outside-file.cs")]
    [InlineData("-leading-dash.cs", "leading-dash.cs")]
    [InlineData("simple.cs", "simple.cs")]
    public void PathToStorageKey_SanitizesCorrectly(string input, string expected)
    {
        var result = StoragePaths.PathToStorageKey(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PathToStorageKey_EmptyResult_ReturnsUnderscore()
    {
        var result = StoragePaths.PathToStorageKey("...");
        Assert.Equal("_empty", result);
    }

    [Fact]
    public void PathToStorageKey_LongPath_ReturnsTruncatedHashedKey()
    {
        var longPath = string.Join("/", Enumerable.Range(0, 30).Select(i => $"directory{i:D10}")) + "/file.cs";
        Assert.True(longPath.Length > 260, "Test path should exceed 260 chars");

        var result = StoragePaths.PathToStorageKey(longPath);
        Assert.True(result.Length <= 200, $"Storage key length {result.Length} exceeds 200 chars");
    }

    [Fact]
    public void PathToStorageKey_ShortPath_ReturnsUnchanged()
    {
        var shortPath = "src/main.cs";
        var result = StoragePaths.PathToStorageKey(shortPath);
        Assert.Equal("src-main.cs", result); // Normal sanitization, no hash
    }

    [Fact]
    public void PathToStorageKey_TwoDifferentLongPaths_ReturnDifferentKeys()
    {
        var longPath1 = string.Join("/", Enumerable.Range(0, 30).Select(i => $"directoryA{i:D10}")) + "/file.cs";
        var longPath2 = string.Join("/", Enumerable.Range(0, 30).Select(i => $"directoryB{i:D10}")) + "/file.cs";

        Assert.True(longPath1.Length > 260);
        Assert.True(longPath2.Length > 260);

        var key1 = StoragePaths.PathToStorageKey(longPath1);
        var key2 = StoragePaths.PathToStorageKey(longPath2);

        Assert.NotEqual(key1, key2);
    }
}
