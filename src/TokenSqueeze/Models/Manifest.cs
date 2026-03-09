namespace TokenSqueeze.Models;

public sealed record Manifest
{
    public required int FormatVersion { get; init; }
    public required string ProjectName { get; init; }
    public required string SourcePath { get; init; }
    public required DateTime IndexedAt { get; init; }
    public required Dictionary<string, ManifestFileEntry> Files { get; init; }
}

public sealed record ManifestFileEntry
{
    public required string Path { get; init; }
    public required string Hash { get; init; }
    public required string Language { get; init; }
    public required int SymbolCount { get; init; }
    public required string StorageKey { get; init; }
    public DateTime? LastModifiedUtc { get; init; }
}
