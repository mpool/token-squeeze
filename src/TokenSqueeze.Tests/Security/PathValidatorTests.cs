using System.Security;
using TokenSqueeze.Security;

namespace TokenSqueeze.Tests.Security;

public sealed class PathValidatorTests : IDisposable
{
    private readonly string _tempRoot;

    public PathValidatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ts-pathval-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("foo/../../..")]
    public void ValidateWithinRoot_RejectsTraversalPaths(string maliciousRelative)
    {
        var maliciousPath = Path.Combine(_tempRoot, maliciousRelative);

        Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateWithinRoot(maliciousPath, _tempRoot));
    }

    [Fact]
    public void ValidateWithinRoot_AcceptsValidSubdirectory()
    {
        var subDir = Path.Combine(_tempRoot, "valid-project");
        Directory.CreateDirectory(subDir);

        var result = PathValidator.ValidateWithinRoot(subDir, _tempRoot);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result);
    }

    [Fact]
    public void ValidateWithinRoot_AcceptsRootItself()
    {
        var result = PathValidator.ValidateWithinRoot(_tempRoot, _tempRoot);

        Assert.Equal(Path.GetFullPath(_tempRoot), result);
    }

    [Fact]
    public void IsSymlinkEscape_ReturnsFalseForRegularFile()
    {
        var filePath = Path.Combine(_tempRoot, "regular.txt");
        File.WriteAllText(filePath, "test content");

        var result = PathValidator.IsSymlinkEscape(filePath, _tempRoot);

        Assert.False(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
