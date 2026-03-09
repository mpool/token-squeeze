namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class ListCommandProjectionTests
{
    [Fact]
    public void ListCommand_HasProjectToJsonLocalFunction_NoDuplicateProjection()
    {
        // Structural test: verify ListCommand.cs contains a ProjectToJson local function
        // and does NOT have duplicate anonymous type projection blocks
        var sourceFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "TokenSqueeze", "Commands", "ListCommand.cs");

        Assert.True(File.Exists(sourceFile), $"ListCommand.cs not found at {sourceFile}");

        var source = File.ReadAllText(sourceFile);

        // Must have ProjectToJson local function
        Assert.Contains("ProjectToJson", source);

        // The anonymous type block (name = m.ProjectName) should appear exactly once
        // (inside the local function definition, not duplicated in the calling code)
        var projectionCount = CountOccurrences(source, "name = m.ProjectName");
        Assert.Equal(1, projectionCount);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
