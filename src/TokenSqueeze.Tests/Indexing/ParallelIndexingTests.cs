using System.Text.Json;
using TokenSqueeze.Indexing;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Indexing;

[Trait("Category", "Phase3")]
public sealed class ParallelIndexingTests : IDisposable
{
    private readonly LanguageRegistry _registry;

    public ParallelIndexingTests()
    {
        _registry = new LanguageRegistry();
    }

    public void Dispose()
    {
        _registry.Dispose();
    }

    [Fact]
    public void Index_Deterministic_ProducesSameOutputAcrossRuns()
    {
        var fixturesDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));

        var jsonOutputs = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            var runCacheDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-det-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(runCacheDir);

            try
            {
                using var registry = new LanguageRegistry();
                var store = new IndexStore(runCacheDir);
                var indexer = new ProjectIndexer(store, registry);

                var result = indexer.Index(fixturesDir);

                var symbolData = result.Index.Symbols.Select(s => new
                {
                    s.Id,
                    s.File,
                    s.Name,
                    s.QualifiedName,
                    s.Kind,
                    s.Language
                }).ToList();

                var json = JsonSerializer.Serialize(symbolData, JsonDefaults.Options);
                jsonOutputs.Add(json);
            }
            finally
            {
                if (Directory.Exists(runCacheDir))
                    Directory.Delete(runCacheDir, recursive: true);
            }
        }

        for (var i = 1; i < jsonOutputs.Count; i++)
        {
            Assert.Equal(jsonOutputs[0], jsonOutputs[i]);
        }
    }

    [Fact]
    public void Index_ErrorIsolation_UnparseableFileDoesNotBlockOtherFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-errisolation-" + Guid.NewGuid().ToString("N")[..8]);
        var cacheDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-errisolation-cache-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(cacheDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "valid.py"), "def hello():\n    return 42\n\nclass MyClass:\n    pass\n");

            File.WriteAllText(Path.Combine(tempDir, "nonsense.py"),
                "!@#$%^&*() +=== {{{}}} <<<>>> ~~~ ??? ;;;;\n!@#$%^&*() +=== {{{}}} <<<>>> ~~~ ??? ;;;;\n");

            var store = new IndexStore(cacheDir);
            var indexer = new ProjectIndexer(store, _registry);

            var result = indexer.Index(tempDir);

            Assert.Contains(result.Index.Symbols, s => s.Name == "hello");
            Assert.Contains(result.Index.Symbols, s => s.Name == "MyClass");

            Assert.True(result.Index.Files.Count >= 2,
                $"Expected at least 2 indexed files, got {result.Index.Files.Count}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void Index_MultiLanguage_ExtractsSymbolsFromMultipleLanguages()
    {
        var fixturesDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));

        var cacheDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-multilang-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(cacheDir);

        try
        {
            var store = new IndexStore(cacheDir);
            var indexer = new ProjectIndexer(store, _registry);

            var result = indexer.Index(fixturesDir);

            var distinctLanguages = result.Index.Symbols
                .Select(s => s.Language)
                .Distinct()
                .ToList();

            Assert.True(distinctLanguages.Count >= 3,
                $"Expected at least 3 distinct languages, got {distinctLanguages.Count}: [{string.Join(", ", distinctLanguages)}]");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }
}
