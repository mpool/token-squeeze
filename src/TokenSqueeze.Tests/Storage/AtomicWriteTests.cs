using System.Text.Json;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

public sealed class AtomicWriteTests : IDisposable
{
    private readonly string _tempDir;

    public AtomicWriteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ts-atomic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ConcurrentAtomicWrite_ToSamePath_ProducesValidJson()
    {
        var targetPath = Path.Combine(_tempDir, "concurrent-target.json");
        var tasks = new Task[10];

        for (var i = 0; i < tasks.Length; i++)
        {
            var value = new { index = i, data = $"payload-{i}" };
            tasks[i] = Task.Run(() => IndexStore.AtomicWrite(targetPath, value));
        }

        await Task.WhenAll(tasks);

        Assert.True(File.Exists(targetPath), "Target file must exist after concurrent writes");

        var content = File.ReadAllText(targetPath);
        var doc = JsonDocument.Parse(content);
        Assert.NotNull(doc);
        Assert.True(doc.RootElement.TryGetProperty("index", out _), "Result must be valid JSON from one of the writers");
    }

    [Fact]
    public void AtomicWrite_UsesGuidBasedTempSuffix()
    {
        var sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TokenSqueeze", "Storage", "IndexStore.cs"));
        Assert.True(File.Exists(sourcePath), $"IndexStore.cs not found at {sourcePath}");

        var source = File.ReadAllText(sourcePath);

        // Find the AtomicWrite method and confirm it uses Guid.NewGuid
        var methodIndex = source.IndexOf("AtomicWrite", StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "AtomicWrite method must exist in IndexStore.cs");

        var afterMethod = source[methodIndex..];
        Assert.True(
            afterMethod.Contains("Guid.NewGuid", StringComparison.Ordinal),
            "AtomicWrite must use Guid.NewGuid for unique temp file suffix to prevent concurrent write collisions");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
