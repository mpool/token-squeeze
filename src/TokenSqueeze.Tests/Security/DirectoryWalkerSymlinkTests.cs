namespace TokenSqueeze.Tests.Security;

[Collection("CLI")]
public sealed class DirectoryWalkerSymlinkTests
{
    [Fact]
    public void SymlinkEscapeCheck_Appears_Before_ReadAllBytes()
    {
        // Structural test: read DirectoryWalker.cs and verify IsSymlinkEscape
        // is checked BEFORE File.ReadAllBytes in the source code.
        var sourceFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "TokenSqueeze", "Indexing", "DirectoryWalker.cs");

        // Resolve to handle .. segments
        sourceFile = Path.GetFullPath(sourceFile);
        Assert.True(File.Exists(sourceFile), $"Source file not found: {sourceFile}");

        var source = File.ReadAllText(sourceFile);
        var symlinkIndex = source.IndexOf("IsSymlinkEscape", StringComparison.Ordinal);
        var readBytesIndex = source.IndexOf("ReadAllBytes", StringComparison.Ordinal);

        Assert.True(symlinkIndex >= 0, "IsSymlinkEscape not found in DirectoryWalker.cs");
        Assert.True(readBytesIndex >= 0, "ReadAllBytes not found in DirectoryWalker.cs");
        Assert.True(symlinkIndex < readBytesIndex,
            $"IsSymlinkEscape (pos {symlinkIndex}) must appear BEFORE ReadAllBytes (pos {readBytesIndex}) in DirectoryWalker.cs");
    }

    [Fact]
    public void WalkDirectory_Yields_NonSymlink_Files()
    {
        // Ensure normal (non-symlink) files are still yielded by the walker.
        // Create a temp directory with a simple .cs file and walk it.
        var tempDir = Path.Combine(Path.GetTempPath(), "ts-symlink-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var testFile = Path.Combine(tempDir, "test.cs");
            File.WriteAllText(testFile, "class Foo {}");

            var registry = new TokenSqueeze.Parser.LanguageRegistry();
            var walker = new TokenSqueeze.Indexing.DirectoryWalker(registry, tempDir);
            var files = walker.Walk().ToList();

            Assert.Single(files);
            Assert.Contains("test.cs", files[0].Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
