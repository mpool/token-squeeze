using System.Diagnostics;
using System.Text.RegularExpressions;
using TokenSqueeze.Commands;

namespace TokenSqueeze.Tests.Commands;

[Collection("CLI")]
public sealed class FindCommandGlobTests
{
    [Fact]
    public void GlobToRegex_RejectsGlobOver200Characters()
    {
        var longGlob = new string('a', 201);
        Assert.Throws<ArgumentException>(() => FindCommand.GlobToRegex(longGlob));
    }

    [Fact]
    public void GlobToRegex_MaliciousPattern_CompletesWithinBoundedTime()
    {
        // This pattern could cause catastrophic backtracking in naive regex engines
        var malicious = "a]]]]]]]]]]]]]]]]]]]]]";
        var sw = Stopwatch.StartNew();

        // Should either complete quickly or throw ArgumentException
        var regex = FindCommand.GlobToRegex(malicious);

        // Try matching against a test string — the regex timeout should protect us
        var testString = "a" + new string(']', 50) + ".cs";
        try
        {
            regex.IsMatch(testString);
        }
        catch (RegexMatchTimeoutException)
        {
            // Timeout is acceptable — it means the protection works
        }

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"GlobToRegex with malicious pattern took {sw.ElapsedMilliseconds}ms (expected < 2000ms)");
    }

    [Fact]
    public void GlobToRegex_VeryLongGlob_IsRejected()
    {
        var longGlob = string.Concat(Enumerable.Repeat("**/", 100)) + "*.cs";
        Assert.True(longGlob.Length > 200);
        Assert.Throws<ArgumentException>(() => FindCommand.GlobToRegex(longGlob));
    }

    [Fact]
    public void GlobToRegex_NormalGlobStarStar_StillWorks()
    {
        var regex = FindCommand.GlobToRegex("**/*.cs");
        Assert.Matches(regex, "src/Commands/FindCommand.cs");
        Assert.Matches(regex, "FindCommand.cs");
    }

    [Fact]
    public void GlobToRegex_NormalGlob_StillWorks()
    {
        var regex = FindCommand.GlobToRegex("src/*.ts");
        Assert.Matches(regex, "src/utils.ts");
        Assert.DoesNotMatch(regex, "src/sub/deep.ts");
    }

    [Fact]
    public void GlobToRegex_AcceptsGlobAt200Characters()
    {
        // Exactly 200 chars should be accepted
        var glob = new string('a', 196) + ".cs*";
        Assert.Equal(200, glob.Length);
        var regex = FindCommand.GlobToRegex(glob); // should not throw
        Assert.NotNull(regex);
    }
}
