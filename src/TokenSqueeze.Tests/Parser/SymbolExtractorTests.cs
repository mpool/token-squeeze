using TokenSqueeze.Models;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Parser;

public sealed class SymbolExtractorTests
{
    private static readonly string FixtureDir = Path.Combine(
        Path.GetDirectoryName(typeof(SymbolExtractorTests).Assembly.Location)!,
        "..", "..", "..", "Fixtures");

    private readonly LanguageRegistry _registry = new();
    private readonly SymbolExtractor _extractor;

    public SymbolExtractorTests()
    {
        _extractor = new SymbolExtractor(_registry);
    }

    private List<Symbol> ExtractFromFixture(string filename)
    {
        var filePath = Path.Combine(FixtureDir, filename);
        var sourceBytes = File.ReadAllBytes(filePath);
        var ext = Path.GetExtension(filename);
        var spec = _registry.GetSpecForExtension(ext)
            ?? throw new InvalidOperationException($"No spec for extension {ext}");
        return _extractor.ExtractSymbols(filePath, sourceBytes, spec);
    }

    private static void AssertContainsSymbol(List<Symbol> symbols, string name, SymbolKind kind)
    {
        Assert.Contains(symbols, s => s.Name == name && s.Kind == kind);
    }

    private static void AssertDoesNotContainKind(List<Symbol> symbols, string name, SymbolKind kind)
    {
        Assert.DoesNotContain(symbols, s => s.Name == name && s.Kind == kind);
    }

    [Fact]
    public void Python_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.py");

        AssertContainsSymbol(symbols, "greet", SymbolKind.Function);
        AssertContainsSymbol(symbols, "Calculator", SymbolKind.Class);
        AssertContainsSymbol(symbols, "add", SymbolKind.Method);
        AssertContainsSymbol(symbols, "subtract", SymbolKind.Method);
    }

    [Fact]
    public void JavaScript_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.js");

        AssertContainsSymbol(symbols, "greet", SymbolKind.Function);
        AssertContainsSymbol(symbols, "Calculator", SymbolKind.Class);
        AssertContainsSymbol(symbols, "add", SymbolKind.Method);
    }

    [Fact]
    public void TypeScript_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.ts");

        AssertContainsSymbol(symbols, "createUser", SymbolKind.Function);
        AssertContainsSymbol(symbols, "UserService", SymbolKind.Class);
        AssertContainsSymbol(symbols, "findByEmail", SymbolKind.Method);
    }

    [Fact]
    public void Tsx_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.tsx");

        AssertContainsSymbol(symbols, "Greeting", SymbolKind.Function);
    }

    [Fact]
    public void CSharp_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.cs");

        AssertContainsSymbol(symbols, "User", SymbolKind.Class);
        AssertContainsSymbol(symbols, "Greet", SymbolKind.Method);
    }

    [Fact]
    public void C_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.c");

        AssertContainsSymbol(symbols, "distance", SymbolKind.Function);
        AssertContainsSymbol(symbols, "main", SymbolKind.Function);
    }

    [Fact]
    public void Cpp_ExtractsExpectedSymbols()
    {
        var symbols = ExtractFromFixture("sample.cpp");

        AssertContainsSymbol(symbols, "Vector", SymbolKind.Class);
    }

    [Fact]
    public void C_FunctionsAreNotMethods_Bug02()
    {
        // BUG-02: C has no container types, so functions should never be promoted to Method
        var symbols = ExtractFromFixture("sample.c");

        AssertDoesNotContainKind(symbols, "distance", SymbolKind.Method);
        AssertDoesNotContainKind(symbols, "main", SymbolKind.Method);
    }

    [Fact]
    public void ClassMethods_AreMethod_NotFunction()
    {
        // Methods inside classes should be Method kind, not Function
        var pySymbols = ExtractFromFixture("sample.py");
        AssertDoesNotContainKind(pySymbols, "add", SymbolKind.Function);
        AssertContainsSymbol(pySymbols, "add", SymbolKind.Method);

        var jsSymbols = ExtractFromFixture("sample.js");
        AssertDoesNotContainKind(jsSymbols, "add", SymbolKind.Function);
        AssertContainsSymbol(jsSymbols, "add", SymbolKind.Method);
    }
}
