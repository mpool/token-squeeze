using TokenSqueeze.Indexing;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests;

[Collection("CLI")]
public sealed class ReindexOnQueryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storageDir;
    private readonly LanguageRegistry _registry;

    public ReindexOnQueryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-test-" + Guid.NewGuid().ToString("N")[..8]);
        _storageDir = Path.Combine(Path.GetTempPath(), "tokensqueeze-store-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_storageDir);
        _registry = new LanguageRegistry();
        StoragePaths.TestRootOverride = _storageDir;
    }

    public void Dispose()
    {
        StoragePaths.TestRootOverride = null;
        _registry.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        if (Directory.Exists(_storageDir))
            Directory.Delete(_storageDir, recursive: true);
    }

    [Fact]
    public void ReindexSingleFile_UpdatesFragmentAndSearchIndex()
    {
        // Create and index a .py file
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var indexResult = indexer.Index(_tempDir);
        var projectName = indexResult.Index.ProjectName;

        // Verify original symbols
        var originalSymbols = store.LoadFileSymbols(projectName, "hello.py");
        Assert.NotNull(originalSymbols);
        Assert.Contains(originalSymbols, s => s.Name == "hello");

        // Modify the file
        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def goodbye(): pass\n");

        // Detect stale and reindex
        var manifest = store.LoadManifest(projectName)!;
        var stalenessResult = StalenessChecker.DetectStaleFiles(manifest, _registry);
        Assert.Single(stalenessResult.StaleFiles);

        var reindexer = new IncrementalReindexer(store, _registry);
        reindexer.ReindexFiles(manifest, stalenessResult);

        // Verify fragment updated
        var updatedSymbols = store.LoadFileSymbols(projectName, "hello.py");
        Assert.NotNull(updatedSymbols);
        Assert.Contains(updatedSymbols, s => s.Name == "goodbye");
        Assert.DoesNotContain(updatedSymbols, s => s.Name == "hello");

        // Verify search-index updated
        var allSymbols = store.LoadAllSymbols(projectName);
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "goodbye");
        Assert.DoesNotContain(allSymbols, s => s.Name == "hello");
    }

    [Fact]
    public void ReindexBounded_CapsAtThreshold()
    {
        // Create 60 .py files
        for (int i = 0; i < 60; i++)
        {
            var file = Path.Combine(_tempDir, $"file_{i}.py");
            File.WriteAllText(file, $"x_{i} = {i}\n");
        }

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var indexResult = indexer.Index(_tempDir);
        var projectName = indexResult.Index.ProjectName;

        // Modify all 60 files
        Thread.Sleep(50);
        for (int i = 0; i < 60; i++)
        {
            var file = Path.Combine(_tempDir, $"file_{i}.py");
            File.WriteAllText(file, $"y_{i} = {i + 100}\n");
        }

        var manifest = store.LoadManifest(projectName)!;
        var stalenessResult = StalenessChecker.DetectStaleFiles(manifest, _registry);
        // With short-circuit, stale count is capped at MaxReindexPerQuery
        Assert.Equal(IncrementalReindexer.MaxReindexPerQuery, stalenessResult.StaleFiles.Count);

        var reindexer = new IncrementalReindexer(store, _registry);
        reindexer.ReindexFiles(manifest, stalenessResult);
    }

    [Fact]
    public void EnsureFresh_FindPipeline_ReturnsUpdatedSymbols()
    {
        // Index a .py file with original symbol
        var pyFile = Path.Combine(_tempDir, "test.py");
        File.WriteAllText(pyFile, "def original(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var result = indexer.Index(_tempDir);
        var projectName = result.Index.ProjectName;

        // Verify original
        var allSymbols = store.LoadAllSymbols(projectName);
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "original");

        // Modify the file
        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def updated(): pass\n");

        // Call EnsureFresh (what FindCommand calls) then LoadAllSymbols
        var manifest = QueryReindexer.EnsureFresh(projectName, store, _registry);
        Assert.NotNull(manifest);

        var refreshedSymbols = store.LoadAllSymbols(projectName);
        Assert.NotNull(refreshedSymbols);
        Assert.Contains(refreshedSymbols, s => s.Name == "updated");
        Assert.DoesNotContain(refreshedSymbols, s => s.Name == "original");
    }

    [Fact]
    public void EnsureFresh_OutlinePipeline_ReturnsUpdatedSymbols()
    {
        // Index a .py file with original symbol
        var pyFile = Path.Combine(_tempDir, "test.py");
        File.WriteAllText(pyFile, "def original(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var result = indexer.Index(_tempDir);
        var projectName = result.Index.ProjectName;

        // Modify the file
        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def updated(): pass\n");

        // Call EnsureFresh (what OutlineCommand calls) then LoadFileSymbols
        var manifest = QueryReindexer.EnsureFresh(projectName, store, _registry);
        Assert.NotNull(manifest);

        var fileSymbols = store.LoadFileSymbols(projectName, "test.py");
        Assert.NotNull(fileSymbols);
        Assert.Contains(fileSymbols, s => s.Name == "updated");
        Assert.DoesNotContain(fileSymbols, s => s.Name == "original");
    }

    [Fact]
    public void EnsureFresh_ExtractPipeline_ReturnsUpdatedSource()
    {
        // Index a .py file with original symbol
        var pyFile = Path.Combine(_tempDir, "test.py");
        File.WriteAllText(pyFile, "def original(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var result = indexer.Index(_tempDir);
        var projectName = result.Index.ProjectName;

        // Modify the file
        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def updated(): pass\n");

        // Call EnsureFresh (what ExtractCommand calls) then load symbols
        var manifest = QueryReindexer.EnsureFresh(projectName, store, _registry);
        Assert.NotNull(manifest);

        var fileSymbols = store.LoadFileSymbols(projectName, "test.py");
        Assert.NotNull(fileSymbols);
        var updatedSymbol = fileSymbols.FirstOrDefault(s => s.Name == "updated");
        Assert.NotNull(updatedSymbol);

        // Verify source extraction works with updated byte offsets
        var fileBytes = File.ReadAllBytes(pyFile);
        var source = System.Text.Encoding.UTF8.GetString(
            fileBytes, updatedSymbol.ByteOffset, updatedSymbol.ByteLength);
        Assert.Contains("def updated", source);
    }

    [Fact]
    public void EnsureFresh_ReturnsNull_ForUnknownProject()
    {
        var store = new IndexStore();
        var manifest = QueryReindexer.EnsureFresh("nonexistent", store, _registry);
        Assert.Null(manifest);
    }

    [Fact]
    public void ReindexPreservesUnchangedFiles()
    {
        // Create two .py files
        var file1 = Path.Combine(_tempDir, "keep.py");
        var file2 = Path.Combine(_tempDir, "change.py");
        File.WriteAllText(file1, "def keep(): pass\n");
        File.WriteAllText(file2, "def change(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var indexResult = indexer.Index(_tempDir);
        var projectName = indexResult.Index.ProjectName;

        // Record original state of keep.py
        var originalKeepSymbols = store.LoadFileSymbols(projectName, "keep.py");
        Assert.NotNull(originalKeepSymbols);

        // Modify only change.py
        Thread.Sleep(50);
        File.WriteAllText(file2, "def changed(): pass\n");

        var manifest = store.LoadManifest(projectName)!;
        var stalenessResult = StalenessChecker.DetectStaleFiles(manifest, _registry);
        Assert.Single(stalenessResult.StaleFiles);
        Assert.Contains("change.py", stalenessResult.StaleFiles[0]);

        var reindexer = new IncrementalReindexer(store, _registry);
        var updated = reindexer.ReindexFiles(manifest, stalenessResult);

        // Verify keep.py unchanged
        var keepSymbols = store.LoadFileSymbols(projectName, "keep.py");
        Assert.NotNull(keepSymbols);
        Assert.Contains(keepSymbols, s => s.Name == "keep");

        // Verify change.py updated
        var changeSymbols = store.LoadFileSymbols(projectName, "change.py");
        Assert.NotNull(changeSymbols);
        Assert.Contains(changeSymbols, s => s.Name == "changed");
        Assert.DoesNotContain(changeSymbols, s => s.Name == "change");

        // Verify manifest entry for keep.py preserved
        Assert.True(updated.Files.ContainsKey("keep.py"));
        Assert.Equal("Python", updated.Files["keep.py"].Language);
    }

    [Fact]
    public void DeletedFile_PurgedFromResults()
    {
        // Index dir with 2 .py files
        var file1 = Path.Combine(_tempDir, "keep.py");
        var file2 = Path.Combine(_tempDir, "remove.py");
        File.WriteAllText(file1, "def keep(): pass\n");
        File.WriteAllText(file2, "def remove(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var indexResult = indexer.Index(_tempDir);
        var projectName = indexResult.Index.ProjectName;

        // Verify both files indexed
        var allSymbols = store.LoadAllSymbols(projectName);
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "keep");
        Assert.Contains(allSymbols, s => s.Name == "remove");

        // Delete one file
        File.Delete(file2);

        // Call EnsureFresh
        var manifest = QueryReindexer.EnsureFresh(projectName, store, _registry);
        Assert.NotNull(manifest);

        // Verify deleted file's symbols no longer in LoadAllSymbols (find)
        var refreshedSymbols = store.LoadAllSymbols(projectName);
        Assert.NotNull(refreshedSymbols);
        Assert.Contains(refreshedSymbols, s => s.Name == "keep");
        Assert.DoesNotContain(refreshedSymbols, s => s.Name == "remove");

        // Verify LoadFileSymbols for deleted file returns null
        var deletedSymbols = store.LoadFileSymbols(projectName, "remove.py");
        Assert.Null(deletedSymbols);

        // Verify manifest no longer has the deleted file entry
        Assert.False(manifest.Files.ContainsKey("remove.py"));
        Assert.True(manifest.Files.ContainsKey("keep.py"));
    }

    [Fact]
    public void DeletedFile_FragmentRemovedFromDisk()
    {
        // Index a file
        var pyFile = Path.Combine(_tempDir, "target.py");
        File.WriteAllText(pyFile, "def target(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var indexResult = indexer.Index(_tempDir);
        var projectName = indexResult.Index.ProjectName;

        // Note the storage key
        var manifest = store.LoadManifest(projectName)!;
        var storageKey = manifest.Files["target.py"].StorageKey;
        var fragmentPath = StoragePaths.GetFileFragmentPath(projectName, storageKey);
        Assert.True(File.Exists(fragmentPath));

        // Delete the source file
        File.Delete(pyFile);

        // Call EnsureFresh
        QueryReindexer.EnsureFresh(projectName, store, _registry);

        // Assert fragment JSON no longer exists on disk
        Assert.False(File.Exists(fragmentPath));
    }

    [Fact]
    public void NewFile_DiscoveredAndIndexed()
    {
        // Index dir with 1 .py file
        var file1 = Path.Combine(_tempDir, "original.py");
        File.WriteAllText(file1, "def original(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        var indexResult = indexer.Index(_tempDir);
        var projectName = indexResult.Index.ProjectName;

        // Add a second .py file after indexing
        var file2 = Path.Combine(_tempDir, "added.py");
        File.WriteAllText(file2, "def added(): pass\n");

        // Call EnsureFresh
        var manifest = QueryReindexer.EnsureFresh(projectName, store, _registry);
        Assert.NotNull(manifest);

        // Assert LoadAllSymbols contains symbols from new file
        var allSymbols = store.LoadAllSymbols(projectName);
        Assert.NotNull(allSymbols);
        Assert.Contains(allSymbols, s => s.Name == "original");
        Assert.Contains(allSymbols, s => s.Name == "added");

        // Assert LoadFileSymbols for new file returns its symbols
        var newFileSymbols = store.LoadFileSymbols(projectName, "added.py");
        Assert.NotNull(newFileSymbols);
        Assert.Contains(newFileSymbols, s => s.Name == "added");

        // Assert manifest has the new file entry
        Assert.True(manifest.Files.ContainsKey("added.py"));
    }
}
