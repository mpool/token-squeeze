using System.Security;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

[Collection("CLI")]
public sealed class IndexStoreValidationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousOverride;
    private readonly IndexStore _store;

    public IndexStoreValidationTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        StoragePaths.TestRootOverride = _tempDir;
        _store = new IndexStore();
    }

    public void Dispose()
    {
        StoragePaths.TestRootOverride = _previousOverride;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    public void SaveFileFragment_RejectsTraversalStorageKey(string storageKey)
    {
        var fragment = new FileSymbolData { File = "test.cs", Symbols = [] };
        Assert.Throws<SecurityException>(() => _store.SaveFileFragment("valid-project", storageKey, fragment));
    }

    [Fact]
    public void SaveFileFragment_AcceptsValidStorageKey()
    {
        // Set up required directories so the write can succeed
        var projectDir = StoragePaths.GetProjectDir("valid-project");
        var filesDir = StoragePaths.GetFilesDir("valid-project");
        Directory.CreateDirectory(filesDir);

        var fragment = new FileSymbolData { File = "test.cs", Symbols = [] };
        var ex = Record.Exception(() => _store.SaveFileFragment("valid-project", "src-main-cs", fragment));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("../../etc")]
    [InlineData("..\\windows")]
    public void RebuildSearchIndex_RejectsTraversalProjectName(string projectName)
    {
        var manifest = new Manifest
        {
            FormatVersion = 2,
            ProjectName = projectName,
            SourcePath = "/fake",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, ManifestFileEntry>()
        };

        Assert.Throws<SecurityException>(() => _store.RebuildSearchIndex(projectName, manifest));
    }

    [Fact]
    public void RebuildSearchIndex_AcceptsValidProjectName()
    {
        var projectDir = StoragePaths.GetProjectDir("valid-project");
        Directory.CreateDirectory(projectDir);

        var manifest = new Manifest
        {
            FormatVersion = 2,
            ProjectName = "valid-project",
            SourcePath = "/fake",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, ManifestFileEntry>()
        };

        var ex = Record.Exception(() => _store.RebuildSearchIndex("valid-project", manifest));
        Assert.Null(ex);
    }
}
