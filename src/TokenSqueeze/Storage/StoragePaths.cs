namespace TokenSqueeze.Storage;

internal static class StoragePaths
{
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".token-squeeze",
        "projects");

    public static string GetProjectDir(string projectName)
        => Path.Combine(RootDir, projectName);

    public static string GetIndexPath(string projectName)
        => Path.Combine(GetProjectDir(projectName), "index.json");

    public static void EnsureRootExists()
        => Directory.CreateDirectory(RootDir);
}
