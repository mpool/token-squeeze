using TokenSqueeze.Models;
using TreeSitter;

namespace TokenSqueeze.Parser;

public sealed class LanguageRegistry : IDisposable
{
    private readonly Dictionary<string, LanguageSpec> _specsByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LanguageSpec> _allSpecs = [];
    private readonly Dictionary<string, Language> _languages = [];
    private readonly Dictionary<string, TreeSitter.Parser> _parsers = [];
    private bool _disposed;

    public LanguageRegistry()
    {
        RegisterPython();
        RegisterJavaScript();
        RegisterTypeScript();
        RegisterTsx();
        RegisterCSharp();
        RegisterC();
        RegisterCpp();
    }

    public LanguageSpec? GetSpecForExtension(string ext)
    {
        return _specsByExtension.TryGetValue(ext, out var spec) ? spec : null;
    }

    public IReadOnlyList<LanguageSpec> GetAllSpecs() => _allSpecs;

    public TreeSitter.Parser GetOrCreateParser(string languageId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_parsers.TryGetValue(languageId, out var parser))
            return parser;

        var language = new Language(languageId);
        _languages[languageId] = language;

        parser = new TreeSitter.Parser(language);
        _parsers[languageId] = parser;

        return parser;
    }

    private void Register(LanguageSpec spec)
    {
        _allSpecs.Add(spec);
        foreach (var ext in spec.Extensions)
            _specsByExtension[ext] = spec;
    }

    private void RegisterPython()
    {
        Register(new LanguageSpec
        {
            LanguageId = "Python",
            DisplayName = "Python",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["function_definition"] = SymbolKind.Function,
                ["class_definition"] = SymbolKind.Class,
            },
            NameFields = new Dictionary<string, string>
            {
                ["function_definition"] = "name",
                ["class_definition"] = "name",
            },
            ParamFields = new Dictionary<string, string>
            {
                ["function_definition"] = "parameters",
            },
            ReturnTypeFields = new Dictionary<string, string>
            {
                ["function_definition"] = "return_type",
            },
            DocstringStrategy = DocstringStrategy.NextSiblingString,
            ContainerNodeTypes = ["class_definition"],
            ConstantPatterns = ["assignment"],
            TypePatterns = ["type_alias_statement"],
            Extensions = [".py"],
        });
    }

    private void RegisterJavaScript()
    {
        Register(new LanguageSpec
        {
            LanguageId = "JavaScript",
            DisplayName = "JavaScript",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["function_declaration"] = SymbolKind.Function,
                ["class_declaration"] = SymbolKind.Class,
                ["method_definition"] = SymbolKind.Method,
            },
            NameFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "name",
                ["class_declaration"] = "name",
                ["method_definition"] = "name",
            },
            ParamFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "parameters",
                ["method_definition"] = "parameters",
            },
            ReturnTypeFields = new Dictionary<string, string>(),
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = ["class_declaration"],
            ConstantPatterns = ["lexical_declaration"],
            TypePatterns = [],
            Extensions = [".js", ".jsx"],
        });
    }

    private void RegisterTypeScript()
    {
        Register(new LanguageSpec
        {
            LanguageId = "TypeScript",
            DisplayName = "TypeScript",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["function_declaration"] = SymbolKind.Function,
                ["class_declaration"] = SymbolKind.Class,
                ["method_definition"] = SymbolKind.Method,
            },
            NameFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "name",
                ["class_declaration"] = "name",
                ["method_definition"] = "name",
                ["interface_declaration"] = "name",
                ["type_alias_declaration"] = "name",
                ["enum_declaration"] = "name",
            },
            ParamFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "parameters",
                ["method_definition"] = "parameters",
            },
            ReturnTypeFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "return_type",
                ["method_definition"] = "return_type",
            },
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = ["class_declaration"],
            ConstantPatterns = ["lexical_declaration"],
            TypePatterns = ["interface_declaration", "type_alias_declaration", "enum_declaration"],
            Extensions = [".ts"],
        });
    }

    private void RegisterTsx()
    {
        Register(new LanguageSpec
        {
            LanguageId = "Tsx",
            DisplayName = "TypeScript (TSX)",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["function_declaration"] = SymbolKind.Function,
                ["class_declaration"] = SymbolKind.Class,
                ["method_definition"] = SymbolKind.Method,
            },
            NameFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "name",
                ["class_declaration"] = "name",
                ["method_definition"] = "name",
                ["interface_declaration"] = "name",
                ["type_alias_declaration"] = "name",
                ["enum_declaration"] = "name",
            },
            ParamFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "parameters",
                ["method_definition"] = "parameters",
            },
            ReturnTypeFields = new Dictionary<string, string>
            {
                ["function_declaration"] = "return_type",
                ["method_definition"] = "return_type",
            },
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = ["class_declaration"],
            ConstantPatterns = ["lexical_declaration"],
            TypePatterns = ["interface_declaration", "type_alias_declaration", "enum_declaration"],
            Extensions = [".tsx"],
        });
    }

    private void RegisterCSharp()
    {
        Register(new LanguageSpec
        {
            LanguageId = "C-Sharp",
            DisplayName = "C#",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["class_declaration"] = SymbolKind.Class,
                ["record_declaration"] = SymbolKind.Class,
                ["method_declaration"] = SymbolKind.Method,
                ["constructor_declaration"] = SymbolKind.Method,
            },
            NameFields = new Dictionary<string, string>
            {
                ["class_declaration"] = "name",
                ["record_declaration"] = "name",
                ["method_declaration"] = "name",
                ["constructor_declaration"] = "name",
                ["interface_declaration"] = "name",
                ["enum_declaration"] = "name",
                ["struct_declaration"] = "name",
                ["delegate_declaration"] = "name",
            },
            ParamFields = new Dictionary<string, string>
            {
                ["method_declaration"] = "parameters",
                ["constructor_declaration"] = "parameters",
                ["delegate_declaration"] = "parameters",
            },
            ReturnTypeFields = new Dictionary<string, string>
            {
                ["method_declaration"] = "returns",
            },
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = ["class_declaration", "struct_declaration", "record_declaration", "interface_declaration"],
            ConstantPatterns = [],
            TypePatterns = ["interface_declaration", "enum_declaration", "struct_declaration", "delegate_declaration"],
            Extensions = [".cs"],
        });
    }

    private void RegisterC()
    {
        Register(new LanguageSpec
        {
            LanguageId = "C",
            DisplayName = "C",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["function_definition"] = SymbolKind.Function,
            },
            NameFields = new Dictionary<string, string>
            {
                ["struct_specifier"] = "name",
                ["enum_specifier"] = "name",
                ["union_specifier"] = "name",
            },
            ParamFields = new Dictionary<string, string>(),
            ReturnTypeFields = new Dictionary<string, string>
            {
                ["function_definition"] = "type",
            },
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = [],
            ConstantPatterns = ["preproc_def"],
            TypePatterns = ["struct_specifier", "enum_specifier", "union_specifier", "type_definition"],
            Extensions = [".c", ".h"],
            RequiresDeclaratorDrilling = true,
        });
    }

    private void RegisterCpp()
    {
        Register(new LanguageSpec
        {
            LanguageId = "Cpp",
            DisplayName = "C++",
            SymbolNodeTypes = new Dictionary<string, SymbolKind>
            {
                ["function_definition"] = SymbolKind.Function,
                ["class_specifier"] = SymbolKind.Class,
            },
            NameFields = new Dictionary<string, string>
            {
                ["class_specifier"] = "name",
                ["struct_specifier"] = "name",
                ["enum_specifier"] = "name",
                ["union_specifier"] = "name",
                ["alias_declaration"] = "name",
            },
            ParamFields = new Dictionary<string, string>(),
            ReturnTypeFields = new Dictionary<string, string>
            {
                ["function_definition"] = "type",
            },
            DocstringStrategy = DocstringStrategy.PrecedingComment,
            ContainerNodeTypes = ["class_specifier", "struct_specifier", "union_specifier"],
            ConstantPatterns = ["preproc_def"],
            TypePatterns = ["struct_specifier", "enum_specifier", "union_specifier", "type_definition", "alias_declaration"],
            Extensions = [".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx"],
            RequiresDeclaratorDrilling = true,
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var parser in _parsers.Values)
            parser.Dispose();
        foreach (var language in _languages.Values)
            language.Dispose();

        _parsers.Clear();
        _languages.Clear();
    }
}
