namespace TokenSqueeze.Models;

public sealed record SearchSymbol
{
    public required string Id { get; init; }
    public required string File { get; init; }
    public required string Name { get; init; }
    public required string QualifiedName { get; init; }
    public required SymbolKind Kind { get; init; }
    public required string Language { get; init; }
    public required string Signature { get; init; }
    public string Docstring { get; init; } = "";
    public string? Parent { get; init; }
    public int Line { get; init; }
    public int EndLine { get; init; }
}
