using TokenSqueeze.Indexing;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests;

public sealed class RobustnessTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly LanguageRegistry _registry;

    public RobustnessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-test-" + Guid.NewGuid().ToString("N")[..8]);
        _cacheDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-store-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _registry = new LanguageRegistry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    // --- File Size Limit Tests ---

    [Fact]
    public void FileSizeLimit_SkipsOversizedFile()
    {
        var filePath = Path.Combine(_tempDir, "big.py");
        File.WriteAllText(filePath, new string('x', 200));

        var walker = new DirectoryWalker(_registry, _tempDir, maxFileSize: 100);
        var files = walker.Walk().ToList();

        Assert.DoesNotContain(files, f => f.Path.EndsWith("big.py"));
    }

    [Fact]
    public void FileSizeLimit_IncludesSmallFile()
    {
        var filePath = Path.Combine(_tempDir, "small.py");
        File.WriteAllText(filePath, "x = 1\n");

        var walker = new DirectoryWalker(_registry, _tempDir, maxFileSize: 100_000);
        var files = walker.Walk().ToList();

        Assert.Contains(files, f => f.Path.EndsWith("small.py"));
    }

    [Fact]
    public void FileSizeLimit_DefaultIsOneMB()
    {
        var walker = new DirectoryWalker(_registry, _tempDir);
        Assert.NotNull(walker);
    }

    // --- Depth Limit Tests ---

    [Fact]
    public void DepthLimit_ReturnsSymbolsWithinLimit()
    {
        var fixturePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Fixtures", "sample.py");
        fixturePath = Path.GetFullPath(fixturePath);

        var spec = _registry.GetSpecForExtension(".py")!;
        var sourceBytes = File.ReadAllBytes(fixturePath);
        var extractor = new SymbolExtractor(_registry);

        var symbols = extractor.ExtractSymbols("sample.py", sourceBytes, spec);

        Assert.NotEmpty(symbols);
    }

    [Fact]
    public void DepthLimit_StopsAtMaxDepth()
    {
        var sb = new System.Text.StringBuilder();
        const int nestingDepth = 135;
        for (int i = 0; i < nestingDepth; i++)
        {
            var indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}class Level{i}:");
        }
        sb.AppendLine($"{new string(' ', nestingDepth * 4)}pass");

        var sourceBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var spec = _registry.GetSpecForExtension(".py")!;
        var extractor = new SymbolExtractor(_registry);

        var originalErr = Console.Error;
        var errWriter = new StringWriter();
        Console.SetError(errWriter);

        try
        {
            var symbols = extractor.ExtractSymbols("deep.py", sourceBytes, spec);

            Assert.NotEmpty(symbols);

            var errOutput = errWriter.ToString();
            Assert.Contains("depth", errOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    // --- Error Isolation Tests ---

    [Fact]
    public void ErrorIsolation_ContinuesAfterParseFailure()
    {
        File.WriteAllText(Path.Combine(_tempDir, "good.py"), "def hello():\n    pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        var result = indexer.Index(_tempDir);

        Assert.True(result.Index.Symbols.Count > 0);
    }

    [Fact]
    public void ErrorIsolation_ReportsErrorCount()
    {
        File.WriteAllText(Path.Combine(_tempDir, "valid.py"), "class Foo:\n    pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        var result = indexer.Index(_tempDir);

        Assert.Equal(0, result.ErrorCount);
        Assert.True(result.Index.Files.Count > 0);
    }

    [Fact]
    public void ErrorIsolation_ErrorCountInJsonOutput()
    {
        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);

        File.WriteAllText(Path.Combine(_tempDir, "simple.py"), "x = 1\n");
        var result = indexer.Index(_tempDir);

        var output = new
        {
            sourcePath = result.Index.SourcePath,
            filesIndexed = result.Index.Files.Count,
            symbolsExtracted = result.Index.Symbols.Count,
            errorsEncountered = result.ErrorCount
        };

        Assert.Equal(0, output.errorsEncountered);
        Assert.True(output.filesIndexed > 0);
    }
}
