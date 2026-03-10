using TokenSqueeze.Indexing;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests;

public sealed class ReindexOnQueryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly LanguageRegistry _registry;

    public ReindexOnQueryTests()
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

    [Fact]
    public void ReindexSingleFile_UpdatesFragmentAndSearchIndex()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var originalSymbols = store.LoadFileSymbols("hello.py");
        Assert.NotNull(originalSymbols);
        Assert.Contains(originalSymbols, s => s.Name == "hello");

        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def goodbye(): pass\n");

        var manifest = store.LoadManifest()!;
        var stalenessResult = StalenessChecker.DetectStaleFiles(manifest, _registry);
        Assert.Single(stalenessResult.StaleFiles);

        var reindexer = new IncrementalReindexer(store, _registry);
        reindexer.ReindexFiles(manifest, stalenessResult);

        var updatedSymbols = store.LoadFileSymbols("hello.py");
        Assert.NotNull(updatedSymbols);
        Assert.Contains(updatedSymbols, s => s.Name == "goodbye");
        Assert.DoesNotContain(updatedSymbols, s => s.Name == "hello");

        var allSymbols = store.LoadAllSymbols();
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "goodbye");
        Assert.DoesNotContain(allSymbols, s => s.Name == "hello");
    }

    [Fact]
    public void ReindexBounded_CapsAtThreshold()
    {
        for (int i = 0; i < 60; i++)
        {
            var file = Path.Combine(_tempDir, $"file_{i}.py");
            File.WriteAllText(file, $"x_{i} = {i}\n");
        }

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        Thread.Sleep(50);
        for (int i = 0; i < 60; i++)
        {
            var file = Path.Combine(_tempDir, $"file_{i}.py");
            File.WriteAllText(file, $"y_{i} = {i + 100}\n");
        }

        var manifest = store.LoadManifest()!;
        var stalenessResult = StalenessChecker.DetectStaleFiles(manifest, _registry);
        Assert.Equal(IncrementalReindexer.MaxReindexPerQuery, stalenessResult.StaleFiles.Count);

        var reindexer = new IncrementalReindexer(store, _registry);
        reindexer.ReindexFiles(manifest, stalenessResult);
    }

    [Fact]
    public void EnsureFresh_FindPipeline_ReturnsUpdatedSymbols()
    {
        var pyFile = Path.Combine(_tempDir, "test.py");
        File.WriteAllText(pyFile, "def original(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var allSymbols = store.LoadAllSymbols();
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "original");

        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def updated(): pass\n");

        var manifest = QueryReindexer.EnsureFresh(store, _registry);
        Assert.NotNull(manifest);

        var refreshedSymbols = store.LoadAllSymbols();
        Assert.NotNull(refreshedSymbols);
        Assert.Contains(refreshedSymbols, s => s.Name == "updated");
        Assert.DoesNotContain(refreshedSymbols, s => s.Name == "original");
    }

    [Fact]
    public void EnsureFresh_OutlinePipeline_ReturnsUpdatedSymbols()
    {
        var pyFile = Path.Combine(_tempDir, "test.py");
        File.WriteAllText(pyFile, "def original(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def updated(): pass\n");

        var manifest = QueryReindexer.EnsureFresh(store, _registry);
        Assert.NotNull(manifest);

        var fileSymbols = store.LoadFileSymbols("test.py");
        Assert.NotNull(fileSymbols);
        Assert.Contains(fileSymbols, s => s.Name == "updated");
        Assert.DoesNotContain(fileSymbols, s => s.Name == "original");
    }

    [Fact]
    public void EnsureFresh_ExtractPipeline_ReturnsUpdatedSource()
    {
        var pyFile = Path.Combine(_tempDir, "test.py");
        File.WriteAllText(pyFile, "def original(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def updated(): pass\n");

        var manifest = QueryReindexer.EnsureFresh(store, _registry);
        Assert.NotNull(manifest);

        var fileSymbols = store.LoadFileSymbols("test.py");
        Assert.NotNull(fileSymbols);
        var updatedSymbol = fileSymbols.FirstOrDefault(s => s.Name == "updated");
        Assert.NotNull(updatedSymbol);

        var fileBytes = File.ReadAllBytes(pyFile);
        var source = System.Text.Encoding.UTF8.GetString(
            fileBytes, updatedSymbol.ByteOffset, updatedSymbol.ByteLength);
        Assert.Contains("def updated", source);
    }

    [Fact]
    public void EnsureFresh_ReturnsNull_ForUnknownProject()
    {
        var store = new IndexStore(Path.Combine(_cacheDir, "nonexistent"));
        var manifest = QueryReindexer.EnsureFresh(store, _registry);
        Assert.Null(manifest);
    }

    [Fact]
    public void ReindexPreservesUnchangedFiles()
    {
        var file1 = Path.Combine(_tempDir, "keep.py");
        var file2 = Path.Combine(_tempDir, "change.py");
        File.WriteAllText(file1, "def keep(): pass\n");
        File.WriteAllText(file2, "def change(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var originalKeepSymbols = store.LoadFileSymbols("keep.py");
        Assert.NotNull(originalKeepSymbols);

        Thread.Sleep(50);
        File.WriteAllText(file2, "def changed(): pass\n");

        var manifest = store.LoadManifest()!;
        var stalenessResult = StalenessChecker.DetectStaleFiles(manifest, _registry);
        Assert.Single(stalenessResult.StaleFiles);
        Assert.Contains("change.py", stalenessResult.StaleFiles[0]);

        var reindexer = new IncrementalReindexer(store, _registry);
        var updated = reindexer.ReindexFiles(manifest, stalenessResult);

        var keepSymbols = store.LoadFileSymbols("keep.py");
        Assert.NotNull(keepSymbols);
        Assert.Contains(keepSymbols, s => s.Name == "keep");

        var changeSymbols = store.LoadFileSymbols("change.py");
        Assert.NotNull(changeSymbols);
        Assert.Contains(changeSymbols, s => s.Name == "changed");
        Assert.DoesNotContain(changeSymbols, s => s.Name == "change");

        Assert.True(updated.Files.ContainsKey("keep.py"));
        Assert.Equal("Python", updated.Files["keep.py"].Language);
    }

    [Fact]
    public void DeletedFile_PurgedFromResults()
    {
        var file1 = Path.Combine(_tempDir, "keep.py");
        var file2 = Path.Combine(_tempDir, "remove.py");
        File.WriteAllText(file1, "def keep(): pass\n");
        File.WriteAllText(file2, "def remove(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var allSymbols = store.LoadAllSymbols();
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "keep");
        Assert.Contains(allSymbols, s => s.Name == "remove");

        File.Delete(file2);

        var manifest = QueryReindexer.EnsureFresh(store, _registry);
        Assert.NotNull(manifest);

        var refreshedSymbols = store.LoadAllSymbols();
        Assert.NotNull(refreshedSymbols);
        Assert.Contains(refreshedSymbols, s => s.Name == "keep");
        Assert.DoesNotContain(refreshedSymbols, s => s.Name == "remove");

        var deletedSymbols = store.LoadFileSymbols("remove.py");
        Assert.Null(deletedSymbols);

        Assert.False(manifest.Files.ContainsKey("remove.py"));
        Assert.True(manifest.Files.ContainsKey("keep.py"));
    }

    [Fact]
    public void DeletedFile_FragmentRemovedFromDisk()
    {
        var pyFile = Path.Combine(_tempDir, "target.py");
        File.WriteAllText(pyFile, "def target(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var manifest = store.LoadManifest()!;
        var storageKey = manifest.Files["target.py"].StorageKey;
        var fragmentPath = StoragePaths.GetFileFragmentPath(_cacheDir, storageKey);
        Assert.True(File.Exists(fragmentPath));

        File.Delete(pyFile);

        QueryReindexer.EnsureFresh(store, _registry);

        Assert.False(File.Exists(fragmentPath));
    }

    [Fact]
    public void NewFile_DiscoveredAndIndexed()
    {
        var file1 = Path.Combine(_tempDir, "original.py");
        File.WriteAllText(file1, "def original(): pass\n");

        var store = new IndexStore(_cacheDir);
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var file2 = Path.Combine(_tempDir, "added.py");
        File.WriteAllText(file2, "def added(): pass\n");

        var manifest = QueryReindexer.EnsureFresh(store, _registry);
        Assert.NotNull(manifest);

        var allSymbols = store.LoadAllSymbols();
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "original");
        Assert.Contains(allSymbols, s => s.Name == "added");

        var newFileSymbols = store.LoadFileSymbols("added.py");
        Assert.NotNull(newFileSymbols);
        Assert.Contains(newFileSymbols, s => s.Name == "added");

        Assert.True(manifest.Files.ContainsKey("added.py"));
    }
}
