using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TokenSqueeze.Models;
using TreeSitter;

namespace TokenSqueeze.Parser;

public sealed partial class SymbolExtractor
{
    private const int MaxTreeDepth = 128;

    private readonly LanguageRegistry _registry;

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]+$")]
    private static partial Regex AllCapsPattern();

    public SymbolExtractor(LanguageRegistry registry)
    {
        _registry = registry;
    }

    public List<Symbol> ExtractSymbols(string filePath, byte[] sourceBytes, LanguageSpec spec)
    {
        var parser = _registry.GetOrCreateParser(spec.LanguageId);
        var sourceText = Encoding.UTF8.GetString(sourceBytes);
        using var tree = parser.Parse(sourceText);

        var symbols = new List<Symbol>();
        if (tree?.RootNode is { } root)
        {
            WalkTree(root, spec, sourceBytes, filePath, symbols, parentSymbol: null, scopeParts: [], depth: 0);
        }

        return symbols;
    }

    private void WalkTree(
        Node node,
        LanguageSpec spec,
        byte[] sourceBytes,
        string filePath,
        List<Symbol> symbols,
        Symbol? parentSymbol,
        List<string> scopeParts,
        int depth)
    {
        if (depth > MaxTreeDepth)
        {
            Console.Error.WriteLine($"Warning: AST depth limit ({MaxTreeDepth}) reached in {filePath}, truncating tree walk");
            return;
        }

        // Check if this node is a symbol we extract
        if (spec.SymbolNodeTypes.TryGetValue(node.Type, out var kind))
        {
            // Function inside a container is a Method
            if (kind == SymbolKind.Function && scopeParts.Count > 0)
            {
                kind = SymbolKind.Method;
            }

            var name = ExtractName(node, spec);
            if (!string.IsNullOrEmpty(name))
            {
                var symbol = BuildSymbol(node, spec, sourceBytes, filePath, name, kind, parentSymbol, scopeParts);
                symbols.Add(symbol);

                // If container, recurse children with updated scope
                if (spec.ContainerNodeTypes.Contains(node.Type))
                {
                    var newScope = new List<string>(scopeParts) { name };
                    foreach (var child in node.NamedChildren)
                        WalkTree(child, spec, sourceBytes, filePath, symbols, symbol, newScope, depth + 1);
                }

                // Don't recurse into extracted symbols (prevents picking up
                // struct_specifier params inside C/C++ function definitions, etc.)
                return;
            }
        }

        // Check constant patterns
        if (spec.ConstantPatterns.Contains(node.Type))
        {
            TryExtractConstant(node, spec, sourceBytes, filePath, symbols, parentSymbol, scopeParts);
            return; // Don't recurse into constants
        }

        // Check type patterns
        if (spec.TypePatterns.Contains(node.Type))
        {
            var name = ExtractTypeName(node, spec);
            if (!string.IsNullOrEmpty(name))
            {
                var symbol = BuildSymbol(node, spec, sourceBytes, filePath, name, SymbolKind.Type, parentSymbol, scopeParts);
                symbols.Add(symbol);
            }
            return;
        }

        // Recurse children
        foreach (var child in node.NamedChildren)
            WalkTree(child, spec, sourceBytes, filePath, symbols, parentSymbol, scopeParts, depth + 1);
    }

    private void TryExtractConstant(
        Node node,
        LanguageSpec spec,
        byte[] sourceBytes,
        string filePath,
        List<Symbol> symbols,
        Symbol? parentSymbol,
        List<string> scopeParts)
    {
        if (spec.ConstantExtractor == null) return;
        var name = spec.ConstantExtractor(node, spec, parentSymbol);
        if (string.IsNullOrEmpty(name)) return;
        var symbol = BuildSymbol(node, spec, sourceBytes, filePath, name, SymbolKind.Constant, parentSymbol, scopeParts);
        symbols.Add(symbol);
    }

    internal static string? ExtractPythonConstant(Node node, LanguageSpec spec, Symbol? parentSymbol)
    {
        // Python: module-level assignment with ALL_CAPS identifier
        if (parentSymbol != null) return null; // Must be module-level

        var leftNode = node.NamedChildren.FirstOrDefault();
        if (leftNode?.Type != "identifier") return null;

        var name = leftNode.Text;
        if (!AllCapsPattern().IsMatch(name)) return null;

        return name;
    }

    internal static string? ExtractJsConstant(Node node, LanguageSpec spec, Symbol? parentSymbol)
    {
        // JS/TS: lexical_declaration starting with "const"
        var nodeText = node.Text;
        if (!nodeText.StartsWith("const ")) return null;

        var declarator = node.NamedChildren.FirstOrDefault(c => c.Type == "variable_declarator");
        if (declarator == null) return null;

        var nameNode = TryGetField(declarator, "name");
        return nameNode?.Text;
    }

    internal static string? ExtractCConstant(Node node, LanguageSpec spec, Symbol? parentSymbol)
    {
        // C/C++: preproc_def -- #define NAME VALUE
        var nameNode = TryGetField(node, "name");
        return nameNode?.Text;
    }

    private Symbol BuildSymbol(
        Node node,
        LanguageSpec spec,
        byte[] sourceBytes,
        string filePath,
        string name,
        SymbolKind kind,
        Symbol? parentSymbol,
        List<string> scopeParts)
    {
        var qualifiedName = BuildQualifiedName(scopeParts, name);
        var signature = BuildSignature(node, spec, name, kind);
        var docstring = ExtractDocstring(node, spec);
        var contentHash = ComputeHash(sourceBytes, node.StartIndex, node.EndIndex - node.StartIndex);

        return new Symbol
        {
            Id = Symbol.MakeId(filePath, qualifiedName, kind),
            File = filePath,
            Name = name,
            QualifiedName = qualifiedName,
            Kind = kind,
            Language = spec.DisplayName,
            Signature = signature,
            Docstring = docstring,
            Parent = parentSymbol?.QualifiedName,
            Line = node.StartPosition.Row + 1,
            EndLine = node.EndPosition.Row + 1,
            ByteOffset = node.StartIndex,
            ByteLength = node.EndIndex - node.StartIndex,
            ContentHash = contentHash,
        };
    }

    private static string ExtractName(Node node, LanguageSpec spec)
    {
        // C/C++ function_definition uses declarator drilling
        if (spec.RequiresDeclaratorDrilling && node.Type == "function_definition")
        {
            return DrillDeclaratorForName(node);
        }

        if (spec.NameFields.TryGetValue(node.Type, out var fieldName))
        {
            var nameNode = TryGetField(node, fieldName);
            return nameNode?.Text ?? "";
        }

        return "";
    }

    private static string ExtractTypeName(Node node, LanguageSpec spec)
    {
        // C/C++ type_definition: typedef struct Point Point_t; -- name is in "declarator"
        if (spec.RequiresDeclaratorDrilling && node.Type == "type_definition")
        {
            var declaratorNode = TryGetField(node, "declarator");
            if (declaratorNode != null)
                return declaratorNode.Text;
        }

        // Fall back to standard name extraction
        if (spec.NameFields.TryGetValue(node.Type, out var fieldName))
        {
            var nameNode = TryGetField(node, fieldName);
            return nameNode?.Text ?? "";
        }

        return "";
    }

    private static string DrillDeclaratorForName(Node functionDef)
    {
        var declarator = TryGetField(functionDef, "declarator");
        if (declarator == null) return "";

        return DrillToIdentifier(declarator);
    }

    private static string DrillToIdentifier(Node node)
    {
        if (node.Type is "identifier" or "type_identifier" or "field_identifier")
            return node.Text;

        if (node.Type == "qualified_identifier")
        {
            var nameNode = TryGetField(node, "name");
            return nameNode?.Text ?? node.Text;
        }

        if (node.Type == "function_declarator")
        {
            var inner = TryGetField(node, "declarator");
            if (inner != null)
                return DrillToIdentifier(inner);
        }

        var decl = TryGetField(node, "declarator");
        if (decl != null)
            return DrillToIdentifier(decl);

        foreach (var child in node.NamedChildren)
        {
            if (child.Type is "identifier" or "type_identifier" or "field_identifier")
                return child.Text;
        }

        return "";
    }

    private static string DrillDeclaratorForParams(Node functionDef)
    {
        var declarator = TryGetField(functionDef, "declarator");
        if (declarator == null) return "";

        return FindFunctionDeclaratorParams(declarator);
    }

    private static string FindFunctionDeclaratorParams(Node node)
    {
        if (node.Type == "function_declarator")
        {
            var paramsNode = TryGetField(node, "parameters");
            return paramsNode?.Text ?? "()";
        }

        var decl = TryGetField(node, "declarator");
        if (decl != null)
            return FindFunctionDeclaratorParams(decl);

        return "()";
    }

    private static string BuildQualifiedName(List<string> scopeParts, string name)
    {
        if (scopeParts.Count == 0) return name;
        return string.Join(".", scopeParts) + "." + name;
    }

    private static string BuildSignature(Node node, LanguageSpec spec, string name, SymbolKind kind)
    {
        if (kind == SymbolKind.Constant)
        {
            if (spec.RequiresDeclaratorDrilling && node.Type == "preproc_def")
            {
                var macroNameNode = TryGetField(node, "name");
                var valueNode = TryGetField(node, "value");
                var macroName = macroNameNode?.Text ?? name;
                var macroValue = valueNode?.Text?.Split('\n')[0].TrimEnd() ?? "";
                return string.IsNullOrEmpty(macroValue) ? $"#define {macroName}" : $"#define {macroName} {macroValue}";
            }
            return node.Text.Split('\n')[0].TrimEnd();
        }

        if (spec.RequiresDeclaratorDrilling && node.Type == "function_definition")
        {
            return BuildCFunctionSignature(node, name);
        }

        var paramsText = "";
        if (spec.ParamFields.TryGetValue(node.Type, out var paramField))
        {
            var paramsNode = TryGetField(node, paramField);
            paramsText = paramsNode?.Text ?? "()";
        }

        var returnText = "";
        if (spec.ReturnTypeFields.TryGetValue(node.Type, out var returnField))
        {
            var returnNode = TryGetField(node, returnField);
            if (returnNode != null)
                returnText = returnNode.Text;
        }

        if (spec.SignatureBuilder != null)
            return spec.SignatureBuilder(name, kind, paramsText, returnText);
        return $"{name}{paramsText}";
    }

    private static string BuildCFunctionSignature(Node functionDef, string name)
    {
        var returnTypeNode = TryGetField(functionDef, "type");
        var returnType = returnTypeNode?.Text ?? "";
        var paramsText = DrillDeclaratorForParams(functionDef);
        return string.IsNullOrEmpty(returnType) ? $"{name}{paramsText}" : $"{returnType} {name}{paramsText}";
    }

    internal static string BuildCCppSignature(string name, SymbolKind kind, string paramsText, string returnText)
    {
        return kind switch
        {
            SymbolKind.Class => $"class {name}",
            _ => name,
        };
    }

    internal static string BuildPythonSignature(string name, SymbolKind kind, string paramsText, string returnText)
    {
        if (kind == SymbolKind.Class)
            return $"class {name}";

        var sig = $"def {name}{paramsText}";
        if (!string.IsNullOrEmpty(returnText))
            sig += $" -> {returnText}";
        return sig;
    }

    internal static string BuildJavaScriptSignature(string name, SymbolKind kind, string paramsText, string returnText)
    {
        return kind switch
        {
            SymbolKind.Class => $"class {name}",
            SymbolKind.Method => $"{name}{paramsText}",
            _ => $"function {name}{paramsText}",
        };
    }

    internal static string BuildTypeScriptSignature(string name, SymbolKind kind, string paramsText, string returnText)
    {
        if (kind == SymbolKind.Class)
            return $"class {name}";
        if (kind == SymbolKind.Type)
            return name;

        var sig = kind == SymbolKind.Method ? $"{name}{paramsText}" : $"function {name}{paramsText}";
        if (!string.IsNullOrEmpty(returnText))
        {
            var cleanReturn = returnText.TrimStart(':', ' ');
            sig += $": {cleanReturn}";
        }
        return sig;
    }

    internal static string BuildCSharpSignature(string name, SymbolKind kind, string paramsText, string returnText)
    {
        if (kind == SymbolKind.Class)
            return $"class {name}";

        var sig = "";
        if (!string.IsNullOrEmpty(returnText))
            sig += $"{returnText} ";
        sig += $"{name}{paramsText}";
        return sig;
    }

    private static Node? TryGetField(Node node, string fieldName)
    {
        return node.GetChildForField(fieldName);
    }

    private static string ExtractDocstring(Node node, LanguageSpec spec)
    {
        return spec.DocstringStrategy switch
        {
            DocstringStrategy.NextSiblingString => ExtractNextSiblingDocstring(node),
            DocstringStrategy.PrecedingComment => ExtractPrecedingComment(node),
            _ => "",
        };
    }

    private static string ExtractNextSiblingDocstring(Node node)
    {
        var bodyNode = TryGetField(node, "body");
        if (bodyNode == null || bodyNode.NamedChildren.Count == 0)
            return "";

        var firstStmt = bodyNode.NamedChildren[0];
        Node? stringNode = null;

        if (firstStmt.Type == "expression_statement")
            stringNode = firstStmt.NamedChildren.FirstOrDefault(c => c.Type == "string");
        else if (firstStmt.Type == "string")
            stringNode = firstStmt;

        if (stringNode == null)
            return "";

        return StripQuotes(stringNode.Text);
    }

    private static string StripQuotes(string text)
    {
        if (text.StartsWith("\"\"\"") && text.EndsWith("\"\"\""))
            text = text[3..^3];
        else if (text.StartsWith("'''") && text.EndsWith("'''"))
            text = text[3..^3];
        else if (text.StartsWith("\"") && text.EndsWith("\""))
            text = text[1..^1];
        else if (text.StartsWith("'") && text.EndsWith("'"))
            text = text[1..^1];
        return text.Trim();
    }

    private static string ExtractPrecedingComment(Node node)
    {
        var commentLines = new List<string>();
        var prevSibling = node.PreviousSibling;

        while (prevSibling != null && prevSibling.Type == "comment")
        {
            commentLines.Insert(0, prevSibling.Text);
            prevSibling = prevSibling.PreviousSibling;
        }

        return commentLines.Count > 0 ? string.Join("\n", commentLines) : "";
    }

    private static string ComputeHash(byte[] sourceBytes, int startIndex, int length)
    {
        if (length <= 0 || startIndex < 0 || startIndex + length > sourceBytes.Length)
            return "";

        var hashBytes = SHA256.HashData(sourceBytes.AsSpan(startIndex, length));
        return Convert.ToHexStringLower(hashBytes);
    }
}
