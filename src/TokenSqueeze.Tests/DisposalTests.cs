using System.Text;
using System.Text.RegularExpressions;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests;

public sealed class DisposalTests
{
    [Fact]
    public void ProgramCs_DisposesServiceProvider_AfterAppRun()
    {
        var programPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TokenSqueeze", "Program.cs"));
        Assert.True(File.Exists(programPath), $"Program.cs not found at {programPath}");

        var source = File.ReadAllText(programPath);

        // app.Run must appear before Dispose
        var runIndex = source.IndexOf("app.Run", StringComparison.Ordinal);
        Assert.True(runIndex >= 0, "Program.cs must call app.Run");

        var afterRun = source[runIndex..];
        Assert.True(
            afterRun.Contains("Dispose", StringComparison.Ordinal),
            "Program.cs must call Dispose after app.Run to release native tree-sitter handles");
    }

    [Fact]
    public void Disposal_AfterParsing_NoException()
    {
        var registry = new LanguageRegistry();
        var extractor = new SymbolExtractor(registry);
        var spec = registry.GetSpecForExtension(".py")!;
        extractor.ExtractSymbols("test.py", Encoding.UTF8.GetBytes("def foo(): pass"), spec);
        registry.Dispose();
        // Test passes if no exception is thrown
    }

    [Fact]
    public void Disposal_AfterMultipleLanguages_NoException()
    {
        var registry = new LanguageRegistry();
        var extractor = new SymbolExtractor(registry);

        var pySpec = registry.GetSpecForExtension(".py")!;
        extractor.ExtractSymbols("test.py", Encoding.UTF8.GetBytes("def foo(): pass"), pySpec);

        var jsSpec = registry.GetSpecForExtension(".js")!;
        extractor.ExtractSymbols("test.js", Encoding.UTF8.GetBytes("function bar() {}"), jsSpec);

        registry.Dispose();
        // Test passes if no exception is thrown
    }

    [Fact]
    public void Disposal_ThenGetOrCreateParser_ThrowsObjectDisposed()
    {
        var registry = new LanguageRegistry();
        registry.Dispose();
        Assert.Throws<ObjectDisposedException>(() => registry.GetOrCreateParser("Python"));
    }

    [Fact]
    public void Disposal_DoubleDispose_NoException()
    {
        var registry = new LanguageRegistry();
        registry.Dispose();
        registry.Dispose();
        // Test passes if no exception is thrown (idempotent disposal)
    }
}
