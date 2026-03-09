using TokenSqueeze.Models;

namespace TokenSqueeze.Tests.Helpers;

public static class TestIndexBuilder
{
    public static CodeIndex Create(string projectName, string sourcePath, params Symbol[] symbols)
    {
        var symbolList = symbols.ToList();
        var files = symbolList
            .GroupBy(s => s.File)
            .ToDictionary(
                g => g.Key,
                g => new IndexedFile
                {
                    Path = g.Key,
                    Hash = "fakehash",
                    Language = g.First().Language,
                    SymbolCount = g.Count()
                });

        return new CodeIndex
        {
            ProjectName = projectName,
            SourcePath = sourcePath,
            IndexedAt = DateTime.UtcNow,
            Files = files,
            Symbols = symbolList
        };
    }

    public static Symbol MakeSymbol(
        string name,
        string file = "test.py",
        SymbolKind kind = SymbolKind.Function,
        string? qualifiedName = null,
        string? signature = null,
        string docstring = "",
        string language = "Python")
    {
        var qn = qualifiedName ?? name;
        return new Symbol
        {
            Id = Symbol.MakeId(file, qn, kind),
            File = file,
            Name = name,
            QualifiedName = qn,
            Kind = kind,
            Language = language,
            Signature = signature ?? $"{name}()",
            Docstring = docstring,
            Line = 1,
            EndLine = 5,
            ByteOffset = 0,
            ByteLength = 50,
            ContentHash = "testhash"
        };
    }
}
