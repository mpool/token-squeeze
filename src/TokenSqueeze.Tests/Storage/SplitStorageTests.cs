using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

[Collection("CLI")]
public sealed class SplitStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousOverride;
    private readonly IndexStore _store;

    public SplitStorageTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-split-" + Guid.NewGuid().ToString("N"));
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

    private static CodeIndex CreateTestIndex(string projectName = "testproj", params (string file, string lang)[] files)
    {
        if (files.Length == 0)
            files = [("src/main.cs", "C#"), ("src/utils.cs", "C#")];

        var symbols = new List<Symbol>();
        var indexedFiles = new Dictionary<string, IndexedFile>();

        foreach (var (file, lang) in files)
        {
            var sym = new Symbol
            {
                Id = Symbol.MakeId(file, "TestFunc", SymbolKind.Function),
                File = file,
                Name = "TestFunc",
                QualifiedName = "TestFunc",
                Kind = SymbolKind.Function,
                Language = lang,
                Signature = "void TestFunc()",
                Line = 1,
                EndLine = 5,
                ByteOffset = 0,
                ByteLength = 100,
                ContentHash = "abc123"
            };
            symbols.Add(sym);

            indexedFiles[file] = new IndexedFile
            {
                Path = file,
                Hash = "filehash_" + file.GetHashCode(),
                Language = lang,
                SymbolCount = 1
            };
        }

        return new CodeIndex
        {
            ProjectName = projectName,
            SourcePath = "/tmp/" + projectName,
            IndexedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Files = indexedFiles,
            Symbols = symbols
        };
    }

    [Fact]
    public void Save_CreatesManifestWithFormatVersion2()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var manifestPath = StoragePaths.GetManifestPath("testproj");
        Assert.True(File.Exists(manifestPath), "manifest.json should exist");

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(json, JsonDefaults.Options);
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.FormatVersion);
        Assert.Equal("testproj", manifest.ProjectName);
        Assert.Equal(2, manifest.Files.Count);
    }

    [Fact]
    public void Save_CreatesFragmentPerSourceFile()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var filesDir = StoragePaths.GetFilesDir("testproj");
        Assert.True(Directory.Exists(filesDir), "files/ directory should exist");

        var fragments = Directory.GetFiles(filesDir, "*.json");
        Assert.Equal(2, fragments.Length);
    }

    [Fact]
    public void Save_DoesNotCreateLegacyIndexJson()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var legacyPath = StoragePaths.GetLegacyIndexPath("testproj");
        Assert.False(File.Exists(legacyPath), "index.json should NOT exist after split save");
    }

    [Fact]
    public void Save_CreatesSearchIndexWithoutByteFields()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var searchPath = StoragePaths.GetSearchIndexPath("testproj");
        Assert.True(File.Exists(searchPath), "search-index.json should exist");

        var json = File.ReadAllText(searchPath);
        var symbols = JsonSerializer.Deserialize<List<Symbol>>(json, JsonDefaults.Options);
        Assert.NotNull(symbols);
        Assert.Equal(2, symbols.Count);

        foreach (var sym in symbols)
        {
            Assert.Equal(0, sym.ByteOffset);
            Assert.Equal(0, sym.ByteLength);
            Assert.Equal("", sym.ContentHash);
        }
    }

    [Fact]
    public void Save_Load_RoundTrip_PreservesAllData()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var loaded = _store.Load("testproj");
        Assert.NotNull(loaded);
        Assert.Equal(index.ProjectName, loaded.ProjectName);
        Assert.Equal(index.SourcePath, loaded.SourcePath);
        Assert.Equal(index.IndexedAt, loaded.IndexedAt);
        Assert.Equal(index.Files.Count, loaded.Files.Count);
        Assert.Equal(index.Symbols.Count, loaded.Symbols.Count);

        foreach (var sym in index.Symbols)
        {
            var match = loaded.Symbols.FirstOrDefault(s => s.Id == sym.Id);
            Assert.NotNull(match);
            Assert.Equal(sym.ByteOffset, match.ByteOffset);
            Assert.Equal(sym.ByteLength, match.ByteLength);
            Assert.Equal(sym.ContentHash, match.ContentHash);
        }
    }

    [Fact]
    public void LoadManifest_ReturnsManifestWithoutFragments()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var manifest = _store.LoadManifest("testproj");
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.FormatVersion);
        Assert.Equal("testproj", manifest.ProjectName);
        Assert.Equal(2, manifest.Files.Count);
    }

    [Fact]
    public void LoadManifest_ReturnsNullForMissingProject()
    {
        var manifest = _store.LoadManifest("nonexistent");
        Assert.Null(manifest);
    }

    [Fact]
    public void LoadFileSymbols_ReturnsSingleFileSymbols()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var symbols = _store.LoadFileSymbols("testproj", "src/main.cs");
        Assert.NotNull(symbols);
        Assert.Single(symbols);
        Assert.Equal("src/main.cs", symbols[0].File);
    }

    [Fact]
    public void LoadFileSymbols_ReturnsNullForMissingFile()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var symbols = _store.LoadFileSymbols("testproj", "nonexistent.cs");
        Assert.Null(symbols);
    }

    [Fact]
    public void LoadAllSymbols_ReturnsSearchIndexSymbols()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var symbols = _store.LoadAllSymbols("testproj");
        Assert.NotNull(symbols);
        Assert.Equal(2, symbols.Count);

        // SearchSymbol type does not have ByteOffset/ByteLength/ContentHash fields at all
        foreach (var sym in symbols)
        {
            Assert.IsType<SearchSymbol>(sym);
        }
    }

    [Fact]
    public void LoadAllSymbols_ReturnsNullForMissingProject()
    {
        var symbols = _store.LoadAllSymbols("nonexistent");
        Assert.Null(symbols);
    }

    [Fact]
    public void Save_CleansUpOrphanedFragments()
    {
        // First save with two files
        var index1 = CreateTestIndex("testproj", ("src/main.cs", "C#"), ("src/utils.cs", "C#"));
        _store.Save(index1);

        var filesDir = StoragePaths.GetFilesDir("testproj");
        Assert.Equal(2, Directory.GetFiles(filesDir, "*.json").Length);

        // Second save with only one file
        var index2 = CreateTestIndex("testproj", ("src/main.cs", "C#"));
        _store.Save(index2);

        var remaining = Directory.GetFiles(filesDir, "*.json");
        Assert.Single(remaining);
    }

    [Fact]
    public void Load_FallsBackToLegacyIndexJson()
    {
        // Write old-style index.json manually
        var projectDir = StoragePaths.GetProjectDir("legacy");
        Directory.CreateDirectory(projectDir);

        var legacyIndex = CreateTestIndex("legacy", ("old.py", "Python"));
        var json = JsonSerializer.Serialize(legacyIndex, JsonDefaults.Options);
        File.WriteAllText(Path.Combine(projectDir, "index.json"), json);

        var loaded = _store.Load("legacy");
        Assert.NotNull(loaded);
        Assert.Equal("legacy", loaded.ProjectName);
        Assert.Single(loaded.Symbols);
    }

    [Fact]
    public void Save_DeletesLegacyFiles()
    {
        // Create legacy files first
        var projectDir = StoragePaths.GetProjectDir("testproj");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "index.json"), "{}");
        File.WriteAllText(Path.Combine(projectDir, "metadata.json"), "{}");

        var index = CreateTestIndex();
        _store.Save(index);

        Assert.False(File.Exists(Path.Combine(projectDir, "index.json")));
        Assert.False(File.Exists(Path.Combine(projectDir, "metadata.json")));
    }
}
