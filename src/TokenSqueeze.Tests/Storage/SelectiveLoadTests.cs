using TokenSqueeze.Indexing;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

[Collection("CLI")]
public sealed class SelectiveLoadTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storageDir;
    private readonly string? _previousOverride;
    private readonly IndexStore _store;
    private readonly LanguageRegistry _registry;

    public SelectiveLoadTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-selective-" + Guid.NewGuid().ToString("N")[..8]);
        _storageDir = Path.Combine(Path.GetTempPath(), "ts-selective-store-" + Guid.NewGuid().ToString("N")[..8]);
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

    private string CreateMultiFileProject()
    {
        var sourceDir = Path.Combine(_tempDir, "multifile");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, "greet.py"), "def greet(name):\n    return f\"Hello {name}\"\n");
        File.WriteAllText(Path.Combine(sourceDir, "calc.py"), "def add(a, b):\n    return a + b\n\ndef subtract(a, b):\n    return a - b\n");

        var indexer = new ProjectIndexer(_store, _registry);
        indexer.Index(sourceDir, "multifile");

        return sourceDir;
    }

    [Fact]
    public void LoadFileSymbols_ReturnsOnlyRequestedFileSymbols()
    {
        CreateMultiFileProject();

        var greetSymbols = _store.LoadFileSymbols("multifile", "greet.py");
        Assert.NotNull(greetSymbols);
        Assert.Single(greetSymbols);
        Assert.Equal("greet", greetSymbols[0].Name);

        var calcSymbols = _store.LoadFileSymbols("multifile", "calc.py");
        Assert.NotNull(calcSymbols);
        Assert.Equal(2, calcSymbols.Count);
    }

    [Fact]
    public void LoadAllSymbols_ReturnsAllSymbolsWithZeroByteFields()
    {
        CreateMultiFileProject();

        var allSymbols = _store.LoadAllSymbols("multifile");
        Assert.NotNull(allSymbols);
        Assert.Equal(3, allSymbols.Count); // 1 from greet.py + 2 from calc.py

        // SearchSymbol type does not have ByteOffset/ByteLength/ContentHash fields at all
        foreach (var sym in allSymbols)
        {
            Assert.IsType<SearchSymbol>(sym);
        }
    }

    [Fact]
    public void LoadManifest_ReturnsManifestWithCorrectFileCount()
    {
        CreateMultiFileProject();

        var manifest = _store.LoadManifest("multifile");
        Assert.NotNull(manifest);
        Assert.IsType<Manifest>(manifest);
        Assert.Equal(2, manifest.Files.Count);
        Assert.Equal(2, manifest.FormatVersion);
    }
}
