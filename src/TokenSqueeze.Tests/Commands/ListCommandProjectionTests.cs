namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class ListCommandProjectionTests
{
    [Fact]
    public void ListCommand_UsesPrebuiltCatalog()
    {
        var sourceFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "TokenSqueeze", "Commands", "ListCommand.cs");

        Assert.True(File.Exists(sourceFile), $"ListCommand.cs not found at {sourceFile}");

        var source = File.ReadAllText(sourceFile);

        // Must use catalog, not manifest loading
        Assert.Contains("LoadCatalogJson", source);
        Assert.DoesNotContain("LoadManifest", source);
        Assert.DoesNotContain("ListProjects", source);
    }
}
