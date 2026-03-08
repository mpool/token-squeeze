namespace TokenSqueeze.Security;

internal static class SecretDetector
{
    private static readonly HashSet<string> SecretFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        ".env.local",
        ".env.production",
        ".env.staging",
        ".env.development",
        "credentials.json",
        "service-account.json",
        "id_rsa",
        "id_ed25519",
        "id_ecdsa"
    };

    private static readonly string[] SecretExtensions =
    [
        ".pem",
        ".key",
        ".pfx",
        ".p12",
        ".jks",
        ".keystore"
    ];

    public static bool IsSecretFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (SecretFileNames.Contains(fileName))
            return true;

        var extension = Path.GetExtension(filePath);
        return SecretExtensions.Any(ext =>
            string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
    }
}
