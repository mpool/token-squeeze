# Coding Conventions

**Analysis Date:** 2026-03-08

## Naming Patterns

**Files:**
- PascalCase for all C# source files: `IndexCommand.cs`, `SymbolExtractor.cs`, `PathValidator.cs`
- One primary type per file; filename matches the type name
- Nested `Settings` classes live inside their parent command file (not separate files)

**Functions/Methods:**
- PascalCase for all public and private methods: `Execute()`, `ValidateWithinRoot()`, `ComputeFileHash()`
- Static factory methods use verb prefixes: `Symbol.MakeId()`, `Symbol.ParseId()`
- Private helper methods use PascalCase same as public: `ResolveProjectName()`, `IsIgnoredByStack()`

**Variables:**
- Private instance fields prefixed with `_`: `_store`, `_registry`, `_rootPath`, `_maxFileSize`
- Static readonly fields without prefix: `SkippedDirectories`, `RegexTimeout`, `FixtureDir`
- Local variables use camelCase: `projectDir`, `manifestPath`, `searchSymbols`
- Loop variables use short names: `s` for symbol, `f` for file, `r` for result

**Types:**
- PascalCase for all types: `CodeIndex`, `SymbolKind`, `ManifestFileEntry`
- Enum members are PascalCase: `Function`, `Class`, `Method`, `Constant`, `Type`

**Namespaces:**
- Root: `TokenSqueeze` for main project, `TokenSqueeze.Tests` for test project
- Sub-namespaces match directory structure: `TokenSqueeze.Commands`, `TokenSqueeze.Storage`, `TokenSqueeze.Parser`, `TokenSqueeze.Security`, `TokenSqueeze.Infrastructure`, `TokenSqueeze.Models`, `TokenSqueeze.Indexing`

## Code Style

**Formatting:**
- No explicit formatter config detected (no `.editorconfig`, no `Directory.Build.props`)
- Consistent 4-space indentation throughout
- Opening braces on same line for method bodies and control flow
- Single blank line between methods
- File-scoped namespaces everywhere: `namespace TokenSqueeze.Commands;`

**Access Modifiers:**
- `sealed` on ALL concrete classes: `internal sealed class IndexCommand`, `public sealed record Symbol`
- `internal` for non-model types: commands, infrastructure, storage, parser, security, indexing
- `public` for model types only: `Symbol`, `CodeIndex`, `Manifest`, `IndexedFile`, `ManifestFileEntry`, `FileSymbolData`, `ProjectMetadata`, `SymbolKind`
- `internal` visibility exposed to tests via `InternalsVisibleTo` in `TokenSqueeze.csproj`

**Records vs Classes:**
- Use `sealed record` with `required init` properties for data models: `Symbol`, `CodeIndex`, `Manifest`, `IndexedFile`, `ManifestFileEntry`
- Use `sealed record` for simple value types: `IndexResult`, `WalkedFile`, `GitignoreRule`
- Use `sealed class` for service/behavior types: `IndexStore`, `ProjectIndexer`, `DirectoryWalker`, `SymbolExtractor`
- Use `static class` for pure utility types: `JsonOutput`, `JsonDefaults`, `PathValidator`, `SecretDetector`, `StoragePaths`

**Nullable Reference Types:**
- Enabled project-wide via `<Nullable>enable</Nullable>`
- Return `null` for "not found" scenarios: `LoadManifest()`, `LoadFileSymbols()`, `LoadAllSymbols()`
- Use `is not null` / `is null` pattern matching (never `!= null`)

## Import Organization

**Order:**
1. `System.*` namespaces
2. Third-party namespaces (`Spectre.Console.Cli`, `Microsoft.Extensions.DependencyInjection`)
3. Project namespaces (`TokenSqueeze.*`)

**Path Aliases:**
- None. All imports use full namespace paths.

**Global Usings:**
- `<ImplicitUsings>enable</ImplicitUsings>` provides standard `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`
- Test project adds `<Using Include="Xunit" />` globally

## Error Handling

**Command-Level Pattern:**
- Commands wrap their `Execute` body in `try/catch(Exception ex)` and return error via `JsonOutput.WriteError(ex.Message)` with exit code 1
- Validation failures (missing project, missing file) use early-return with `JsonOutput.WriteError()` and `return 1`
- Successful execution returns `0`

