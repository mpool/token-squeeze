using TokenSqueeze.Indexing;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests.Indexing;

public sealed class DirectoryWalkerFilterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LanguageRegistry _registry = new();

    public DirectoryWalkerFilterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ts-walker-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateFile(string relativePath, byte[]? content = null)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content ?? "# placeholder"u8.ToArray());
    }

    [Fact]
    public void IsBinaryContent_ReturnsTrueForBytesWithNullBytes()
    {
        var content = new byte[] { 0x48, 0x65, 0x00, 0x6C };

        Assert.True(DirectoryWalker.IsBinaryContent(content));
    }

    [Fact]
    public void IsBinaryContent_ReturnsFalseForTextBytes()
    {
        var content = "Hello, world!"u8.ToArray();

        Assert.False(DirectoryWalker.IsBinaryContent(content));
    }

    [Fact]
    public void IsBinaryContent_ReturnsFalseForEmptySpan()
    {
        Assert.False(DirectoryWalker.IsBinaryContent(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Walk_SkipsNodeModulesAndGitDirectories()
    {
        CreateFile("app.py", "def main(): pass"u8.ToArray());
        CreateFile("node_modules/lib.js", "function f() {}"u8.ToArray());
        CreateFile(".git/config", "ref: refs/heads/main"u8.ToArray());

        var walker = new DirectoryWalker(_registry, _tempDir);
        var files = walker.Walk().Select(f => Path.GetFileName(f.Path)).ToList();

        Assert.Contains("app.py", files);
        Assert.DoesNotContain("lib.js", files);
        Assert.DoesNotContain("config", files);
    }

    [Fact]
    public void Walk_SkipsBinAndObjDirectories()
    {
        CreateFile("src/main.cs", "class C {}"u8.ToArray());
        CreateFile("bin/Debug/app.cs", "class D {}"u8.ToArray());
        CreateFile("obj/Debug/gen.cs", "class E {}"u8.ToArray());

        var walker = new DirectoryWalker(_registry, _tempDir);
        var files = walker.Walk().Select(f => Path.GetFileName(f.Path)).ToList();

        Assert.Contains("main.cs", files);
        Assert.DoesNotContain("app.cs", files);
        Assert.DoesNotContain("gen.cs", files);
    }

    [Fact]
    public void Walk_OnlyYieldsSupportedExtensions()
    {
        CreateFile("code.py", "x = 1"u8.ToArray());
        CreateFile("readme.md", "# Hello"u8.ToArray());
        CreateFile("data.json", "{}"u8.ToArray());
        CreateFile("notes.txt", "notes"u8.ToArray());

        var walker = new DirectoryWalker(_registry, _tempDir);
        var files = walker.Walk().Select(f => Path.GetFileName(f.Path)).ToList();

        Assert.Contains("code.py", files);
        Assert.DoesNotContain("readme.md", files);
        Assert.DoesNotContain("data.json", files);
        Assert.DoesNotContain("notes.txt", files);
    }

    [Fact]
    public void Walk_DoesNotYieldBinaryFiles()
    {
        // Create a .py file that contains null bytes (binary disguised as Python)
        CreateFile("binary.py", new byte[] { 0x64, 0x65, 0x66, 0x00, 0x00 });
        CreateFile("valid.py", "def hello(): pass"u8.ToArray());

        var walker = new DirectoryWalker(_registry, _tempDir);
        var files = walker.Walk().Select(f => Path.GetFileName(f.Path)).ToList();

        Assert.Contains("valid.py", files);
        Assert.DoesNotContain("binary.py", files);
    }
}
