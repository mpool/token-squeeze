using System.Security.Cryptography;
using System.Text;

namespace TokenSqueeze.Storage;

internal static class StoragePaths
{
    public static string GetManifestPath(string cacheDir)
        => Path.Combine(cacheDir, "manifest.json");

    public static string GetFilesDir(string cacheDir)
        => Path.Combine(cacheDir, "files");

    public static string GetFileFragmentPath(string cacheDir, string storageKey)
        => Path.Combine(GetFilesDir(cacheDir), storageKey + ".json");

    public static string GetSearchIndexPath(string cacheDir)
        => Path.Combine(cacheDir, "search-index.json");

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
}
