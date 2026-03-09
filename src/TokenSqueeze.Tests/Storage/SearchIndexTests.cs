using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;
using TokenSqueeze.Tests.Helpers;

namespace TokenSqueeze.Tests.Storage;

[Collection("CLI")]
[Trait("Category", "Phase3")]
public sealed class SearchIndexTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousOverride;
    private readonly IndexStore _store;

    public SearchIndexTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-searchidx-" + Guid.NewGuid().ToString("N"));
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

    private static CodeIndex CreateTestIndex(string projectName = "searchtest")
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
            ProjectName = projectName,
            SourcePath = "/tmp/" + projectName,
            IndexedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Files = files,
            Symbols = symbols
        };
    }

    [Fact]
    public void SearchIndexCreatedAtIndexTime()
    {
        // Arrange & Act
        var index = CreateTestIndex();
        _store.Save(index);

        // Assert: search-index.json must exist at the expected path
        var searchIndexPath = StoragePaths.GetSearchIndexPath("searchtest");
        Assert.True(File.Exists(searchIndexPath),
            "search-index.json should be created when indexing a project");
    }

    [Fact]
    public void SearchIndexContainsScoringFields()
    {
        // Arrange & Act
        var index = CreateTestIndex();
        _store.Save(index);

        // Read search-index.json directly and deserialize
        var searchIndexPath = StoragePaths.GetSearchIndexPath("searchtest");
        var json = File.ReadAllText(searchIndexPath);
        var symbols = JsonSerializer.Deserialize<List<Symbol>>(json, JsonDefaults.Options);

        // Assert: all symbols have scoring-relevant fields
        Assert.NotNull(symbols);
        Assert.Equal(3, symbols.Count);

        foreach (var sym in symbols)
        {
            Assert.False(string.IsNullOrEmpty(sym.Name), "Name must be non-empty");
            Assert.False(string.IsNullOrEmpty(sym.File), "File must be non-empty");
            Assert.True(Enum.IsDefined(sym.Kind), "Kind must be a valid SymbolKind");
        }

        // At least one symbol should have a non-empty Signature (proves scoring fields are present)
        Assert.Contains(symbols, s => !string.IsNullOrEmpty(s.Signature));
    }

    [Fact]
    public void LoadAllSymbolsReadsSearchIndex_NotFragments()
    {
        // Arrange: index a project
        var index = CreateTestIndex();
        _store.Save(index);

        // Delete ALL per-file fragment files from the files/ subdirectory
        var filesDir = StoragePaths.GetFilesDir("searchtest");
        Assert.True(Directory.Exists(filesDir), "files/ directory should exist after indexing");

        var fragmentFiles = Directory.GetFiles(filesDir, "*.json");
        Assert.True(fragmentFiles.Length > 0, "There should be fragment files to delete");

        foreach (var fragment in fragmentFiles)
        {
            File.Delete(fragment);
        }

        // Verify fragments are gone
        Assert.Empty(Directory.GetFiles(filesDir, "*.json"));

        // Act: LoadAllSymbols should still work because it reads search-index.json
        var symbols = _store.LoadAllSymbols("searchtest");

        // Assert: proves LoadAllSymbols reads search-index.json, NOT per-file fragments
        Assert.NotNull(symbols);
        Assert.Equal(3, symbols.Count);
        Assert.Contains(symbols, s => s.Name == "MyFunction");
        Assert.Contains(symbols, s => s.Name == "MyClass");
        Assert.Contains(symbols, s => s.Name == "helper");
    }

    [Fact]
    public void FindCommandWorksWithoutFragments()
    {
        // Arrange: index a project, then delete all per-file fragments
        var index = CreateTestIndex();
        _store.Save(index);

        var filesDir = StoragePaths.GetFilesDir("searchtest");
        foreach (var fragment in Directory.GetFiles(filesDir, "*.json"))
        {
            File.Delete(fragment);
        }

        // Act: replicate FindCommand's exact data loading path
        // FindCommand does: manifest = store.LoadManifest(name); symbols = store.LoadAllSymbols(name);
        var manifest = _store.LoadManifest("searchtest");
        var symbols = _store.LoadAllSymbols("searchtest");

        // Assert: both return valid data even without fragments
        Assert.NotNull(manifest);
        Assert.NotNull(symbols);
        Assert.True(symbols.Count > 0, "LoadAllSymbols should return symbols without fragments");

        // Replicate FindCommand scoring logic to prove search works end-to-end
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

        // Assert: search finds the function with correct scoring
        Assert.NotEmpty(topResults);
        Assert.Equal("MyFunction", topResults[0].symbol.Name);
        Assert.True(topResults[0].score > 0, "Score should be positive for exact name match");
    }
}
