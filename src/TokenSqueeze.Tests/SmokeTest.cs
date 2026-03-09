using Xunit;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests;

public sealed class SmokeTest
{
    [Fact]
    public void SymbolExtractor_CanBeInstantiated()
    {
        // Proves: test project references main project,
        // can access public types, and xUnit runs correctly
        using var registry = new LanguageRegistry();
        var extractor = new SymbolExtractor(registry);
        Assert.NotNull(extractor);
    }

    [Fact]
    public void LanguageRegistry_LoadsPython()
    {
        // Proves: TreeSitter native libraries load correctly in test context
        using var registry = new LanguageRegistry();
        var spec = registry.GetSpecForExtension(".py");
        Assert.NotNull(spec);
        Assert.Equal("Python", spec.LanguageId);
    }
}
