using TokenSqueeze.Models;

namespace TokenSqueeze.Parser;

public enum DocstringStrategy
{
    NextSiblingString,
    PrecedingComment
}

public sealed record LanguageSpec
{
    public required string LanguageId { get; init; }
    public required string DisplayName { get; init; }
    public required Dictionary<string, SymbolKind> SymbolNodeTypes { get; init; }
    public required Dictionary<string, string> NameFields { get; init; }
    public required Dictionary<string, string> ParamFields { get; init; }
    public required Dictionary<string, string> ReturnTypeFields { get; init; }
    public required DocstringStrategy DocstringStrategy { get; init; }
    public required HashSet<string> ContainerNodeTypes { get; init; }
    public required HashSet<string> ConstantPatterns { get; init; }
    public required HashSet<string> TypePatterns { get; init; }
    public required string[] Extensions { get; init; }
    public bool RequiresDeclaratorDrilling { get; init; }
}
