using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

[Collection("CLI")]
public sealed class StoragePathsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousOverride;

    public StoragePathsTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        StoragePaths.TestRootOverride = _tempDir;
    }

    public void Dispose()
    {
        StoragePaths.TestRootOverride = _previousOverride;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetManifestPath_ReturnsManifestJson()
    {
        var path = StoragePaths.GetManifestPath("myproject");
        Assert.Equal(Path.Combine(_tempDir, "myproject", "manifest.json"), path);
    }

    [Fact]
    public void GetFilesDir_ReturnsFilesSubdirectory()
    {
        var path = StoragePaths.GetFilesDir("myproject");
        Assert.Equal(Path.Combine(_tempDir, "myproject", "files"), path);
    }

    [Fact]
    public void GetFileFragmentPath_ReturnsCorrectPath()
    {
        var path = StoragePaths.GetFileFragmentPath("myproject", "src-main-cs");
        Assert.Equal(Path.Combine(_tempDir, "myproject", "files", "src-main-cs.json"), path);
    }

    [Fact]
    public void GetSearchIndexPath_ReturnsSearchIndexJson()
    {
        var path = StoragePaths.GetSearchIndexPath("myproject");
        Assert.Equal(Path.Combine(_tempDir, "myproject", "search-index.json"), path);
    }

    [Fact]
    public void GetLegacyIndexPath_ReturnsIndexJson()
    {
        var path = StoragePaths.GetLegacyIndexPath("myproject");
        Assert.Equal(Path.Combine(_tempDir, "myproject", "index.json"), path);
    }

    [Fact]
    public void GetLegacyIndexPath_IsDistinctFromCurrentFormatPaths()
    {
        var legacyPath = StoragePaths.GetLegacyIndexPath("myproject");
        var manifestPath = StoragePaths.GetManifestPath("myproject");
        var searchIndexPath = StoragePaths.GetSearchIndexPath("myproject");

        Assert.NotEqual(legacyPath, manifestPath);
        Assert.NotEqual(legacyPath, searchIndexPath);
    }

    [Fact]
    public void GetIndexPath_DoesNotExist()
    {
        // GetIndexPath was removed — it returned the same path as GetLegacyIndexPath (BUG-03).
        // This test verifies the method no longer exists via reflection.
        var method = typeof(StoragePaths).GetMethod("GetIndexPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.Null(method);
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
