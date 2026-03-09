using TokenSqueeze.Commands;

namespace TokenSqueeze.Tests.Commands;

public sealed class GlobToRegexTests
{
    [Fact]
    public void Partial_path_matches_with_directory_prefix()
    {
        // src/*.ts should match project/src/utils.ts
        var regex = FindCommand.GlobToRegex("src/*.ts");
        Assert.Matches(regex, "project/src/utils.ts");
    }

    [Fact]
    public void Partial_path_matches_exact_prefix()
    {
        // src/*.ts should match src/utils.ts
        var regex = FindCommand.GlobToRegex("src/*.ts");
        Assert.Matches(regex, "src/utils.ts");
    }

    [Fact]
    public void Single_star_does_not_cross_directories()
    {
        // src/*.ts should NOT match src/sub/deep.ts
        var regex = FindCommand.GlobToRegex("src/*.ts");
        Assert.DoesNotMatch(regex, "src/sub/deep.ts");
    }

    [Fact]
    public void Globstar_matches_deep_paths()
    {
        // **/*.ts should match any/deep/path/file.ts
        var regex = FindCommand.GlobToRegex("**/*.ts");
        Assert.Matches(regex, "any/deep/path/file.ts");
    }

    [Fact]
    public void Simple_star_matches_at_root()
    {
        // *.ts should match file.ts at root
        var regex = FindCommand.GlobToRegex("*.ts");
        Assert.Matches(regex, "file.ts");
    }

    [Fact]
    public void Simple_star_matches_partial_path()
    {
        // *.ts should match dir/file.ts (partial)
        var regex = FindCommand.GlobToRegex("*.ts");
        Assert.Matches(regex, "dir/file.ts");
    }

    [Fact]
    public void Backslash_normalized_to_forward_slash()
    {
        // src\*.ts should work the same as src/*.ts
        var regex = FindCommand.GlobToRegex("src\\*.ts");
        Assert.Matches(regex, "src/utils.ts");
    }

    [Fact]
    public void Rooted_pattern_matches_from_start()
    {
        // /src/*.ts should match src/utils.ts but not deep/src/utils.ts
        var regex = FindCommand.GlobToRegex("/src/*.ts");
        Assert.Matches(regex, "src/utils.ts");
        Assert.DoesNotMatch(regex, "deep/src/utils.ts");
    }

    [Fact]
    public void Globstar_at_start_matches_any_depth()
    {
        var regex = FindCommand.GlobToRegex("**/*.cs");
        Assert.Matches(regex, "src/Commands/FindCommand.cs");
        Assert.Matches(regex, "FindCommand.cs");
    }
}
