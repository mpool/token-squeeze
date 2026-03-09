using System.Text.Json;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class IndexCommandTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    [Fact]
    public void MultiFileIndex_ProducesCorrectCounts()
    {
        var sourceDir = _harness.CreateSourceDir("multi", new Dictionary<string, string>
        {
            ["app.py"] = "def main():\n    pass\n\ndef helper():\n    pass\n",
            ["utils.js"] = "function format(s) { return s; }\nclass Formatter { }\n"
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("filesIndexed").GetInt32());
        // 2 Python functions + 1 JS function + 1 JS class = 4 minimum
        Assert.True(root.GetProperty("symbolsExtracted").GetInt32() >= 4,
            $"Expected at least 4 symbols, got {root.GetProperty("symbolsExtracted").GetInt32()}");
    }

    [Fact]
    public void IncrementalReuse_UnchangedDirectoryProducesSameResults()
    {
        var sourceDir = _harness.CreateSourceDir("incremental", new Dictionary<string, string>
        {
            ["app.py"] = "def main():\n    pass\n"
        });

        // First index
        var (exit1, output1) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit1);
        using var doc1 = JsonDocument.Parse(output1);
        var symbols1 = doc1.RootElement.GetProperty("symbolsExtracted").GetInt32();
        var files1 = doc1.RootElement.GetProperty("filesIndexed").GetInt32();

        // Second index (no changes)
        var (exit2, output2) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit2);
        using var doc2 = JsonDocument.Parse(output2);
        var symbols2 = doc2.RootElement.GetProperty("symbolsExtracted").GetInt32();
        var files2 = doc2.RootElement.GetProperty("filesIndexed").GetInt32();

        // Same counts (files are reused from existing index)
        Assert.Equal(files1, files2);
        Assert.Equal(symbols1, symbols2);
    }

    [Fact]
    public void ModifiedFileReparse_DetectsContentChanges()
    {
        var sourceDir = _harness.CreateSourceDir("modified", new Dictionary<string, string>
        {
            ["app.py"] = "def main():\n    pass\n"
        });

        // First index
        var (exit1, output1) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit1);
        using var doc1 = JsonDocument.Parse(output1);
        var symbols1 = doc1.RootElement.GetProperty("symbolsExtracted").GetInt32();

        // Modify file to add a new function
        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(
            Path.Combine(sourceDir, "app.py"),
            "def main():\n    pass\n\ndef extra():\n    pass\n",
            encoding);

        // Re-index
        var (exit2, output2) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit2);
        using var doc2 = JsonDocument.Parse(output2);
        var symbols2 = doc2.RootElement.GetProperty("symbolsExtracted").GetInt32();

        Assert.True(symbols2 > symbols1,
            $"Expected more symbols after adding function. Before: {symbols1}, After: {symbols2}");
    }

    [Fact]
    public void NewFilePickup_DetectsAddedFiles()
    {
        var sourceDir = _harness.CreateSourceDir("newfile", new Dictionary<string, string>
        {
            ["app.py"] = "def main():\n    pass\n"
        });

        // First index
        var (exit1, output1) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit1);
        using var doc1 = JsonDocument.Parse(output1);
        var files1 = doc1.RootElement.GetProperty("filesIndexed").GetInt32();

        // Add a new file
        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(
            Path.Combine(sourceDir, "extra.py"),
            "def bonus():\n    pass\n",
            encoding);

        // Re-index
        var (exit2, output2) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit2);
        using var doc2 = JsonDocument.Parse(output2);
        var files2 = doc2.RootElement.GetProperty("filesIndexed").GetInt32();

        Assert.Equal(files1 + 1, files2);
    }

    [Fact]
    public void HashNotMtime_UnchangedContentNotReparsed()
    {
        var sourceDir = _harness.CreateSourceDir("hashtest", new Dictionary<string, string>
        {
            ["app.py"] = "def main():\n    pass\n"
        });

        // First index
        var (exit1, output1) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit1);
        using var doc1 = JsonDocument.Parse(output1);
        var symbols1 = doc1.RootElement.GetProperty("symbolsExtracted").GetInt32();
        var project1 = doc1.RootElement.GetProperty("projectName").GetString()!;

        // Touch the file (change mtime but not content)
        var filePath = Path.Combine(sourceDir, "app.py");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddHours(1));

        // Re-index
        var (exit2, output2) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exit2);
        using var doc2 = JsonDocument.Parse(output2);
        var symbols2 = doc2.RootElement.GetProperty("symbolsExtracted").GetInt32();

        // Same content hash means same results -- file should be reused
        Assert.Equal(symbols1, symbols2);

        // Verify the index content is identical by checking outline
        var (outlineExit, outlineOutput) = _harness.Run("outline", project1, "app.py");
        Assert.Equal(0, outlineExit);
        using var outlineDoc = JsonDocument.Parse(outlineOutput);
        var symbols = outlineDoc.RootElement.GetProperty("symbols");
        Assert.True(symbols.GetArrayLength() >= 1);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
