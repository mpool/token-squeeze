namespace TokenSqueeze.Models;

public sealed record ProjectMetadata
{
    public required string ProjectName { get; init; }
    public required string SourcePath { get; init; }
    public required int FileCount { get; init; }
    public required int SymbolCount { get; init; }
    public required List<string> Languages { get; init; }
    public required DateTime IndexedAt { get; init; }
}
