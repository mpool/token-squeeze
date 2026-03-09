namespace TokenSqueeze.Models;

public sealed record FileSymbolData
{
    public required string File { get; init; }
    public required List<Symbol> Symbols { get; init; }
}
