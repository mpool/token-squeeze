using System.Text.Json;
using TokenSqueeze.Models;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class CliIntegrationTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    [Fact]
    public void ListEmpty_ReturnsEmptyProjectsArray()
    {
        var (exitCode, output) = _harness.Run("list");

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var projects = doc.RootElement.GetProperty("projects");
        Assert.Equal(JsonValueKind.Array, projects.ValueKind);
        Assert.Equal(0, projects.GetArrayLength());
    }

    [Fact]
    public void IndexAndList_IndexesDirectoryAndAppearsInList()
    {
        var sourceDir = _harness.CreateSourceDir("myproject", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        var projectName = root.GetProperty("projectName").GetString();
        Assert.NotNull(projectName);
        Assert.True(root.GetProperty("filesIndexed").GetInt32() >= 1);
        Assert.True(root.GetProperty("symbolsExtracted").GetInt32() >= 1);

        // Verify it appears in list
        var (listExit, listOutput) = _harness.Run("list");
        Assert.Equal(0, listExit);
        using var listDoc = JsonDocument.Parse(listOutput);
        var projects = listDoc.RootElement.GetProperty("projects");
        Assert.True(projects.GetArrayLength() >= 1);

        var found = false;
        foreach (var p in projects.EnumerateArray())
        {
            if (p.GetProperty("name").GetString() == projectName)
            {
                found = true;
                break;
            }
        }
        Assert.True(found, $"Project '{projectName}' not found in list output");
    }

    [Fact]
    public void OutlineAfterIndex_ReturnsSymbols()
    {
        var sourceDir = _harness.CreateSourceDir("outline-test", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (indexExit, indexOutput) = _harness.Run("index", sourceDir);
        Assert.Equal(0, indexExit);
        using var indexDoc = JsonDocument.Parse(indexOutput);
        var projectName = indexDoc.RootElement.GetProperty("projectName").GetString()!;

        var (outlineExit, outlineOutput) = _harness.Run("outline", projectName, "hello.py");

        Assert.Equal(0, outlineExit);
        using var doc = JsonDocument.Parse(outlineOutput);
        var symbols = doc.RootElement.GetProperty("symbols");
        Assert.True(symbols.GetArrayLength() >= 1);

        var hasGreet = false;
        foreach (var sym in symbols.EnumerateArray())
        {
            if (sym.GetProperty("name").GetString() == "greet")
            {
                hasGreet = true;
                break;
            }
        }
        Assert.True(hasGreet, "Expected 'greet' symbol in outline");
    }

    [Fact]
    public void FindAfterIndex_ReturnsMatchingSymbols()
    {
        var sourceDir = _harness.CreateSourceDir("find-test", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (indexExit, _) = _harness.Run("index", sourceDir);
        Assert.Equal(0, indexExit);

        // Project name is derived from directory name
        var (listExit, listOutput) = _harness.Run("list");
        Assert.Equal(0, listExit);
        using var listDoc = JsonDocument.Parse(listOutput);
        var projectName = listDoc.RootElement.GetProperty("projects")[0].GetProperty("name").GetString()!;

        var (findExit, findOutput) = _harness.Run("find", projectName, "greet");

        Assert.Equal(0, findExit);
        using var findDoc = JsonDocument.Parse(findOutput);
        var results = findDoc.RootElement.GetProperty("results");
        Assert.True(results.GetArrayLength() >= 1);

        var first = results[0];
        Assert.Equal("greet", first.GetProperty("name").GetString());
        Assert.True(first.GetProperty("score").GetInt32() > 0);
    }

    [Fact]
    public void ExtractAfterIndex_ReturnsSourceCode()
    {
        var sourceDir = _harness.CreateSourceDir("extract-test", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (indexExit, _) = _harness.Run("index", sourceDir);
        Assert.Equal(0, indexExit);

        // Get the project name and symbol ID via find
        var (listExit, listOutput) = _harness.Run("list");
        Assert.Equal(0, listExit);
        using var listDoc = JsonDocument.Parse(listOutput);
        var projectName = listDoc.RootElement.GetProperty("projects")[0].GetProperty("name").GetString()!;

        var (findExit, findOutput) = _harness.Run("find", projectName, "greet");
        Assert.Equal(0, findExit);
        using var findDoc = JsonDocument.Parse(findOutput);
        var symbolId = findDoc.RootElement.GetProperty("results")[0].GetProperty("id").GetString()!;

        var (extractExit, extractOutput) = _harness.Run("extract", projectName, symbolId);

        Assert.Equal(0, extractExit);
        using var extractDoc = JsonDocument.Parse(extractOutput);
        var source = extractDoc.RootElement.GetProperty("source").GetString();
        Assert.NotNull(source);
        Assert.Contains("def greet", source);
    }

    [Fact]
    public void PurgeAndVerify_RemovesProjectFromList()
    {
        var sourceDir = _harness.CreateSourceDir("purge-test", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (indexExit, indexOutput) = _harness.Run("index", sourceDir);
        Assert.Equal(0, indexExit);
        using var indexDoc = JsonDocument.Parse(indexOutput);
        var projectName = indexDoc.RootElement.GetProperty("projectName").GetString()!;

        var (purgeExit, purgeOutput) = _harness.Run("purge", projectName);
        Assert.Equal(0, purgeExit);
        using var purgeDoc = JsonDocument.Parse(purgeOutput);
        Assert.Equal("deleted", purgeDoc.RootElement.GetProperty("status").GetString());

        // Verify gone from list
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
    public void ParseTest_ReturnsSymbolJson()
    {
        var sourceDir = _harness.CreateSourceDir("parsetest", new Dictionary<string, string>
        {
            ["sample.py"] = "def hello():\n    pass\n"
        });
        var filePath = Path.Combine(sourceDir, "sample.py");

        var (exitCode, output) = _harness.Run("parse-test", filePath);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var symbols = doc.RootElement.GetProperty("symbols");
        Assert.True(symbols.GetArrayLength() >= 1);
        Assert.True(doc.RootElement.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public void FindNonexistentProject_ReturnsError()
    {
        var (exitCode, output) = _harness.Run("find", "nonexistent", "query");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void ExtractNonexistentProject_ReturnsError()
    {
        var (exitCode, output) = _harness.Run("extract", "nonexistent", "fake-id");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
