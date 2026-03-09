using System.Text.Json;
using TokenSqueeze.Indexing;
using TokenSqueeze.Infrastructure;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Indexing;

[Collection("CLI")]
[Trait("Category", "Phase3")]
public sealed class ParallelIndexingTests : IDisposable
{
    private readonly string _storageDir;
    private readonly LanguageRegistry _registry;

    public ParallelIndexingTests()
    {
        _storageDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-parallel-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_storageDir);
        _registry = new LanguageRegistry();
        StoragePaths.TestRootOverride = _storageDir;
    }

    public void Dispose()
    {
        StoragePaths.TestRootOverride = null;
        _registry.Dispose();
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }

    [Fact]
    public void Index_Deterministic_ProducesSameOutputAcrossRuns()
    {
        var fixturesDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));

        var jsonOutputs = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            // Fresh storage for each run to avoid incremental skip
            var runStorageDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-det-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(runStorageDir);
            StoragePaths.TestRootOverride = runStorageDir;

            try
            {
                using var registry = new LanguageRegistry();
                var store = new IndexStore();
                var indexer = new ProjectIndexer(store, registry);

                var result = indexer.Index(fixturesDir, "determinism-test");

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
                if (Directory.Exists(runStorageDir))
                    Directory.Delete(runStorageDir, recursive: true);
            }
        }

        // Restore storage dir for Dispose
        StoragePaths.TestRootOverride = _storageDir;

        // All 5 runs must produce identical symbol output
        for (var i = 1; i < jsonOutputs.Count; i++)
        {
            Assert.Equal(jsonOutputs[0], jsonOutputs[i]);
        }
    }

    [Fact]
    public void Index_ErrorIsolation_UnparseableFileDoesNotBlockOtherFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-errisolation-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Valid Python file with clear symbols
            File.WriteAllText(Path.Combine(tempDir, "valid.py"), "def hello():\n    return 42\n\nclass MyClass:\n    pass\n");

            // Syntactically nonsensical Python (no null bytes so it passes binary filter,
            // but produces zero extractable symbols). This verifies that a file yielding
            // no symbols does not interfere with other files being indexed.
            File.WriteAllText(Path.Combine(tempDir, "nonsense.py"),
                "!@#$%^&*() +=== {{{}}} <<<>>> ~~~ ??? ;;;;\n!@#$%^&*() +=== {{{}}} <<<>>> ~~~ ??? ;;;;\n");

            var store = new IndexStore();
            var indexer = new ProjectIndexer(store, _registry);

            var result = indexer.Index(tempDir, "error-isolation-test");

            // Valid file symbols must be present regardless of the nonsense file
            Assert.Contains(result.Index.Symbols, s => s.Name == "hello");
            Assert.Contains(result.Index.Symbols, s => s.Name == "MyClass");

            // Both files should be indexed (appear in Files dictionary)
            Assert.True(result.Index.Files.Count >= 2,
                $"Expected at least 2 indexed files, got {result.Index.Files.Count}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Index_MultiLanguage_ExtractsSymbolsFromMultipleLanguages()
    {
        var fixturesDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);

        var result = indexer.Index(fixturesDir, "multilang-test");

        var distinctLanguages = result.Index.Symbols
            .Select(s => s.Language)
            .Distinct()
            .ToList();

        Assert.True(distinctLanguages.Count >= 3,
            $"Expected at least 3 distinct languages, got {distinctLanguages.Count}: [{string.Join(", ", distinctLanguages)}]");
    }
}
