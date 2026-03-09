using TokenSqueeze.Models;
using TokenSqueeze.Parser;

namespace TokenSqueeze.Tests;

public sealed class LanguageSpecValidationTests
{
    private static LanguageSpec MakeMinimalSpec(Action<Dictionary<string, SymbolKind>>? symbolMutator = null,
        Action<HashSet<string>>? containerMutator = null,
        Action<Dictionary<string, string>>? paramMutator = null,
        Action<Dictionary<string, string>>? returnTypeMutator = null,
        string languageId = "TestLang",
        string[]? extensions = null)
    {
        var symbols = new Dictionary<string, SymbolKind>
        {
            ["function_definition"] = SymbolKind.Function,
            ["class_definition"] = SymbolKind.Class,
        };
        symbolMutator?.Invoke(symbols);

        var containers = new HashSet<string> { "class_definition" };
        containerMutator?.Invoke(containers);

        var paramFields = new Dictionary<string, string>
        {
            ["function_definition"] = "parameters",
        };
        paramMutator?.Invoke(paramFields);

        var returnTypeFields = new Dictionary<string, string>
        {
            ["function_definition"] = "return_type",
        };
        returnTypeMutator?.Invoke(returnTypeFields);

        return new LanguageSpec
        {
            LanguageId = languageId,
            DisplayName = "Test Language",
            SymbolNodeTypes = symbols,
            NameFields = new Dictionary<string, string>
            {
                ["function_definition"] = "name",
                ["class_definition"] = "name",
            },
            ParamFields = paramFields,
            ReturnTypeFields = returnTypeFields,
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = containers,
            ConstantPatterns = [],
            TypePatterns = [],
            Extensions = extensions ?? [".test"],
        };
    }

    [Fact]
    public void LanguageSpecValidation_EmptyLanguageId_Throws()
    {
        var spec = MakeMinimalSpec(languageId: "");
        var ex = Assert.Throws<ArgumentException>(() => LanguageSpec.Validate(spec));
        Assert.Contains("LanguageId", ex.Message);
    }

    [Fact]
    public void LanguageSpecValidation_EmptySymbolNodeTypes_Throws()
    {
        var spec = MakeMinimalSpec(
            symbolMutator: s => s.Clear(),
            containerMutator: c => c.Clear(),
            paramMutator: p => p.Clear(),
            returnTypeMutator: r => r.Clear());
        var ex = Assert.Throws<ArgumentException>(() => LanguageSpec.Validate(spec));
        Assert.Contains("SymbolNodeTypes", ex.Message);
    }

    [Fact]
    public void LanguageSpecValidation_EmptyExtensions_Throws()
    {
        var spec = MakeMinimalSpec(extensions: []);
        var ex = Assert.Throws<ArgumentException>(() => LanguageSpec.Validate(spec));
        Assert.Contains("Extensions", ex.Message);
    }

    [Fact]
    public void LanguageSpecValidation_ContainerNotInSymbolNodes_Throws()
    {
        var spec = MakeMinimalSpec(containerMutator: c =>
        {
            c.Clear();
            c.Add("class_declaraton"); // intentional typo
        });
        var ex = Assert.Throws<ArgumentException>(() => LanguageSpec.Validate(spec));
        Assert.Contains("class_declaraton", ex.Message);
        Assert.Contains("ContainerNodeTypes", ex.Message);
    }

    [Fact]
    public void LanguageSpecValidation_ParamFieldKeyNotInSymbolNodes_Throws()
    {
        var spec = MakeMinimalSpec(paramMutator: p =>
        {
            p.Clear();
            p["nonexistent_node"] = "parameters";
        });
        var ex = Assert.Throws<ArgumentException>(() => LanguageSpec.Validate(spec));
        Assert.Contains("nonexistent_node", ex.Message);
        Assert.Contains("ParamFields", ex.Message);
    }

    [Fact]
    public void LanguageSpecValidation_ReturnTypeFieldKeyNotInSymbolNodes_Throws()
    {
        var spec = MakeMinimalSpec(returnTypeMutator: r =>
        {
            r.Clear();
            r["nonexistent_node"] = "return_type";
        });
        var ex = Assert.Throws<ArgumentException>(() => LanguageSpec.Validate(spec));
        Assert.Contains("nonexistent_node", ex.Message);
        Assert.Contains("ReturnTypeFields", ex.Message);
    }

    [Fact]
    public void LanguageSpecValidation_ValidSpec_DoesNotThrow()
    {
        var spec = MakeMinimalSpec();
        var exception = Record.Exception(() => LanguageSpec.Validate(spec));
        Assert.Null(exception);
    }

    [Fact]
    public void LanguageSpecValidation_AllBuiltInSpecs_PassValidation()
    {
        // If any built-in spec fails validation, the constructor throws
        var exception = Record.Exception(() =>
        {
            using var registry = new LanguageRegistry();
        });
        Assert.Null(exception);
    }
}
