namespace TokenSqueeze.Models;

public sealed record CodeIndex
{
    public required string SourcePath { get; init; }
    public required DateTime IndexedAt { get; init; }
    public required Dictionary<string, IndexedFile> Files { get; init; }
    public required List<Symbol> Symbols { get; init; }
}
