using System.Text;
using System.Text.Json;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class ExtractCommandTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    private (string projectName, string sourceDir) IndexPythonProject(string name, Dictionary<string, string> files)
    {
        var sourceDir = _harness.CreateSourceDir(name, files);
        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var projectName = doc.RootElement.GetProperty("projectName").GetString()!;
        return (projectName, sourceDir);
    }

    private string FindSymbolId(string projectName, string symbolName)
    {
        var (exitCode, output) = _harness.Run("find", projectName, symbolName);
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
        var (projectName, _) = IndexPythonProject("extract-byte", new Dictionary<string, string>
        {
            ["hello.py"] = source
        });

        var symbolId = FindSymbolId(projectName, "greet");
        var (exitCode, output) = _harness.Run("extract", projectName, symbolId);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var extractedSource = doc.RootElement.GetProperty("source").GetString()!;
        Assert.Contains("def greet(name):", extractedSource);
        Assert.Contains("return f\"Hello {name}\"", extractedSource);
    }

    [Fact]
    public void AutoReindex_ReturnsUpdatedSource_NoStaleFlag()
    {
        var (projectName, sourceDir) = IndexPythonProject("extract-stale", new Dictionary<string, string>
        {
            ["app.py"] = "def greet(name):\n    return f\"Hello {name}\"\n"
        });

        // Modify the file -- auto-reindex should pick up the change
        Thread.Sleep(50);
        File.WriteAllText(Path.Combine(sourceDir, "app.py"), "def greet(name):\n    return f\"Hiya {name}\"\n");

        // Extract should auto-reindex and return updated source without stale/warning
        var symbolId = FindSymbolId(projectName, "greet");
        var (exitCode, output) = _harness.Run("extract", projectName, symbolId);

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
        var (projectName, _) = IndexPythonProject("extract-batch", new Dictionary<string, string>
        {
            ["funcs.py"] = "def alpha():\n    pass\n\ndef beta():\n    pass\n"
        });

        var alphaId = FindSymbolId(projectName, "alpha");
        var betaId = FindSymbolId(projectName, "beta");

        var (exitCode, output) = _harness.Run("extract", projectName, "--batch", alphaId, "--batch", betaId);

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
        var (projectName, _) = IndexPythonProject("extract-missing-sym", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    pass\n"
        });

        var (exitCode, output) = _harness.Run("extract", projectName, "nonexistent::fake#Function");

        // ExtractCommand returns 0 even for missing symbols (it includes error in result JSON)
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("error", out var errorProp), "Expected error property for missing symbol");
        Assert.Equal("Symbol not found", errorProp.GetString());
    }

    [Fact]
    public void MissingSourceFile_ReturnsError()
    {
        var (projectName, sourceDir) = IndexPythonProject("extract-missing-file", new Dictionary<string, string>
        {
            ["temp.py"] = "def ephemeral():\n    pass\n"
        });

        var symbolId = FindSymbolId(projectName, "ephemeral");

        // Delete the source file after indexing
        File.Delete(Path.Combine(sourceDir, "temp.py"));

        var (exitCode, output) = _harness.Run("extract", projectName, symbolId);

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("error", out var errorProp), "Expected error for missing source file");
        // After BUG-01 fix, EnsureFresh purges deleted files from manifest,
        // so the symbol is gone entirely (not "source file not found")
        Assert.Equal("Symbol not found", errorProp.GetString());
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
