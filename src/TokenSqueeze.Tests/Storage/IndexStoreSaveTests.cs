using System.Security;
using TokenSqueeze.Security;
using TokenSqueeze.Storage;

namespace TokenSqueeze.Tests.Storage;

public sealed class IndexStoreSaveTests : IDisposable
{
    private readonly string _tempRoot;

    public IndexStoreSaveTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ts-savetest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Theory]
    [InlineData("../../escape")]
    [InlineData("..\\..\\escape")]
    [InlineData("legit/../../../escape")]
    public void Save_ValidateWithinRoot_RejectsTraversalProjectName(string maliciousName)
    {
        // Simulates what IndexStore.Save does:
        // var projectDir = StoragePaths.GetProjectDir(index.ProjectName);
        // PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);
        var projectDir = Path.Combine(_tempRoot, maliciousName);

        Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateWithinRoot(projectDir, _tempRoot));
    }

    [Theory]
    [InlineData("normal-project")]
    [InlineData("my_project_123")]
    public void Save_ValidateWithinRoot_AcceptsNormalProjectName(string normalName)
    {
        var projectDir = Path.Combine(_tempRoot, normalName);

        var result = PathValidator.ValidateWithinRoot(projectDir, _tempRoot);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result);
    }

    [Theory]
    [InlineData("../../etc")]
    [InlineData("..\\..\\windows")]
    [InlineData("good/../../../escape")]
    public void Load_ValidateWithinRoot_RejectsTraversalProjectName(string maliciousName)
    {
        // Simulates what IndexStore.Load should do:
        // var projectDir = StoragePaths.GetProjectDir(projectName);
        // PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);
        var projectDir = Path.Combine(_tempRoot, maliciousName);

        Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateWithinRoot(projectDir, _tempRoot));
    }

    [Theory]
    [InlineData("normal-project")]
    [InlineData("another_project")]
    public void Load_ValidateWithinRoot_AcceptsNormalProjectName(string normalName)
    {
        var projectDir = Path.Combine(_tempRoot, normalName);

        var result = PathValidator.ValidateWithinRoot(projectDir, _tempRoot);

        Assert.StartsWith(Path.GetFullPath(_tempRoot), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
