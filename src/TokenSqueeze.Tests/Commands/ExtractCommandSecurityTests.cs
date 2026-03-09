using System.Security;
using TokenSqueeze.Security;

namespace TokenSqueeze.Tests.Commands;

public sealed class ExtractCommandSecurityTests : IDisposable
{
    private readonly string _tempRoot;

    public ExtractCommandSecurityTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ts-extract-sec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config")]
    [InlineData("src/../../../../../../etc/shadow")]
    public void ValidateWithinRoot_RejectsTraversalInSymbolFile(string traversalFile)
    {
        // Simulates: var sourceFilePath = Path.Combine(index.SourcePath, symbol.File);
        // then: PathValidator.ValidateWithinRoot(sourceFilePath, index.SourcePath);
        var sourceFilePath = Path.Combine(_tempRoot, traversalFile);

        Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateWithinRoot(sourceFilePath, _tempRoot));
    }

    [Theory]
    [InlineData("src/Program.cs")]
    [InlineData("Models/Symbol.cs")]
    [InlineData("deep/nested/path/file.cs")]
    public void ValidateWithinRoot_AcceptsNormalRelativePaths(string normalFile)
    {
        var sourceFilePath = Path.Combine(_tempRoot, normalFile);

        // Should not throw
        var result = PathValidator.ValidateWithinRoot(sourceFilePath, _tempRoot);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
