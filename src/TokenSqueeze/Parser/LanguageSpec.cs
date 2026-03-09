using TokenSqueeze.Models;
using TreeSitter;

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

    /// <summary>
    /// Validates cross-references between LanguageSpec dictionaries.
    /// Throws ArgumentException if any ContainerNodeTypes, ParamFields keys,
    /// or ReturnTypeFields keys are not present in SymbolNodeTypes.
    /// </summary>
    public static void Validate(LanguageSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.LanguageId))
            throw new ArgumentException($"LanguageId must not be empty.");

        if (string.IsNullOrWhiteSpace(spec.DisplayName))
            throw new ArgumentException($"[{spec.LanguageId}] DisplayName must not be empty.");

        if (spec.SymbolNodeTypes.Count == 0)
            throw new ArgumentException($"[{spec.LanguageId}] SymbolNodeTypes must not be empty.");

        if (spec.Extensions.Length == 0)
            throw new ArgumentException($"[{spec.LanguageId}] Extensions must not be empty.");

        foreach (var container in spec.ContainerNodeTypes)
        {
            if (!spec.SymbolNodeTypes.ContainsKey(container))
                throw new ArgumentException(
                    $"[{spec.LanguageId}] ContainerNodeTypes entry '{container}' is not a key in SymbolNodeTypes.");
        }

        foreach (var key in spec.ParamFields.Keys)
        {
            if (!spec.SymbolNodeTypes.ContainsKey(key))
                throw new ArgumentException(
                    $"[{spec.LanguageId}] ParamFields key '{key}' is not a key in SymbolNodeTypes.");
        }

        foreach (var key in spec.ReturnTypeFields.Keys)
        {
            if (!spec.SymbolNodeTypes.ContainsKey(key))
                throw new ArgumentException(
                    $"[{spec.LanguageId}] ReturnTypeFields key '{key}' is not a key in SymbolNodeTypes.");
        }
    }

    /// <summary>
    /// Extracts a constant name from a node matched by ConstantPatterns.
    /// Returns the constant name, or null if the node should be skipped.
    /// </summary>
    public Func<Node, LanguageSpec, Symbol?, string?>? ConstantExtractor { get; init; }

    /// <summary>
    /// Builds a display signature for a symbol.
    /// </summary>
    public Func<string, SymbolKind, string, string, string>? SignatureBuilder { get; init; }
}
