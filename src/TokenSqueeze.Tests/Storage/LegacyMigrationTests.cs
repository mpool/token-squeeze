using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

[Collection("CLI")]
public sealed class LegacyMigrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storageDir;
    private readonly string? _previousOverride;
    private readonly IndexStore _store;
    private readonly LanguageRegistry _registry;

    public LegacyMigrationTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-legacy-" + Guid.NewGuid().ToString("N")[..8]);
        _storageDir = Path.Combine(Path.GetTempPath(), "ts-legacy-store-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_storageDir);
        StoragePaths.TestRootOverride = _storageDir;
        _store = new IndexStore();
        _registry = new LanguageRegistry();
    }

    public void Dispose()
    {
        StoragePaths.TestRootOverride = _previousOverride;
        _registry.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }

    private void WriteLegacyIndex(string projectName, string sourcePath)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        Directory.CreateDirectory(projectDir);

        var legacyIndex = new CodeIndex
        {
            ProjectName = projectName,
            SourcePath = sourcePath,
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, IndexedFile>
            {
                ["test.py"] = new IndexedFile
                {
                    Path = "test.py",
                    Hash = "abc123",
                    Language = "Python",
                    SymbolCount = 1
                }
            },
            Symbols =
            [
                new Symbol
                {
                    Id = "test.py::hello#Function",
                    File = "test.py",
                    Name = "hello",
                    QualifiedName = "hello",
                    Kind = SymbolKind.Function,
                    Language = "Python",
                    Signature = "def hello()",
                    Line = 1,
                    EndLine = 2,
                    ByteOffset = 0,
                    ByteLength = 20
                }
            ]
        };

        var json = JsonSerializer.Serialize(legacyIndex, JsonDefaults.Options);
        File.WriteAllText(Path.Combine(projectDir, "index.json"), json);
    }

    [Fact]
    public void IsLegacyFormat_TrueWhenOnlyIndexJsonExists()
    {
        WriteLegacyIndex("oldproj", "/tmp/oldproj");

        Assert.True(_store.IsLegacyFormat("oldproj"));
    }

    [Fact]
    public void IsLegacyFormat_FalseWhenManifestExists()
    {
        // Save in new format (creates manifest.json)
        var index = new CodeIndex
        {
            ProjectName = "newproj",
            SourcePath = "/tmp/newproj",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, IndexedFile>(),
            Symbols = []
        };
        _store.Save(index);

        Assert.False(_store.IsLegacyFormat("newproj"));
    }

    [Fact]
    public void IsLegacyFormat_FalseWhenNeitherExists()
    {
        Assert.False(_store.IsLegacyFormat("nonexistent"));
    }

    [Fact]
    public void GetLegacySourcePath_ExtractsCorrectPath()
    {
        WriteLegacyIndex("oldproj", "/some/source/path");

        var sourcePath = _store.GetLegacySourcePath("oldproj");

        Assert.Equal("/some/source/path", sourcePath);
    }

    [Fact]
    public void TryMigrateIfNeeded_ReturnsFalseForNewFormat()
    {
        var index = new CodeIndex
        {
            ProjectName = "newproj",
            SourcePath = "/tmp/newproj",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, IndexedFile>(),
            Symbols = []
        };
        _store.Save(index);

        var migrated = LegacyMigration.TryMigrateIfNeeded("newproj", _store, _registry, out var error);

        Assert.False(migrated);
        Assert.Null(error);
    }

    [Fact]
    public void TryMigrateIfNeeded_ReturnsErrorWhenSourcePathMissing()
    {
        WriteLegacyIndex("oldproj", "/nonexistent/path/that/does/not/exist");

        var migrated = LegacyMigration.TryMigrateIfNeeded("oldproj", _store, _registry, out var error);

        Assert.True(migrated);
        Assert.NotNull(error);
        Assert.Contains("source path is missing", error);
    }

    [Fact]
    public void TryMigrateIfNeeded_MigratesLegacyToNewFormat()
    {
        // Create a source directory with a real Python file
        var sourceDir = Path.Combine(_tempDir, "realproject");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "hello.py"), "def hello():\n    pass\n");

        // Write legacy index pointing to that source directory
        WriteLegacyIndex("realproject", sourceDir);

        // Verify it's legacy format
        Assert.True(_store.IsLegacyFormat("realproject"));

        // Capture stderr
        var originalErr = Console.Error;
        var errWriter = new StringWriter();
        Console.SetError(errWriter);

        try
        {
            var migrated = LegacyMigration.TryMigrateIfNeeded("realproject", _store, _registry, out var error);

            Assert.True(migrated);
            Assert.Null(error);

            // Verify new format files exist
            var manifestPath = StoragePaths.GetManifestPath("realproject");
            Assert.True(File.Exists(manifestPath), "manifest.json should exist after migration");

            var legacyPath = StoragePaths.GetLegacyIndexPath("realproject");
            Assert.False(File.Exists(legacyPath), "index.json should be deleted after migration");

            // Verify no longer legacy
            Assert.False(_store.IsLegacyFormat("realproject"));

            // Verify stderr message
            var stderrOutput = errWriter.ToString();
            Assert.Contains("Re-indexing", stderrOutput);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }
}
