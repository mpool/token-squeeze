using System.Security.Cryptography;
using System.Text;

namespace TokenSqueeze.Storage;

internal static class StoragePaths
{
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static string? TestRootOverride;

    public static string RootDir => TestRootOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".token-squeeze",
        "projects");

    public static string CatalogPath => Path.Combine(
        Path.GetDirectoryName(RootDir)!,
        "catalog.json");

    public static string GetProjectDir(string projectName)
        => Path.Combine(RootDir, projectName);

    /// <summary>
    /// Returns the path to the old monolithic index.json format.
    /// Used only for legacy detection, migration, and cleanup.
    /// Current format uses manifest.json + search-index.json + per-file fragments.
    /// </summary>
    public static string GetLegacyIndexPath(string projectName)
        => Path.Combine(GetProjectDir(projectName), "index.json");

    public static string GetMetadataPath(string projectName)
        => Path.Combine(GetProjectDir(projectName), "metadata.json");

    public static string GetManifestPath(string projectName)
        => Path.Combine(GetProjectDir(projectName), "manifest.json");

    public static string GetFilesDir(string projectName)
        => Path.Combine(GetProjectDir(projectName), "files");

    public static string GetFileFragmentPath(string projectName, string storageKey)
        => Path.Combine(GetFilesDir(projectName), storageKey + ".json");

    public static string GetSearchIndexPath(string projectName)
        => Path.Combine(GetProjectDir(projectName), "search-index.json");

    private const int MaxStorageKeyLength = 200;

    public static string PathToStorageKey(string relativePath)
    {
        var key = relativePath
            .Replace('/', '-')
            .Replace('\\', '-')
            .TrimStart('-', '.');

        if (string.IsNullOrEmpty(key))
            return "_empty";

        // SEC-05: Truncate and hash keys that exceed filesystem limits
        if (key.Length > MaxStorageKeyLength)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            var hashHex = Convert.ToHexString(hash)[..16].ToLowerInvariant();
            return key[..100] + "-" + hashHex;
        }

        return key;
    }

    public static void EnsureRootExists()
        => Directory.CreateDirectory(RootDir);
}
