using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;
using TokenSqueeze.Tests.Helpers;

namespace TokenSqueeze.Tests.Models;

[Collection("CLI")]
[Trait("Category", "Phase2")]
public sealed class SearchSymbolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousOverride;
    private readonly IndexStore _store;

    public SearchSymbolTests()
    {
        _previousOverride = StoragePaths.TestRootOverride;
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-searchsym-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void SearchSymbolHasNoByteOffsetField()
    {
        // SearchSymbol record must not have ByteOffset, ByteLength, or ContentHash
        var type = typeof(SearchSymbol);
        Assert.Null(type.GetProperty("ByteOffset"));
        Assert.Null(type.GetProperty("ByteLength"));
        Assert.Null(type.GetProperty("ContentHash"));
    }

    [Fact]
    public void SearchSymbolHasAllScoringFields()
    {
        var type = typeof(SearchSymbol);
        Assert.NotNull(type.GetProperty("Id"));
        Assert.NotNull(type.GetProperty("File"));
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("QualifiedName"));
        Assert.NotNull(type.GetProperty("Kind"));
        Assert.NotNull(type.GetProperty("Language"));
        Assert.NotNull(type.GetProperty("Signature"));
        Assert.NotNull(type.GetProperty("Docstring"));
        Assert.NotNull(type.GetProperty("Parent"));
        Assert.NotNull(type.GetProperty("Line"));
        Assert.NotNull(type.GetProperty("EndLine"));
    }

    [Fact]
    public void SymbolToSearchSymbolMapsAllFields()
    {
        var symbol = TestIndexBuilder.MakeSymbol("Foo", file: "a.py", kind: SymbolKind.Function,
            signature: "def Foo()", docstring: "does stuff");

        var search = symbol.ToSearchSymbol();

        Assert.Equal(symbol.Id, search.Id);
        Assert.Equal(symbol.File, search.File);
        Assert.Equal(symbol.Name, search.Name);
        Assert.Equal(symbol.QualifiedName, search.QualifiedName);
        Assert.Equal(symbol.Kind, search.Kind);
        Assert.Equal(symbol.Language, search.Language);
        Assert.Equal(symbol.Signature, search.Signature);
        Assert.Equal(symbol.Docstring, search.Docstring);
        Assert.Equal(symbol.Parent, search.Parent);
        Assert.Equal(symbol.Line, search.Line);
        Assert.Equal(symbol.EndLine, search.EndLine);
    }

    [Fact]
    public void LoadAllSymbolsReturnsSearchSymbolType()
    {
        var index = TestIndexBuilder.Create("searchsymtest", "/tmp/searchsymtest",
            TestIndexBuilder.MakeSymbol("Bar", file: "b.py"));
        _store.Save(index);

        var result = _store.LoadAllSymbols("searchsymtest");

        Assert.NotNull(result);
        Assert.IsType<List<SearchSymbol>>(result);
        Assert.Single(result);
        Assert.Equal("Bar", result[0].Name);
    }

    [Fact]
    public void SearchIndexJsonLacksByteFields()
    {
        var index = TestIndexBuilder.Create("searchjsontest", "/tmp/searchjsontest",
            TestIndexBuilder.MakeSymbol("Baz", file: "c.py"));
        _store.Save(index);

        var searchIndexPath = StoragePaths.GetSearchIndexPath("searchjsontest");
        var json = File.ReadAllText(searchIndexPath);

        // The JSON should NOT contain byteOffset or byteLength or contentHash
        Assert.DoesNotContain("byteOffset", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("byteLength", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contentHash", json, StringComparison.OrdinalIgnoreCase);

        // But should contain scoring fields
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"signature\"", json);
    }
}
