using TokenSqueeze.Indexing;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

public sealed class SelectiveLoadTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly IndexStore _store;
    private readonly LanguageRegistry _registry;

    public SelectiveLoadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-selective-" + Guid.NewGuid().ToString("N")[..8]);
        _cacheDir = Path.Combine(Path.GetTempPath(), "ts-selective-store-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _store = new IndexStore(_cacheDir);
        _registry = new LanguageRegistry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private void CreateMultiFileProject()
    {
        var sourceDir = Path.Combine(_tempDir, "multifile");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, "greet.py"), "def greet(name):\n    return f\"Hello {name}\"\n");
        File.WriteAllText(Path.Combine(sourceDir, "calc.py"), "def add(a, b):\n    return a + b\n\ndef subtract(a, b):\n    return a - b\n");

        var indexer = new ProjectIndexer(_store, _registry);
        indexer.Index(sourceDir);
    }

    [Fact]
    public void LoadFileSymbols_ReturnsOnlyRequestedFileSymbols()
    {
        CreateMultiFileProject();

        var greetSymbols = _store.LoadFileSymbols("greet.py");
        Assert.NotNull(greetSymbols);
        Assert.Single(greetSymbols);
        Assert.Equal("greet", greetSymbols[0].Name);

        var calcSymbols = _store.LoadFileSymbols("calc.py");
        Assert.NotNull(calcSymbols);
        Assert.Equal(2, calcSymbols.Count);
    }

    [Fact]
    public void LoadAllSymbols_ReturnsAllSymbolsWithZeroByteFields()
    {
        CreateMultiFileProject();

        var allSymbols = _store.LoadAllSymbols();
        Assert.NotNull(allSymbols);
        Assert.Equal(3, allSymbols.Count); // 1 from greet.py + 2 from calc.py

        foreach (var sym in allSymbols)
        {
            Assert.IsType<SearchSymbol>(sym);
        }
    }

    [Fact]
    public void LoadManifest_ReturnsManifestWithCorrectFileCount()
    {
        CreateMultiFileProject();

        var manifest = _store.LoadManifest();
        Assert.NotNull(manifest);
        Assert.IsType<Manifest>(manifest);
        Assert.Equal(2, manifest.Files.Count);
        Assert.Equal(3, manifest.FormatVersion);
    }
}
