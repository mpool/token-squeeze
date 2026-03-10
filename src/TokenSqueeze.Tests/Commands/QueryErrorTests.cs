using System.Text.Json;
using TokenSqueeze.Storage;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class QueryErrorTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    private string IndexAndCorrupt()
    {
        var sourceDir = _harness.CreateSourceDir("query-error", new Dictionary<string, string>
        {
            ["test.py"] = "def foo(): pass\n"
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);

        // Corrupt the manifest so QueryReindexer.EnsureFresh will throw on deserialization
        var cacheDir = Path.Combine(sourceDir, ".cache");
        File.WriteAllText(StoragePaths.GetManifestPath(cacheDir), "CORRUPT");

        return sourceDir;
    }

    [Fact]
    public void FindCommand_ReturnsJsonError_WhenReindexFails()
    {
        var sourceDir = IndexAndCorrupt();

        var (exitCode, output) = _harness.RunInDir(sourceDir, "find", "foo");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void ExtractCommand_ReturnsJsonError_WhenReindexFails()
    {
        var sourceDir = IndexAndCorrupt();

        var (exitCode, output) = _harness.RunInDir(sourceDir, "extract", "any-id");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void OutlineCommand_ReturnsJsonError_WhenReindexFails()
    {
        var sourceDir = IndexAndCorrupt();

        var (exitCode, output) = _harness.RunInDir(sourceDir, "outline", "test.py");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
