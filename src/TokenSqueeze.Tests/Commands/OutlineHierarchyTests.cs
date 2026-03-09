using TokenSqueeze.Models;

namespace TokenSqueeze.Tests.Commands;

public sealed class OutlineHierarchyTests
{
    private static Symbol MakeSymbol(string file, string name, string qualifiedName, SymbolKind kind, string? parentId = null) =>
        new()
        {
            Id = Symbol.MakeId(file, qualifiedName, kind),
            File = file,
            Name = name,
            QualifiedName = qualifiedName,
            Kind = kind,
            Language = "C#",
            Signature = $"{kind} {name}",
            Parent = parentId,
            Line = 1,
            EndLine = 10
        };

    [Fact]
    public void Children_are_nested_under_parent_by_id()
    {
        // Arrange: a class and a method whose Parent is the class's full Id
        var classSymbol = MakeSymbol("file.cs", "Calculator", "Calculator", SymbolKind.Class);
        var methodSymbol = MakeSymbol("file.cs", "Add", "Calculator.Add", SymbolKind.Method, parentId: classSymbol.Id);

        var fileSymbols = new List<Symbol> { classSymbol, methodSymbol };

        // Replicate OutlineCommand hierarchy logic
        var roots = fileSymbols.Where(s => s.Parent is null).ToList();
        var childrenByParent = fileSymbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Act: look up children using root.Id (the correct approach)
        var found = childrenByParent.TryGetValue(roots[0].Id, out var kids);

        // Assert
        Assert.True(found, "Children should be found when looking up by root.Id");
        Assert.NotNull(kids);
        Assert.Single(kids);
        Assert.Equal("Add", kids[0].Name);
    }

    [Fact]
    public void Symbols_with_no_parent_appear_as_roots_with_no_children()
    {
        var func1 = MakeSymbol("file.py", "main", "main", SymbolKind.Function);
        var func2 = MakeSymbol("file.py", "helper", "helper", SymbolKind.Function);

        var fileSymbols = new List<Symbol> { func1, func2 };

        var roots = fileSymbols.Where(s => s.Parent is null).ToList();
        var childrenByParent = fileSymbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        Assert.Equal(2, roots.Count);
        Assert.False(childrenByParent.TryGetValue(roots[0].Id, out _));
        Assert.False(childrenByParent.TryGetValue(roots[1].Id, out _));
    }

    [Fact]
    public void Root_with_no_matching_children_has_empty_lookup()
    {
        var classSymbol = MakeSymbol("file.cs", "EmptyClass", "EmptyClass", SymbolKind.Class);
        var fileSymbols = new List<Symbol> { classSymbol };

        var roots = fileSymbols.Where(s => s.Parent is null).ToList();
        var childrenByParent = fileSymbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        Assert.Single(roots);
        Assert.False(childrenByParent.TryGetValue(roots[0].Id, out _));
    }

    [Fact]
    public void Name_based_lookup_fails_to_find_children()
    {
        // This test proves the bug: using root.Name instead of root.Id fails
        var classSymbol = MakeSymbol("file.cs", "Calculator", "Calculator", SymbolKind.Class);
        var methodSymbol = MakeSymbol("file.cs", "Add", "Calculator.Add", SymbolKind.Method, parentId: classSymbol.Id);

        var fileSymbols = new List<Symbol> { classSymbol, methodSymbol };

        var childrenByParent = fileSymbols
            .Where(s => s.Parent is not null)
            .GroupBy(s => s.Parent!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var roots = fileSymbols.Where(s => s.Parent is null).ToList();

        // Using root.Name (the buggy approach) -- should NOT find children
        var foundByName = childrenByParent.TryGetValue(roots[0].Name, out _);
        Assert.False(foundByName, "Looking up by root.Name should fail (this is the bug)");
    }
}
