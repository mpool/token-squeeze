# Coding Conventions

**Analysis Date:** 2026-03-08

## Naming Patterns

**Files:**
- PascalCase for all C# files: `IndexCommand.cs`, `SymbolExtractor.cs`, `JsonOutput.cs`
- One primary type per file, file name matches the type name
- No suffixes like `I` for interfaces (none exist yet -- project uses concrete classes)

**Namespaces:**
- Follow directory structure: `TokenSqueeze.Commands`, `TokenSqueeze.Parser`, `TokenSqueeze.Models`
- File-scoped namespace declarations throughout: `namespace TokenSqueeze.Commands;`

**Classes:**
- PascalCase: `IndexStore`, `LanguageRegistry`, `DirectoryWalker`
- Commands suffixed with `Command`: `IndexCommand`, `FindCommand`, `PurgeCommand`
- Static utility classes: `JsonOutput`, `StoragePaths`, `PathValidator`, `SecretDetector`

**Methods:**
- PascalCase (standard C#): `ExtractSymbols()`, `GetSpecForExtension()`, `ValidateWithinRoot()`
- Private methods also PascalCase: `BuildSignature()`, `DrillDeclaratorForName()`

**Fields:**
- Private fields prefixed with underscore: `_registry`, `_rootPath`, `_disposed`
- Static readonly fields without underscore prefix: `Options`, `SerializerOptions`, `SecretFileNames`
- Use `private readonly` for injected dependencies

**Variables:**
- camelCase for locals: `fileBytes`, `relativePath`, `allSymbols`
- Descriptive names, no abbreviations except well-known ones (`ext`, `spec`)

**Properties:**
- PascalCase with `{ get; init; }` pattern on model records
- Use `required` modifier on mandatory record properties

**Enums:**
- PascalCase values: `SymbolKind.Function`, `DocstringStrategy.NextSiblingString`
- Decorated with `[JsonConverter(typeof(JsonStringEnumConverter))]` when serialized

## Code Style

**Formatting:**
- No `.editorconfig` or explicit formatting tool detected
- Relies on default Visual Studio / SDK formatting
- 4-space indentation (standard C# default)
- Opening braces on same line for lambdas, next line for methods/classes

**Linting:**
- No `.globalconfig` or custom analyzers
- `<Nullable>enable</Nullable>` in `TokenSqueeze.csproj` -- nullable reference types enforced
- `<ImplicitUsings>enable</ImplicitUsings>` -- standard global usings active

**Language Version:**
- .NET 9 / C# 13 features used freely:
  - Collection expressions: `[]` instead of `new List<T>()`
  - `is not null` / `is null` pattern matching
  - Range/index operators: `text[3..^3]`
  - `required` properties on records
  - Top-level statements in `Program.cs`
  - `GeneratedRegex` source generators

## Import Organization

**Order:**
1. `System.*` namespaces
2. Third-party packages (`Spectre.Console.Cli`, `TreeSitter`)
3. Internal project namespaces (`TokenSqueeze.*`)

**Style:**
- Individual `using` statements at file top (no global using file beyond implicit)
- No static using imports
- No aliased imports

**Path Aliases:**
- None configured. All imports use full namespace paths.

## Error Handling

**CLI Command Pattern:**
All commands follow this structure:
```csharp
public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
{
    try
    {
        // validation
        // business logic
        JsonOutput.Write(result);
        return 0;
    }
    catch (Exception ex)
    {
        JsonOutput.WriteError(ex.Message);
        return 1;
    }
}
```

**Key rules:**
- Return `0` for success, `1` for failure (exit codes)
- ALL output goes through `JsonOutput.Write()` or `JsonOutput.WriteError()` -- never raw `Console.WriteLine()`
- Diagnostic/progress messages go to `Console.Error.WriteLine()` (stderr), never stdout
- Validation errors use early-return with `JsonOutput.WriteError()` before the try/catch
- Some commands (`OutlineCommand`, `FindCommand`) omit the outer try/catch -- inconsistent
- `catch` blocks in `DirectoryWalker.IsBinaryFile()` and `PathValidator.IsSymlinkEscape()` use bare `catch` to fail-safe

**Error output format (JSON to stdout):**
```json
{"error":"message","code":1}
```

## Logging

**Framework:** Raw `Console.Error.WriteLine()` (stderr)

**Patterns:**
- Diagnostic startup info logged to stderr in `Program.cs`
- Indexing progress summary logged to stderr in `ProjectIndexer.Index()`
- No structured logging framework -- acceptable for a CLI tool consumed by another process

## Comments

**When to Comment:**
- Comments explain "why" or non-obvious behavior, not "what"
- Step-by-step numbered comments in `DirectoryWalker.Walk()` explaining the filter pipeline
- Inline comments on algorithmic decisions (e.g., "Don't recurse into extracted symbols")

**XML docs / JSDoc:**
- Not used on any types or methods
- `[Description("...")]` attributes on Spectre.Console command arguments serve as user-facing docs

## Type Design

**Records for data:**
- All model types are `sealed record` with `required` init-only properties
- Examples: `Symbol`, `CodeIndex`, `IndexedFile`, `LanguageSpec`

**Sealed classes for services:**
- All concrete classes marked `sealed`: `IndexCommand`, `IndexStore`, `SymbolExtractor`
- `internal sealed class` is the default visibility for non-model types

**Static classes for utilities:**
- `JsonOutput`, `StoragePaths`, `PathValidator`, `SecretDetector`
- Stateless, pure functions

**No interfaces or DI:**
- Dependencies constructed directly via `new` in command handlers
- No dependency injection container
- `LanguageRegistry` implements `IDisposable` (wraps native tree-sitter resources)

## Module Design

**Exports:**
- `public` on model types (`Symbol`, `CodeIndex`) and parser types (`LanguageRegistry`, `SymbolExtractor`, `LanguageSpec`)
- `internal` on commands, storage, security, and infrastructure
- No barrel files or explicit re-exports

**Disposal:**
- `using var registry = new LanguageRegistry();` pattern in command handlers
- `LanguageRegistry.Dispose()` is idempotent (guarded by `_disposed` flag)

## JSON Output Convention

**Critical convention for all new code:**
- CLI output is consumed by a Claude Code plugin
- ALL stdout output MUST be valid JSON via `JsonOutput.Write()`
- Property names in output use camelCase (enforced by `JsonNamingPolicy.CamelCase`)
- Null properties are omitted (`JsonIgnoreCondition.WhenWritingNull`)
- JSON is NOT indented (single-line for machine parsing)

## Anonymous Types for Output

Commands construct anonymous types for JSON serialization rather than defining dedicated response DTOs:
```csharp
JsonOutput.Write(new
{
    projectName = index.ProjectName,
    filesIndexed = index.Files.Count,
    symbolsExtracted = index.Symbols.Count
});
```

This is the established pattern. Use anonymous types for command output; use named records only for internal data models.

---

*Convention analysis: 2026-03-08*
