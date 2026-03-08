namespace TokenSqueeze.Models;

public sealed record IndexedFile
{
    public required string Path { get; init; }
    public required string Hash { get; init; }
    public required string Language { get; init; }
    public required int SymbolCount { get; init; }
}
