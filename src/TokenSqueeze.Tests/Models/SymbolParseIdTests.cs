using TokenSqueeze.Models;

namespace TokenSqueeze.Tests.Models;

[Collection("CLI")]
public sealed class SymbolParseIdTests
{
    [Fact]
    public void ParseId_FullFormat_ReturnsAllParts()
    {
        var result = Symbol.ParseId("path/to/file.cs::MyClass.MyMethod#Method");

        Assert.NotNull(result);
        Assert.Equal("path/to/file.cs", result.Value.FilePath);
        Assert.Equal("MyClass.MyMethod", result.Value.QualifiedName);
        Assert.Equal("Method", result.Value.Kind);
    }

    [Fact]
    public void ParseId_SimpleFormat_ReturnsAllParts()
    {
        var result = Symbol.ParseId("file.py::func#Function");

        Assert.NotNull(result);
        Assert.Equal("file.py", result.Value.FilePath);
        Assert.Equal("func", result.Value.QualifiedName);
        Assert.Equal("Function", result.Value.Kind);
    }

    [Fact]
    public void ParseId_NoSeparator_ReturnsNull()
    {
        var result = Symbol.ParseId("invalid-no-separator");

        Assert.Null(result);
    }

    [Fact]
    public void ParseId_NoHashKind_ReturnsEmptyKind()
    {
        var result = Symbol.ParseId("path::name");

        Assert.NotNull(result);
        Assert.Equal("path", result.Value.FilePath);
        Assert.Equal("name", result.Value.QualifiedName);
        Assert.Equal("", result.Value.Kind);
    }

    [Fact]
    public void ParseId_RoundtripsWithMakeId()
    {
        var id = Symbol.MakeId("path", "name", SymbolKind.Method);
        var parsed = Symbol.ParseId(id);

        Assert.NotNull(parsed);
        Assert.Equal("path", parsed.Value.FilePath);
        Assert.Equal("name", parsed.Value.QualifiedName);
        Assert.Equal("Method", parsed.Value.Kind);
    }

    [Fact]
    public void ParseId_ExtractsFilePath_UsedByExtractCommandGrouping()
    {
        var id = Symbol.MakeId("src/Controllers/HomeController.cs", "HomeController.Index", SymbolKind.Method);
        var parsed = Symbol.ParseId(id);

        Assert.NotNull(parsed);
        Assert.Equal("src/Controllers/HomeController.cs", parsed.Value.FilePath);
    }
}
