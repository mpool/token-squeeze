using System.Text.Json;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Models;
using TokenSqueeze.Storage;
using TokenSqueeze.Tests.Helpers;

namespace TokenSqueeze.Tests.Models;

[Trait("Category", "Phase2")]
public sealed class SearchSymbolTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly IndexStore _store;

    public SearchSymbolTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "ts-searchsym-" + Guid.NewGuid().ToString("N"));
        _store = new IndexStore(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public void SearchSymbolHasNoByteOffsetField()
    {
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
        var index = TestIndexBuilder.Create("/tmp/searchsymtest",
            TestIndexBuilder.MakeSymbol("Bar", file: "b.py"));
        _store.Save(index);

        var result = _store.LoadAllSymbols();

        Assert.NotNull(result);
        Assert.IsType<List<SearchSymbol>>(result);
        Assert.Single(result);
        Assert.Equal("Bar", result[0].Name);
    }

    [Fact]
    public void SearchIndexJsonLacksByteFields()
    {
        var index = TestIndexBuilder.Create("/tmp/searchjsontest",
            TestIndexBuilder.MakeSymbol("Baz", file: "c.py"));
        _store.Save(index);

        var searchIndexPath = StoragePaths.GetSearchIndexPath(_cacheDir);
        var json = File.ReadAllText(searchIndexPath);

        Assert.DoesNotContain("byteOffset", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("byteLength", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contentHash", json, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("\"name\"", json);
        Assert.Contains("\"signature\"", json);
    }
}
