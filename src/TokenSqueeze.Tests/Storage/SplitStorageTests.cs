using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

public sealed class SplitStorageTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly IndexStore _store;

    public SplitStorageTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "ts-split-" + Guid.NewGuid().ToString("N"));
        _store = new IndexStore(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private static CodeIndex CreateTestIndex(params (string file, string lang)[] files)
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
            SourcePath = "/tmp/testproj",
            IndexedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Files = indexedFiles,
            Symbols = symbols
        };
    }

    [Fact]
    public void Save_CreatesManifestWithFormatVersion3()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var manifestPath = StoragePaths.GetManifestPath(_cacheDir);
        Assert.True(File.Exists(manifestPath), "manifest.json should exist");

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(json, JsonDefaults.Options);
        Assert.NotNull(manifest);
        Assert.Equal(3, manifest.FormatVersion);
        Assert.Equal(2, manifest.Files.Count);
    }

    [Fact]
    public void Save_CreatesFragmentPerSourceFile()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var filesDir = StoragePaths.GetFilesDir(_cacheDir);
        Assert.True(Directory.Exists(filesDir), "files/ directory should exist");

        var fragments = Directory.GetFiles(filesDir, "*.json");
        Assert.Equal(2, fragments.Length);
    }

    [Fact]
    public void Save_CreatesSearchIndexWithoutByteFields()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var searchPath = StoragePaths.GetSearchIndexPath(_cacheDir);
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

        var loaded = _store.Load();
        Assert.NotNull(loaded);
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

        var manifest = _store.LoadManifest();
        Assert.NotNull(manifest);
        Assert.Equal(3, manifest.FormatVersion);
        Assert.Equal(2, manifest.Files.Count);
    }

    [Fact]
    public void LoadManifest_ReturnsNullForMissingProject()
    {
        var nonexistentStore = new IndexStore(Path.Combine(_cacheDir, "nonexistent"));
        var manifest = nonexistentStore.LoadManifest();
        Assert.Null(manifest);
    }

    [Fact]
    public void LoadFileSymbols_ReturnsSingleFileSymbols()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var symbols = _store.LoadFileSymbols("src/main.cs");
        Assert.NotNull(symbols);
        Assert.Single(symbols);
        Assert.Equal("src/main.cs", symbols[0].File);
    }

    [Fact]
    public void LoadFileSymbols_ReturnsNullForMissingFile()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var symbols = _store.LoadFileSymbols("nonexistent.cs");
        Assert.Null(symbols);
    }

    [Fact]
    public void LoadAllSymbols_ReturnsSearchIndexSymbols()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var symbols = _store.LoadAllSymbols();
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
        var nonexistentStore = new IndexStore(Path.Combine(_cacheDir, "nonexistent"));
        var symbols = nonexistentStore.LoadAllSymbols();
        Assert.Null(symbols);
    }

    [Fact]
    public void Save_CleansUpOrphanedFragments()
    {
        // First save with two files
        var index1 = CreateTestIndex(("src/main.cs", "C#"), ("src/utils.cs", "C#"));
        _store.Save(index1);

        var filesDir = StoragePaths.GetFilesDir(_cacheDir);
        Assert.Equal(2, Directory.GetFiles(filesDir, "*.json").Length);

        // Second save with only one file
        var index2 = CreateTestIndex(("src/main.cs", "C#"));
        _store.Save(index2);

        var remaining = Directory.GetFiles(filesDir, "*.json");
        Assert.Single(remaining);
    }

    [Fact]
    public void Constructor_DoesNotCreateDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ts-ctor-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new IndexStore(dir);
            Assert.False(Directory.Exists(dir), "Constructor must not create cache directory");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_WritesCachedirTag()
    {
        var index = CreateTestIndex();
        _store.Save(index);
        var tagPath = Path.Combine(_cacheDir, "CACHEDIR.TAG");
        Assert.True(File.Exists(tagPath));
        var content = File.ReadAllText(tagPath);
        Assert.StartsWith("Signature: 8a477f597d28d172789f06886806bc55", content);
    }

    [Fact]
    public void Save_WritesGitignore()
    {
        var index = CreateTestIndex();
        _store.Save(index);
        var gitignorePath = Path.Combine(_cacheDir, ".gitignore");
        Assert.True(File.Exists(gitignorePath));
        Assert.Equal("*\n", File.ReadAllText(gitignorePath));
    }

    [Fact]
    public void Save_DoesNotOverwriteExistingMarkers()
    {
        Directory.CreateDirectory(_cacheDir);
        var tagPath = Path.Combine(_cacheDir, "CACHEDIR.TAG");
        var gitignorePath = Path.Combine(_cacheDir, ".gitignore");
        File.WriteAllText(tagPath, "custom content");
        File.WriteAllText(gitignorePath, "custom ignore");

        var index = CreateTestIndex();
        _store.Save(index);

        Assert.Equal("custom content", File.ReadAllText(tagPath));
        Assert.Equal("custom ignore", File.ReadAllText(gitignorePath));
    }
}