```csharp
// Standard command error pattern (from IndexCommand.cs, PurgeCommand.cs)
public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
{
    try
    {
        // ... logic ...
        JsonOutput.Write(new { /* success payload */ });
        return 0;
    }
    catch (Exception ex)
    {
        JsonOutput.WriteError(ex.Message);
        return 1;
    }
}
```

**Security Validation Pattern:**
- `PathValidator.ValidateWithinRoot()` throws `SecurityException` for path traversal
- `PathValidator.IsSymlinkEscape()` returns `bool` (fail-safe: returns `true` on any exception)
- Callers either catch `SecurityException` explicitly or let it propagate to the command-level catch

**Infrastructure Error Pattern:**
- `DirectoryWalker` silently skips unreadable files/directories via `catch { continue; }` or empty array fallback
- `IndexStore.AtomicWrite` retries `UnauthorizedAccessException` up to 5 times with backoff, cleans up temp files on failure

## Output Rules (Critical)

**All stdout MUST be valid JSON:**
- Use `JsonOutput.Write()` for success output with anonymous types
- Use `JsonOutput.WriteError()` for error output
- NEVER use `Console.WriteLine()` for stdout
- Diagnostic/progress messages go to `Console.Error.WriteLine()` (stderr) only

**JSON Configuration** (defined in `src/TokenSqueeze/Infrastructure/JsonDefaults.cs`):
- `WriteIndented = false` (single-line output)
- `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` (omit null properties)
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`

**Output Shape:**
- Use anonymous types for command JSON output (not named records)
- Use named records for internal models only

```csharp
// Correct: anonymous type for command output
JsonOutput.Write(new
{
    projectName = result.Index.ProjectName,
    filesIndexed = result.Index.Files.Count,
    symbolsExtracted = result.Index.Symbols.Count
});

// Correct: named record for internal model
public sealed record CodeIndex { ... }
```

## Logging

**Framework:** Direct `Console.Error.WriteLine()` (no logging framework)

**Patterns:**
- Progress/diagnostic messages to stderr: `Console.Error.WriteLine($"Indexed {name}: {count} files...")`
- Warnings to stderr: `Console.Error.WriteLine($"Skipping oversized file...")`
- Never log to stdout (reserved for JSON output)

## Comments

**When to Comment:**
- Security-relevant code gets inline comments with SEC-XX identifiers: `// SEC-04: Reject overly long globs`
- Bug-related test cases reference BUG-XX: `// BUG-02: C has no container types`
- Debt items reference DEBT-XX: `// DEBT-02`
- Brief inline comments for non-obvious logic, especially in complex methods

**JSDoc/TSDoc:**
- XML doc comments (`<summary>`) used sparingly, primarily on methods that could be confused with similar methods (e.g., `StoragePaths.GetLegacyIndexPath`)
- No systematic XML doc coverage

## Dependency Injection

**Pattern:** Constructor injection via Spectre.Console.Cli's `ITypeRegistrar`/`ITypeResolver` backed by `Microsoft.Extensions.DependencyInjection`

**Registration** (in `src/TokenSqueeze/Program.cs`):
```csharp
var services = new ServiceCollection();
services.AddSingleton<LanguageRegistry>();
services.AddSingleton<IndexStore>();
var registrar = new TypeRegistrar(services);
```

**Consumption** via primary constructors:
```csharp
internal sealed class IndexCommand(IndexStore store, LanguageRegistry registry) : Command<IndexCommand.Settings>
```

## Command Structure

**Pattern:** Every CLI command follows the Spectre.Console.Cli `Command<TSettings>` pattern.

```csharp
internal sealed class FooCommand(IndexStore store) : Command<FooCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--flag|-f <VALUE>")]
        [Description("An option")]
        public string? Flag { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        // ... validate, process, output JSON ...
    }
}
```

**Key conventions:**
- `Settings` is always a nested `sealed class` inside the command
- Arguments use `[CommandArgument]` with positional index and angle-bracket syntax
- Options use `[CommandOption]` with long/short form
- `CancellationToken cancellation` parameter on `Execute` (Spectre overload)

## Module Design

**Exports:**
- Models are `public` (needed for serialization and test access)
- Everything else is `internal` (exposed to tests via `InternalsVisibleTo`)
- No barrel files or re-exports

**Static utility classes:**
- `JsonOutput`, `JsonDefaults`, `PathValidator`, `SecretDetector`, `StoragePaths` are `internal static`
- `StoragePaths` has a `TestRootOverride` field for test isolation

---

*Convention analysis: 2026-03-08*
