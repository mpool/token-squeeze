using TokenSqueeze.Models;

namespace TokenSqueeze.Tests.Models;

public sealed class ManifestTests
{
    [Fact]
    public void Manifest_HasRequiredProperties()
    {
        var manifest = new Manifest
        {
            FormatVersion = 2,
            ProjectName = "test",
            SourcePath = "/tmp/test",
            IndexedAt = DateTime.UtcNow,
            Files = new Dictionary<string, ManifestFileEntry>()
        };

        Assert.Equal(2, manifest.FormatVersion);
        Assert.Equal("test", manifest.ProjectName);
        Assert.NotNull(manifest.Files);
    }

    [Fact]
    public void ManifestFileEntry_HasStorageKey()
    {
        var entry = new ManifestFileEntry
        {
            Path = "src/main.cs",
            Hash = "abc123",
            Language = "C#",
            SymbolCount = 5,
            StorageKey = "src-main-cs"
        };

        Assert.Equal("src-main-cs", entry.StorageKey);
    }

    [Fact]
    public void FileSymbolData_HasFileAndSymbols()
    {
        var data = new FileSymbolData
        {
            File = "src/main.cs",
            Symbols = new List<Symbol>()
        };

        Assert.Equal("src/main.cs", data.File);
        Assert.Empty(data.Symbols);
    }
}
