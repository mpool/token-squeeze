using TokenSqueeze.Security;

namespace TokenSqueeze.Tests.Security;

public sealed class SecretDetectorTests
{
    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData(".env.production")]
    [InlineData(".env.staging")]
    [InlineData(".env.development")]
    [InlineData("credentials.json")]
    [InlineData("service-account.json")]
    [InlineData("id_rsa")]
    [InlineData("id_ed25519")]
    [InlineData("id_ecdsa")]
    public void IsSecretFile_ReturnsTrueForKnownSecretNames(string fileName)
    {
        Assert.True(SecretDetector.IsSecretFile(fileName));
    }

    [Theory]
    [InlineData("cert.pem")]
    [InlineData("private.key")]
    [InlineData("certificate.pfx")]
    [InlineData("keystore.p12")]
    [InlineData("java.jks")]
    [InlineData("app.keystore")]
    public void IsSecretFile_ReturnsTrueForSecretExtensions(string fileName)
    {
        Assert.True(SecretDetector.IsSecretFile(fileName));
    }

    [Theory]
    [InlineData("Program.cs")]
    [InlineData("appsettings.json")]
    [InlineData("readme.md")]
    [InlineData("data.json")]
    [InlineData("script.sh")]
    [InlineData("index.html")]
    public void IsSecretFile_ReturnsFalseForSafeFiles(string fileName)
    {
        Assert.False(SecretDetector.IsSecretFile(fileName));
    }

    [Theory]
    [InlineData("src/config/.env")]
    [InlineData("deploy/certs/private.key")]
    [InlineData("home/.ssh/id_rsa")]
    public void IsSecretFile_DetectsSecretsInNestedPaths(string filePath)
    {
        Assert.True(SecretDetector.IsSecretFile(filePath));
    }

    [Theory]
    [InlineData("src/Models/User.cs")]
    [InlineData("config/appsettings.json")]
    public void IsSecretFile_ReturnsFalseForSafeNestedPaths(string filePath)
    {
        Assert.False(SecretDetector.IsSecretFile(filePath));
    }

    [Theory]
    [InlineData(".env.custom")]
    [InlineData(".env.myapp")]
    [InlineData("client_secrets.json")]
    [InlineData("my-secret-config.yaml")]
    [InlineData("secrets.env")]
    [InlineData("user_credentials.xml")]
    [InlineData("credentials.bak")]
    [InlineData("secretary.txt")]
    public void IsSecretFile_ReturnsTrueForPatternMatches(string fileName)
    {
        Assert.True(SecretDetector.IsSecretFile(fileName));
    }

    [Theory]
    [InlineData("environment.cs")]
    [InlineData("discrete.py")]
    [InlineData("incremental.js")]
    [InlineData("MyService.cs")]
    public void IsSecretFile_ReturnsFalseForNonSecretFiles(string fileName)
    {
        Assert.False(SecretDetector.IsSecretFile(fileName));
    }
}
