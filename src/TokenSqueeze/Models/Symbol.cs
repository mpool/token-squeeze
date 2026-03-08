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

    public static string MakeId(string filePath, string qualifiedName, SymbolKind kind)
        => $"{filePath}::{qualifiedName}#{kind}";
}
