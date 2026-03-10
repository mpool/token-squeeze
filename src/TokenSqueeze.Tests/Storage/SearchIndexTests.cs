using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;
using TokenSqueeze.Tests.Helpers;

namespace TokenSqueeze.Tests.Storage;

[Trait("Category", "Phase3")]
public sealed class SearchIndexTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly IndexStore _store;

    public SearchIndexTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "ts-searchidx-" + Guid.NewGuid().ToString("N"));
        _store = new IndexStore(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private static CodeIndex CreateTestIndex()
    {
        var symbols = new List<Symbol>
        {
            TestIndexBuilder.MakeSymbol("MyFunction", file: "src/main.py", kind: SymbolKind.Function,
                signature: "def MyFunction(x, y)", docstring: "Adds two numbers"),
            TestIndexBuilder.MakeSymbol("MyClass", file: "src/models.py", kind: SymbolKind.Class,
                qualifiedName: "MyClass", signature: "class MyClass:", language: "Python"),
            TestIndexBuilder.MakeSymbol("helper", file: "src/utils.py", kind: SymbolKind.Function,
                signature: "def helper()", language: "Python")
        };

        var files = symbols
            .GroupBy(s => s.File)
            .ToDictionary(
                g => g.Key,
                g => new IndexedFile
                {
                    Path = g.Key,
                    Hash = "fakehash_" + g.Key.GetHashCode(),
                    Language = "Python",
                    SymbolCount = g.Count()
                });

        return new CodeIndex
        {
            SourcePath = "/tmp/searchtest",
            IndexedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Files = files,
            Symbols = symbols
        };
    }

    [Fact]
    public void SearchIndexCreatedAtIndexTime()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);
        Assert.True(File.Exists(searchIndexPath),
            "search-index.json should be created when indexing a project");
    }

    [Fact]
    public void SearchIndexContainsScoringFields()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);
        var json = File.ReadAllText(searchIndexPath);
        var symbols = JsonSerializer.Deserialize<List<Symbol>>(json, JsonDefaults.Options);

        Assert.NotNull(symbols);
        Assert.Equal(3, symbols.Count);

        foreach (var sym in symbols)
        {
            Assert.False(string.IsNullOrEmpty(sym.Name), "Name must be non-empty");
            Assert.False(string.IsNullOrEmpty(sym.File), "File must be non-empty");
            Assert.True(Enum.IsDefined(sym.Kind), "Kind must be a valid SymbolKind");
        }

        Assert.Contains(symbols, s => !string.IsNullOrEmpty(s.Signature));
    }

    [Fact]
    public void LoadAllSymbolsReadsSearchIndex_NotFragments()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var filesDir = StoragePaths.GetFilesDir(_cacheDir);
        Assert.True(Directory.Exists(filesDir), "files/ directory should exist after indexing");

        var fragmentFiles = Directory.GetFiles(filesDir, "*.json");
        Assert.True(fragmentFiles.Length > 0, "There should be fragment files to delete");

        foreach (var fragment in fragmentFiles)
        {
            File.Delete(fragment);
        }

        Assert.Empty(Directory.GetFiles(filesDir, "*.json"));

        var symbols = _store.LoadAllSymbols();

        Assert.NotNull(symbols);
        Assert.Equal(3, symbols.Count);
        Assert.Contains(symbols, s => s.Name == "MyFunction");
        Assert.Contains(symbols, s => s.Name == "MyClass");
        Assert.Contains(symbols, s => s.Name == "helper");
    }

    [Fact]
    public void FindCommandWorksWithoutFragments()
    {
        var index = CreateTestIndex();
        _store.Save(index);

        var filesDir = StoragePaths.GetFilesDir(_cacheDir);
        foreach (var fragment in Directory.GetFiles(filesDir, "*.json"))
        {
            File.Delete(fragment);
        }

        var manifest = _store.LoadManifest();
        var symbols = _store.LoadAllSymbols();

        Assert.NotNull(manifest);
        Assert.NotNull(symbols);
        Assert.True(symbols.Count > 0, "LoadAllSymbols should return symbols without fragments");

        var query = "MyFunction";
        var scored = new List<(SearchSymbol symbol, int score)>();

        foreach (var symbol in symbols)
        {
            int score = 0;
            if (string.Equals(symbol.Name, query, StringComparison.OrdinalIgnoreCase))
                score += 200;
            else if (symbol.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 100;

            if (symbol.QualifiedName.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 75;

            if (symbol.Signature.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 50;

            if (!string.IsNullOrEmpty(symbol.Docstring) &&
                symbol.Docstring.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 25;

            if (score > 0)
                scored.Add((symbol, score));
        }

        var topResults = scored.OrderByDescending(x => x.score).Take(50).ToList();

        Assert.NotEmpty(topResults);
        Assert.Equal("MyFunction", topResults[0].symbol.Name);
        Assert.True(topResults[0].score > 0, "Score should be positive for exact name match");
    }
}
