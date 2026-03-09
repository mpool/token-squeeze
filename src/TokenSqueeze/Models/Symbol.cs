using System.Text.Json.Serialization;

namespace TokenSqueeze.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SymbolKind
{
    Function,
    Class,
    Method,
    Constant,
    Type
}

public sealed record Symbol
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
    public int ByteOffset { get; init; }
    public int ByteLength { get; init; }
    public string ContentHash { get; init; } = "";

    public SearchSymbol ToSearchSymbol() => new()
    {
        Id = Id,
        File = File,
        Name = Name,
        QualifiedName = QualifiedName,
        Kind = Kind,
        Language = Language,
        Signature = Signature,
        Docstring = Docstring,
        Parent = Parent,
        Line = Line,
        EndLine = EndLine
    };

    public static string MakeId(string filePath, string qualifiedName, SymbolKind kind)
        => $"{filePath}::{qualifiedName}#{kind}";

    public static (string FilePath, string QualifiedName, string Kind)? ParseId(string id)
    {
        var separatorIndex = id.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex < 0)
            return null;

        var rest = id[(separatorIndex + 2)..];
        var hashIndex = rest.LastIndexOf('#');
        if (hashIndex < 0)
            return (id[..separatorIndex], rest, "");

        return (id[..separatorIndex], rest[..hashIndex], rest[(hashIndex + 1)..]);
    }
}
