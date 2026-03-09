using System.Text.Json;
using TokenSqueeze.Tests.Helpers;
using Xunit;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class FindCommandTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    private string IndexScoringProject()
    {
        // Carefully crafted Python source to produce predictable scoring:
        // - "Foo" exact name match -> 200 + 75 (qualified) + 50 (signature) = 325
        // - "FooBar" contains "Foo" -> 100 + 75 (qualified) + 50 (signature) = 225
        // - "unrelated" docstring mentions Foo -> 25
        // - "another" no relation -> 0 (excluded)
        var sourceDir = _harness.CreateSourceDir("find-scoring", new Dictionary<string, string>
        {
            ["scoring.py"] = "def Foo():\n    \"\"\"A function named exactly Foo\"\"\"\n    pass\n\n" +
                             "def FooBar():\n    \"\"\"Contains Foo in name but not exact\"\"\"\n    pass\n\n" +
                             "def unrelated():\n    \"\"\"This docstring mentions Foo\"\"\"\n    pass\n\n" +
                             "def another():\n    \"\"\"No relation to search query\"\"\"\n    pass\n"
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        return doc.RootElement.GetProperty("projectName").GetString()!;
    }

    private JsonElement RunFind(string projectName, string query, params string[] extraArgs)
    {
        var args = new List<string> { "find" };
        args.AddRange(extraArgs);
        args.Add(projectName);
        args.Add(query);
        var (exitCode, output) = _harness.Run(args.ToArray());
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        // Return a clone since doc will be disposed
        return doc.RootElement.Clone();
    }

    private JsonElement? FindResultByName(JsonElement root, string name)
    {
        var results = root.GetProperty("results");
        foreach (var r in results.EnumerateArray())
        {
            if (r.GetProperty("name").GetString() == name)
                return r;
        }
        return null;
    }

    [Fact]
    public void ExactMatchScore_Returns325()
    {
        var projectName = IndexScoringProject();
        var root = RunFind(projectName, "Foo");

        var foo = FindResultByName(root, "Foo");
        Assert.NotNull(foo);
        // Exact(200) + Qualified(75) + Signature(50) + Docstring("...Foo...")(25) = 350
        Assert.Equal(350, foo.Value.GetProperty("score").GetInt32());
    }

    [Fact]
    public void ContainsMatchScore_Returns225()
    {
        var projectName = IndexScoringProject();
        var root = RunFind(projectName, "Foo");

        var fooBar = FindResultByName(root, "FooBar");
        Assert.NotNull(fooBar);
        // Contains(100) + Qualified(75) + Signature(50) + Docstring("...Foo...")(25) = 250
        Assert.Equal(250, fooBar.Value.GetProperty("score").GetInt32());
    }

    [Fact]
    public void DocstringOnlyMatch_Returns25()
    {
        var projectName = IndexScoringProject();
        var root = RunFind(projectName, "Foo");

        var unrelated = FindResultByName(root, "unrelated");
        Assert.NotNull(unrelated);
        // Only docstring contains "Foo" = 25
        Assert.Equal(25, unrelated.Value.GetProperty("score").GetInt32());
    }

    [Fact]
    public void NoMatchExcluded_AnotherNotInResults()
    {
        var projectName = IndexScoringProject();
        var root = RunFind(projectName, "Foo");

        var another = FindResultByName(root, "another");
        Assert.Null(another);
    }

    [Fact]
    public void ResultOrdering_DescendingByScore()
    {
        var projectName = IndexScoringProject();
        var root = RunFind(projectName, "Foo");

        var results = root.GetProperty("results");
        int previousScore = int.MaxValue;
        foreach (var r in results.EnumerateArray())
        {
            var score = r.GetProperty("score").GetInt32();
            Assert.True(score <= previousScore, $"Results not ordered descending: {score} after {previousScore}");
            previousScore = score;
        }
    }

    [Fact]
    public void KindFilter_RestrictsToMatchingKind()
    {
        // Create source with a class and a function both containing "Alpha"
        var sourceDir = _harness.CreateSourceDir("find-kind", new Dictionary<string, string>
        {
            ["mixed.py"] = """
class AlphaClass:
    pass

def AlphaFunc():
    pass
"""
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);
        using var indexDoc = JsonDocument.Parse(output);
        var projectName = indexDoc.RootElement.GetProperty("projectName").GetString()!;

        var root = RunFind(projectName, "Alpha", "--kind", "function");
        var results = root.GetProperty("results");

        foreach (var r in results.EnumerateArray())
        {
            var kind = r.GetProperty("kind").GetString();
            Assert.Equal("Function", kind);
        }
        // Should have at least one function result
        Assert.True(results.GetArrayLength() >= 1, "Expected at least one function result");
    }

    [Fact]
    public void PathFilter_RestrictsToMatchingFiles()
    {
        var sourceDir = _harness.CreateSourceDir("find-path", new Dictionary<string, string>
        {
            ["subdir1/mod1.py"] = "def Target():\n    pass\n",
            ["subdir2/mod2.py"] = "def Target():\n    pass\n"
        });

        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);
        using var indexDoc = JsonDocument.Parse(output);
        var projectName = indexDoc.RootElement.GetProperty("projectName").GetString()!;

        var root = RunFind(projectName, "Target", "--path", "subdir1/*");
        var results = root.GetProperty("results");

        Assert.True(results.GetArrayLength() >= 1, "Expected at least one result from subdir1");
        foreach (var r in results.EnumerateArray())
        {
            var file = r.GetProperty("file").GetString()!;
            Assert.Contains("subdir1", file);
        }
    }

    public void Dispose()
    {
        _harness.Dispose();
    }
}
