using System.Text.Json;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class PurgeCommandTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    [Fact]
    public void PurgeCommand_Success_DeletesProjectAndReturnsJson()
    {
        var sourceDir = _harness.CreateSourceDir("purge-ok", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(): pass\n"
        });

        var (indexExit, indexOutput) = _harness.Run("index", sourceDir);
        Assert.Equal(0, indexExit);
        using var indexDoc = JsonDocument.Parse(indexOutput);
        var projectName = indexDoc.RootElement.GetProperty("projectName").GetString()!;

        var (purgeExit, purgeOutput) = _harness.Run("purge", projectName);

        Assert.Equal(0, purgeExit);
        using var purgeDoc = JsonDocument.Parse(purgeOutput);
        Assert.Equal("deleted", purgeDoc.RootElement.GetProperty("status").GetString());

        // Verify project is gone from list
        var (listExit, listOutput) = _harness.Run("list");
        Assert.Equal(0, listExit);
        using var listDoc = JsonDocument.Parse(listOutput);
        var projects = listDoc.RootElement.GetProperty("projects");
        foreach (var p in projects.EnumerateArray())
        {
            Assert.NotEqual(projectName, p.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void PurgeCommand_NonexistentProject_ReturnsJsonError()
    {
        var (exitCode, output) = _harness.Run("purge", "nonexistent-project-xyz");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void PurgeCommand_PathTraversal_ReturnsJsonError()
    {
        var (exitCode, output) = _harness.Run("purge", "../../../etc");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
