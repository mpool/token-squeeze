using TokenSqueeze.Indexing;
using TokenSqueeze.Models;
using TokenSqueeze.Parser;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests;

[Collection("CLI")]
public sealed class StalenessCheckerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storageDir;
    private readonly LanguageRegistry _registry;

    public StalenessCheckerTests()
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
    public void MtimeMatch_ReturnsEmpty()
    {
        // Index a .py file, then call DetectStaleFiles immediately. Mtime matches -> empty.
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Empty(result.StaleFiles);
        Assert.Empty(result.DeletedFiles);
        Assert.Empty(result.NewFiles);
    }

    [Fact]
    public void MtimeDiffers_HashDiffers_ReturnsStale()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Modify content (changes both mtime and hash)
        Thread.Sleep(50); // ensure mtime differs
        File.WriteAllText(pyFile, "def goodbye(): pass\n");

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Single(result.StaleFiles);
        Assert.Contains("hello.py", result.StaleFiles[0]);
    }

    [Fact]
    public void MtimeDiffers_HashMatches_ReturnsEmpty()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        var content = "def hello(): pass\n";
        File.WriteAllText(pyFile, content);

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Touch file: rewrite same content to change mtime
        Thread.Sleep(50);
        File.WriteAllText(pyFile, content);

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Empty(result.StaleFiles);
    }

    [Fact]
    public void DeletedFile_AppearsInDeletedList()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Delete the file
        File.Delete(pyFile);

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Empty(result.StaleFiles);
        Assert.Single(result.DeletedFiles);
        Assert.Contains("hello.py", result.DeletedFiles[0]);
    }

    [Fact]
    public void NewFile_AppearsInNewList()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Add a second .py file after indexing
        var newFile = Path.Combine(_tempDir, "world.py");
        File.WriteAllText(newFile, "def world(): pass\n");

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Empty(result.StaleFiles);
        Assert.Empty(result.DeletedFiles);
        Assert.Single(result.NewFiles);
        Assert.Contains("world.py", result.NewFiles[0]);
    }

    [Fact]
    public void HashShortCircuit_StopsAtMax()
    {
        // Create 60 .py files
        for (int i = 0; i < 60; i++)
        {
            var file = Path.Combine(_tempDir, $"file_{i:D2}.py");
            File.WriteAllText(file, $"x_{i} = {i}\n");
        }

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Modify all 60 files (change content so hash differs)
        Thread.Sleep(50);
        for (int i = 0; i < 60; i++)
        {
            var file = Path.Combine(_tempDir, $"file_{i:D2}.py");
            File.WriteAllText(file, $"y_{i} = {i + 100}\n");
        }

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        // Should cap at MaxReindexPerQuery (50), not all 60
        Assert.Equal(IncrementalReindexer.MaxReindexPerQuery, result.StaleFiles.Count);
    }

    [Fact]
    public void NullMtime_FallsBackToHash()
    {
        // Create a file and index it
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Load manifest and recreate with null LastModifiedUtc (simulates old index)
        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var oldEntry = manifest.Files.Values.First();
        var nullMtimeEntry = oldEntry with { LastModifiedUtc = null };
        var nullMtimeFiles = new Dictionary<string, ManifestFileEntry>
        {
            [manifest.Files.Keys.First()] = nullMtimeEntry
        };
        var nullMtimeManifest = manifest with { Files = nullMtimeFiles };

        // File unchanged -> hash matches -> not stale
        var result = StalenessChecker.DetectStaleFiles(nullMtimeManifest, _registry);
        Assert.Empty(result.StaleFiles);

        // Now modify file -> hash differs -> stale
        Thread.Sleep(50);
        File.WriteAllText(pyFile, "def modified(): pass\n");

        result = StalenessChecker.DetectStaleFiles(nullMtimeManifest, _registry);
        Assert.Single(result.StaleFiles);
    }

    [Fact]
    public void NewFile_UnsupportedExtension_NotDetected()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Add a file with unsupported extension
        var txtFile = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(txtFile, "just text");

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Empty(result.NewFiles);
    }

    [Fact]
    public void NewFile_InSkippedDirectory_NotDetected()
    {
        var pyFile = Path.Combine(_tempDir, "hello.py");
        File.WriteAllText(pyFile, "def hello(): pass\n");

        var store = new IndexStore();
        var indexer = new ProjectIndexer(store, _registry);
        indexer.Index(_tempDir);

        // Add a .py file inside node_modules (should be skipped)
        var nodeModules = Path.Combine(_tempDir, "node_modules");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllText(Path.Combine(nodeModules, "lib.py"), "def lib(): pass\n");

        var manifest = store.LoadManifest(Path.GetFileName(_tempDir))!;
        var result = StalenessChecker.DetectStaleFiles(manifest, _registry);

        Assert.Empty(result.NewFiles);
    }
}
