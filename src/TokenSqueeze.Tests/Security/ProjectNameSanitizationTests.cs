using TokenSqueeze.Indexing;

namespace TokenSqueeze.Tests.Security;

public sealed class ProjectNameSanitizationTests
{
    [Theory]
    [InlineData("../../../etc", "etc")]
    [InlineData("my project", "my-project")]
    [InlineData("foo/bar\\baz", "foo-bar-baz")]
    [InlineData("...", "unnamed")]
    [InlineData("valid-name", "valid-name")]
    [InlineData("", "unnamed")]
    [InlineData("a--b", "a-b")]
    [InlineData("...hidden", "hidden")]
    [InlineData("name.", "name")]
    [InlineData("-leading", "leading")]
    [InlineData("trailing-", "trailing")]
    [InlineData("hello world!", "hello-world")]
    [InlineData("../../windows/system32", "windows-system32")]
    public void SanitizeName_ReturnsExpectedResult(string input, string expected)
    {
        var result = ProjectIndexer.SanitizeName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeName_PreservesUnderscoresAndDots()
    {
        var result = ProjectIndexer.SanitizeName("my_project.v2");
        Assert.Equal("my_project.v2", result);
    }
}
