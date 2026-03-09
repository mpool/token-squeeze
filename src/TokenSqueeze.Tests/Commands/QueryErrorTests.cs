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

        using var doc = JsonDocument.Parse(output);
        var projectName = doc.RootElement.GetProperty("projectName").GetString()!;

        // Corrupt the manifest so QueryReindexer.EnsureFresh will throw on deserialization
        File.WriteAllText(StoragePaths.GetManifestPath(projectName), "CORRUPT");

        return projectName;
    }

    [Fact]
    public void FindCommand_ReturnsJsonError_WhenReindexFails()
    {
        var projectName = IndexAndCorrupt();

        var (exitCode, output) = _harness.Run("find", projectName, "foo");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void ExtractCommand_ReturnsJsonError_WhenReindexFails()
    {
        var projectName = IndexAndCorrupt();

        var (exitCode, output) = _harness.Run("extract", projectName, "any-id");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void OutlineCommand_ReturnsJsonError_WhenReindexFails()
    {
        var projectName = IndexAndCorrupt();

        var (exitCode, output) = _harness.Run("outline", projectName, "test.py");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
