using TokenSqueeze.Indexing;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Indexing;

public sealed class DirectoryWalkerGitignoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LanguageRegistry _registry;

    public DirectoryWalkerGitignoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ts_walker_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _registry = new LanguageRegistry();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateFile(string relativePath, string content = "# placeholder")
    {
        var fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private HashSet<string> WalkRelative()
    {
        var walker = new DirectoryWalker(_registry, _tempDir);
        return walker.Walk()
            .Select(f => Path.GetRelativePath(_tempDir, f.Path).Replace('\\', '/'))
            .ToHashSet();
    }

    [Fact]
    public void RootGitignoreExcludesGlobally()
    {
        CreateFile(".gitignore", "*.log");
        CreateFile("debug.log");
        CreateFile("app.py");

        var walked = WalkRelative();

        Assert.Contains("app.py", walked);
        Assert.DoesNotContain("debug.log", walked);
    }

    [Fact]
    public void NestedGitignoreExcludesLocally()
    {
        CreateFile("subdir/.gitignore", "*.generated.cs");
        CreateFile("subdir/Foo.generated.cs", "// placeholder");
        CreateFile("subdir/Bar.cs", "// placeholder");

        var walked = WalkRelative();

        Assert.Contains("subdir/Bar.cs", walked);
        Assert.DoesNotContain("subdir/Foo.generated.cs", walked);
    }

    [Fact]
    public void NestedGitignoreDoesNotAffectSiblings()
    {
        CreateFile("subdir/.gitignore", "*.generated.cs");
        CreateFile("subdir/Foo.generated.cs", "// placeholder");
        CreateFile("other/Baz.generated.cs", "// placeholder");

        var walked = WalkRelative();

        Assert.Contains("other/Baz.generated.cs", walked);
        Assert.DoesNotContain("subdir/Foo.generated.cs", walked);
    }

    [Fact]
    public void NoGitignoreAllSupportedFilesWalked()
    {
        CreateFile("app.py");
        CreateFile("index.js", "// placeholder");

        var walked = WalkRelative();

        Assert.Contains("app.py", walked);
        Assert.Contains("index.js", walked);
    }
}
