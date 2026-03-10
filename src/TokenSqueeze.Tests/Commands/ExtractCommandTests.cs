using System.Text;
using System.Text.Json;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class ExtractCommandTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    private (string sourceDir, string sourceDir2) IndexPythonProject(string name, Dictionary<string, string> files)
    {
        var sourceDir = _harness.CreateSourceDir(name, files);
        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);
        return (sourceDir, sourceDir);
    }

    private string FindSymbolId(string sourceDir, string symbolName)
    {
        var (exitCode, output) = _harness.RunInDir(sourceDir, "find", symbolName);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var results = doc.RootElement.GetProperty("results");
        foreach (var r in results.EnumerateArray())
        {
            if (r.GetProperty("name").GetString() == symbolName)
                return r.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException($"Symbol '{symbolName}' not found in find results");
    }

    [Fact]
    public void ByteOffsetExtraction_ReturnsCorrectSource()
    {
        var source = "def greet(name):\n    return f\"Hello {name}\"\n";
        var (sourceDir, _) = IndexPythonProject("extract-byte", new Dictionary<string, string>
        {
            ["hello.py"] = source
        });

        var symbolId = FindSymbolId(sourceDir, "greet");
        var (exitCode, output) = _harness.RunInDir(sourceDir, "extract", symbolId);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var extractedSource = doc.RootElement.GetProperty("source").GetString()!;
        Assert.Contains("def greet(name):", extractedSource);
        Assert.Contains("return f\"Hello {name}\"", extractedSource);
    }

    [Fact]
    public void AutoReindex_ReturnsUpdatedSource_NoStaleFlag()
    {
        var (sourceDir, _) = IndexPythonProject("extract-stale", new Dictionary<string, string>
        {
            ["app.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        Thread.Sleep(50);
        File.WriteAllText(Path.Combine(sourceDir, "app.py"), "def greet(name):\n    return f\"Hiya {name}\"\n");

        var symbolId = FindSymbolId(sourceDir, "greet");
        var (exitCode, output) = _harness.RunInDir(sourceDir, "extract", symbolId);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("source", out var sourceProp));
        Assert.Contains("Hiya", sourceProp.GetString());
        Assert.False(root.TryGetProperty("stale", out _), "Output should not contain 'stale' field after auto-reindex");
        Assert.False(root.TryGetProperty("warning", out _), "Output should not contain 'warning' field after auto-reindex");
    }

    [Fact]
    public void BatchMode_ReturnsMultipleSymbols()
    {
        var (sourceDir, _) = IndexPythonProject("extract-batch", new Dictionary<string, string>
        {
            ["funcs.py"] = "def alpha():\n    pass\n\ndef beta():\n    pass\n"
        });

        var alphaId = FindSymbolId(sourceDir, "alpha");
        var betaId = FindSymbolId(sourceDir, "beta");

        var (exitCode, output) = _harness.RunInDir(sourceDir, "extract", "--batch", alphaId, "--batch", betaId);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var results = doc.RootElement.GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());

        var names = new HashSet<string>();
        foreach (var r in results.EnumerateArray())
        {
            names.Add(r.GetProperty("name").GetString()!);
        }
        Assert.Contains("alpha", names);
        Assert.Contains("beta", names);
    }

    [Fact]
    public void MissingSymbol_ReturnsError()
    {
        var (sourceDir, _) = IndexPythonProject("extract-missing-sym", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    pass\n"
        });

        var (exitCode, output) = _harness.RunInDir(sourceDir, "extract", "nonexistent::fake#Function");

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("error", out var errorProp), "Expected error property for missing symbol");
        Assert.Equal("Symbol not found", errorProp.GetString());
    }

    [Fact]
    public void MissingSourceFile_ReturnsError()
    {
        var (sourceDir, _) = IndexPythonProject("extract-missing-file", new Dictionary<string, string>
        {
            ["temp.py"] = "def ephemeral():\n    pass\n"
        });

        var symbolId = FindSymbolId(sourceDir, "ephemeral");

        File.Delete(Path.Combine(sourceDir, "temp.py"));

        var (exitCode, output) = _harness.RunInDir(sourceDir, "extract", symbolId);

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("error", out var errorProp), "Expected error for missing source file");
        Assert.Equal("Symbol not found", errorProp.GetString());
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
