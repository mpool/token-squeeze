using System.Security;
using TokenSqueeze.Security;

namespace TokenSqueeze.Tests.Storage;

public sealed class IndexStoreDeleteTests : IDisposable
{
    private readonly string _tempRoot;

    public IndexStoreDeleteTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ts-deltest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void ValidateWithinRoot_ThrowsForTraversalPath()
    {
        var maliciousPath = Path.Combine(_tempRoot, "..", "..", "etc");

        Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateWithinRoot(maliciousPath, _tempRoot));
    }

    [Fact]
    public void ValidateWithinRoot_AcceptsValidSubpath()
    {
        var validPath = Path.Combine(_tempRoot, "my-project");
        Directory.CreateDirectory(validPath);

        var result = PathValidator.ValidateWithinRoot(validPath, _tempRoot);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
