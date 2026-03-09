using System.Text;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Parser;

public sealed class SymbolExtractorEdgeCaseTests : IDisposable
{
    private readonly LanguageRegistry _registry = new();
    private readonly SymbolExtractor _extractor;

    public SymbolExtractorEdgeCaseTests()
    {
        _extractor = new SymbolExtractor(_registry);
    }

    [Theory]
    [InlineData(".py")]
    [InlineData(".js")]
    [InlineData(".cs")]
    public void EmptyFile_ReturnsNoSymbols(string ext)
    {
        var spec = _registry.GetSpecForExtension(ext)!;
        var symbols = _extractor.ExtractSymbols($"empty{ext}", Array.Empty<byte>(), spec);
        Assert.Empty(symbols);
    }

    [Fact]
    public void SyntaxError_ExtractsValidSymbols()
    {
        var source = "def valid():\n    pass\n\ndef broken(";
        var bytes = Encoding.UTF8.GetBytes(source);
        var spec = _registry.GetSpecForExtension(".py")!;

        var symbols = _extractor.ExtractSymbols("broken.py", bytes, spec);

        Assert.Contains(symbols, s => s.Name == "valid" && s.Kind == SymbolKind.Function);
    }

    [Fact]
    public void CommentOnlyFile_ReturnsNoSymbols()
    {
        var source = "# just a comment\n# another comment\n";
        var bytes = Encoding.UTF8.GetBytes(source);
        var spec = _registry.GetSpecForExtension(".py")!;

        var symbols = _extractor.ExtractSymbols("comments.py", bytes, spec);

        Assert.Empty(symbols);
    }

    [Fact]
    public void UnicodeIdentifier_ExtractedCorrectly()
    {
        var source = "def greet_\u00e9l\u00e8ve():\n    pass\n";
        var bytes = Encoding.UTF8.GetBytes(source);
        var spec = _registry.GetSpecForExtension(".py")!;

        var symbols = _extractor.ExtractSymbols("unicode.py", bytes, spec);

        Assert.Single(symbols);
        Assert.Equal("greet_\u00e9l\u00e8ve", symbols[0].Name);
    }

    [Fact]
    public void DeeplyNested_ExtractsAllLevels()
    {
        var source = "class Outer {\n  innerMethod() {\n  }\n}\n";
        var bytes = Encoding.UTF8.GetBytes(source);
        var spec = _registry.GetSpecForExtension(".js")!;

        var symbols = _extractor.ExtractSymbols("nested.js", bytes, spec);

        Assert.Contains(symbols, s => s.Name == "Outer" && s.Kind == SymbolKind.Class);
        Assert.Contains(symbols, s => s.Name == "innerMethod" && s.Kind == SymbolKind.Method);
    }

    public void Dispose()
    {
        _registry.Dispose();
    }
}
