using System.Security;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

public sealed class IndexStoreValidationTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly IndexStore _store;

    public IndexStoreValidationTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "ts-test-" + Guid.NewGuid().ToString("N"));
        _store = new IndexStore(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    public void SaveFileFragment_RejectsTraversalStorageKey(string storageKey)
    {
        var fragment = new FileSymbolData { File = "test.cs", Symbols = [] };
        Assert.Throws<SecurityException>(() => _store.SaveFileFragment(storageKey, fragment));
    }

    [Fact]
    public void SaveFileFragment_AcceptsValidStorageKey()
    {
        // Set up required directories so the write can succeed
        var filesDir = StoragePaths.GetFilesDir(_cacheDir);
        Directory.CreateDirectory(filesDir);

        var fragment = new FileSymbolData { File = "test.cs", Symbols = [] };
        var ex = Record.Exception(() => _store.SaveFileFragment("src-main-cs", fragment));
        Assert.Null(ex);
    }

    [Fact]
    public void RebuildSearchIndex_RejectsTraversalStorageKey()
    {
        var manifest = new Manifest
        {
            FormatVersion = 3,
            SourcePath = "/fake",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, ManifestFileEntry>
            {
                ["evil.cs"] = new ManifestFileEntry
                {
                    Path = "evil.cs",
                    Hash = "abc",
                    Language = "C#",
                    SymbolCount = 1,
                    StorageKey = "../../escape"
                }
            }
        };

        Assert.Throws<SecurityException>(() => _store.RebuildSearchIndex(manifest));
    }

    [Fact]
    public void RebuildSearchIndex_AcceptsValidManifest()
    {
        // RebuildSearchIndex writes to disk, so directory must exist
        Directory.CreateDirectory(_cacheDir);

        var manifest = new Manifest
        {
            FormatVersion = 3,
            SourcePath = "/fake",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, ManifestFileEntry>()
        };

        var ex = Record.Exception(() => _store.RebuildSearchIndex(manifest));
        Assert.Null(ex);
    }
}
