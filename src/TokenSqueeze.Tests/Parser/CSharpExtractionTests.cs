using TokenSqueeze.Models;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Parser;

public sealed class CSharpExtractionTests : IDisposable
{
    private static readonly string FixtureDir = Path.Combine(
        Path.GetDirectoryName(typeof(CSharpExtractionTests).Assembly.Location)!,
        "..", "..", "..", "Fixtures");

    private readonly LanguageRegistry _registry = new();
    private readonly SymbolExtractor _extractor;
    private readonly List<Symbol> _symbols;

    public CSharpExtractionTests()
    {
        _extractor = new SymbolExtractor(_registry);
        var filePath = Path.Combine(FixtureDir, "sample-advanced.cs");
        var sourceBytes = File.ReadAllBytes(filePath);
        var spec = _registry.GetSpecForExtension(".cs")!;
        _symbols = _extractor.ExtractSymbols(filePath, sourceBytes, spec);
    }

    [Fact]
    public void Struct_ExtractedAsClass()
    {
        // struct_declaration is in SymbolNodeTypes as Class and in ContainerNodeTypes
        Assert.Contains(_symbols, s => s.Name == "PointStruct" && s.Kind == SymbolKind.Class);
    }

    [Fact]
    public void StructMethod_ExtractedWithParent()
    {
        // struct_declaration is a container, so methods inside are discovered
        var distance = Assert.Single(_symbols, s => s.Name == "Distance");
        Assert.Equal(SymbolKind.Method, distance.Kind);
        Assert.NotNull(distance.Parent);
        Assert.Contains("PointStruct", distance.Parent);
    }

    [Fact]
    public void Record_ExtractedAsClass()
    {
        // record_declaration is in SymbolNodeTypes as Class
        Assert.Contains(_symbols, s => s.Name == "UserRecord" && s.Kind == SymbolKind.Class);
    }

    [Fact]
    public void RecordMethod_ExtractedWithParent()
    {
        // record_declaration is in ContainerNodeTypes, so methods are found via recursion
        var validate = Assert.Single(_symbols, s => s.Name == "Validate");
        Assert.Equal(SymbolKind.Method, validate.Kind);
        Assert.NotNull(validate.Parent);
        Assert.Contains("UserRecord", validate.Parent);
    }

    [Fact]
    public void Delegate_ExtractedAsType()
    {
        // delegate_declaration is in SymbolNodeTypes as Type
        Assert.Contains(_symbols, s => s.Name == "EventCallback" && s.Kind == SymbolKind.Type);
    }

    [Fact]
    public void ClassContainer_ExtractedAsClass()
    {
        Assert.Contains(_symbols, s => s.Name == "Container" && s.Kind == SymbolKind.Class);
    }

    [Fact]
    public void ClassMethod_ExtractedWithParent()
    {
        var process = Assert.Single(_symbols, s => s.Name == "Process");
        Assert.Equal(SymbolKind.Method, process.Kind);
        Assert.NotNull(process.Parent);
        Assert.Contains("Container", process.Parent);
    }

    [Fact]
    public void NestedClass_OuterExtracted()
    {
        Assert.Contains(_symbols, s => s.Name == "Outer" && s.Kind == SymbolKind.Class);
    }

    [Fact]
    public void NestedClass_InnerExtracted()
    {
        Assert.Contains(_symbols, s => s.Name == "Inner" && s.Kind == SymbolKind.Class);
    }

    [Fact]
    public void NestedClass_InnerMethodExtracted()
    {
        var innerMethod = Assert.Single(_symbols, s => s.Name == "InnerMethod");
        Assert.Equal(SymbolKind.Method, innerMethod.Kind);
        Assert.NotNull(innerMethod.Parent);
        Assert.Contains("Inner", innerMethod.Parent);
    }

    public void Dispose()
    {
        _registry.Dispose();
    }
}
