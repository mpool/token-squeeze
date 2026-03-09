using System.Text;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests;

public sealed class OutlineHierarchyTests : IDisposable
{
    private readonly LanguageRegistry _registry = new();
    private readonly SymbolExtractor _extractor;

    public OutlineHierarchyTests()
    {
        _extractor = new SymbolExtractor(_registry);
    }

    public void Dispose() => _registry.Dispose();

    private List<Symbol> ExtractFromSource(string source, string extension, string fakePath = "test.py")
    {
        var spec = _registry.GetSpecForExtension(extension)
            ?? throw new InvalidOperationException($"No spec for extension {extension}");
        var bytes = Encoding.UTF8.GetBytes(source);
        return _extractor.ExtractSymbols(fakePath, bytes, spec);
    }

    [Fact]
    public void OutlineHierarchy_NestedClassMethod_ShowsChildren()
    {
        var source = "class Foo:\n    def bar(self):\n        pass\n";
        var symbols = ExtractFromSource(source, ".py");

        var fooSymbol = symbols.Single(s => s.Name == "Foo" && s.Kind == SymbolKind.Class);
        var barSymbol = symbols.Single(s => s.Name == "bar" && s.Kind == SymbolKind.Method);

        // bar's Parent should equal Foo's QualifiedName
        Assert.Equal(fooSymbol.QualifiedName, barSymbol.Parent);
    }

    [Fact]
    public void OutlineHierarchy_TopLevelFunction_NoParent()
    {
        var source = "def standalone():\n    pass\n";
        var symbols = ExtractFromSource(source, ".py");

        var fn = symbols.Single(s => s.Name == "standalone");
        Assert.Null(fn.Parent);
    }

    [Fact]
    public void OutlineHierarchy_ParentUsesQualifiedName_NotId()
    {
        var source = "class Foo:\n    def bar(self):\n        pass\n";
        var symbols = ExtractFromSource(source, ".py");

        var barSymbol = symbols.Single(s => s.Name == "bar" && s.Kind == SymbolKind.Method);

        // Parent must be QualifiedName (e.g., "Foo"), NOT Id (e.g., "test.py::Foo#Class")
        Assert.NotNull(barSymbol.Parent);
        Assert.DoesNotContain("::", barSymbol.Parent);
        Assert.DoesNotContain("#", barSymbol.Parent);
    }

    [Fact]
    public void OutlineHierarchy_IdFormatChange_DoesNotBreakHierarchy()
    {
        var source = "class Foo:\n    def bar(self):\n        pass\n";
        var symbols = ExtractFromSource(source, ".py");

        var fooSymbol = symbols.Single(s => s.Name == "Foo" && s.Kind == SymbolKind.Class);
        var barSymbol = symbols.Single(s => s.Name == "bar" && s.Kind == SymbolKind.Method);

        // Parent references QualifiedName which is format-independent
        // Even if MakeId format changed, hierarchy would still work
        Assert.Equal("Foo", fooSymbol.QualifiedName);
        Assert.Equal("Foo.bar", barSymbol.QualifiedName);
        Assert.Equal(fooSymbol.QualifiedName, barSymbol.Parent);

        // Verify the outline grouping logic works with QualifiedName-based Parent
        var roots = symbols.Where(s => s.Parent is null).ToList();
        var childrenByParent = symbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        Assert.Single(roots);
        Assert.Equal("Foo", roots[0].Name);

        // Lookup children by QualifiedName (the new approach)
        Assert.True(childrenByParent.TryGetValue(roots[0].QualifiedName, out var kids));
        Assert.Single(kids);
        Assert.Equal("bar", kids[0].Name);
    }

    [Fact]
    public void OutlineHierarchy_CSharpNestedClass_ShowsChildren()
    {
        var source = @"namespace MyApp
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";
        var symbols = ExtractFromSource(source, ".cs", "test.cs");

        var calcSymbol = symbols.Single(s => s.Name == "Calculator" && s.Kind == SymbolKind.Class);
        var addSymbol = symbols.Single(s => s.Name == "Add" && s.Kind == SymbolKind.Method);

        // Add's Parent should be Calculator's QualifiedName
        Assert.Equal(calcSymbol.QualifiedName, addSymbol.Parent);
        Assert.DoesNotContain("::", addSymbol.Parent);
        Assert.DoesNotContain("#", addSymbol.Parent);
    }
}
