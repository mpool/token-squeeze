using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Parser;

public sealed class LanguageRegistryTests : IDisposable
{
    private readonly LanguageRegistry _registry = new();

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_SymbolNodeTypes()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.SymbolNodeTypes, tsx.SymbolNodeTypes);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_NameFields()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.NameFields, tsx.NameFields);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_ParamFields()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.ParamFields, tsx.ParamFields);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_ReturnTypeFields()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.ReturnTypeFields, tsx.ReturnTypeFields);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_DocstringStrategy()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.DocstringStrategy, tsx.DocstringStrategy);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_ContainerNodeTypes()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.ContainerNodeTypes, tsx.ContainerNodeTypes);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_ConstantPatterns()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.ConstantPatterns, tsx.ConstantPatterns);
    }

    [Fact]
    public void TypeScript_And_Tsx_Have_Identical_TypePatterns()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.Equal(ts.TypePatterns, tsx.TypePatterns);
    }

    [Fact]
    public void TypeScript_Spec_Has_Correct_Identity()
    {
        var ts = _registry.GetSpecForExtension(".ts")!;

        Assert.NotNull(ts);
        Assert.Equal("TypeScript", ts.LanguageId);
        Assert.Equal("TypeScript", ts.DisplayName);
        Assert.Equal([".ts"], ts.Extensions);
    }

    [Fact]
    public void Tsx_Spec_Has_Correct_Identity()
    {
        var tsx = _registry.GetSpecForExtension(".tsx")!;

        Assert.NotNull(tsx);
        Assert.Equal("Tsx", tsx.LanguageId);
        Assert.Equal("TypeScript (TSX)", tsx.DisplayName);
        Assert.Equal([".tsx"], tsx.Extensions);
    }

    public void Dispose() => _registry.Dispose();
}
