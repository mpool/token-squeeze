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
    public void IndexAndVerify_IndexesDirectorySuccessfully()
    {
        var sourceDir = _harness.CreateSourceDir("myproject", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("filesIndexed").GetInt32() >= 1);
        Assert.True(root.GetProperty("symbolsExtracted").GetInt32() >= 1);
    }

    [Fact]
    public void OutlineAfterIndex_ReturnsSymbols()
    {
        var sourceDir = _harness.CreateSourceDir("outline-test", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        var (indexExit, _) = _harness.Run("index", sourceDir);
        Assert.Equal(0, indexExit);

        // Query commands resolve cache from cwd
        var (outlineExit, outlineOutput) = _harness.RunInDir(sourceDir, "outline", "hello.py");

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

        var (findExit, findOutput) = _harness.RunInDir(sourceDir, "find", "greet");

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

        var (findExit, findOutput) = _harness.RunInDir(sourceDir, "find", "greet");
        Assert.Equal(0, findExit);
        using var findDoc = JsonDocument.Parse(findOutput);
        var symbolId = findDoc.RootElement.GetProperty("results")[0].GetProperty("id").GetString()!;

        var (extractExit, extractOutput) = _harness.RunInDir(sourceDir, "extract", symbolId);

        Assert.Equal(0, extractExit);
        using var extractDoc = JsonDocument.Parse(extractOutput);
        var source = extractDoc.RootElement.GetProperty("source").GetString();
        Assert.NotNull(source);
        Assert.Contains("def greet", source);
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
    public void FindNoIndex_ReturnsError()
    {
        // Run find in a directory with no .cache
        var emptyDir = _harness.CreateSourceDir("no-index", new Dictionary<string, string>());

        var (exitCode, output) = _harness.RunInDir(emptyDir, "find", "query");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("No index found", error!);
    }

    [Fact]
    public void ExtractNoIndex_ReturnsError()
    {
        var emptyDir = _harness.CreateSourceDir("no-index-extract", new Dictionary<string, string>());

        var (exitCode, output) = _harness.RunInDir(emptyDir, "extract", "fake-id");

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(output);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("No index found", error!);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
