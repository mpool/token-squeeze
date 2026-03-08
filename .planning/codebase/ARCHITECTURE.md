# Architecture

**Analysis Date:** 2026-03-08

## Pattern Overview

**Overall:** CLI tool with layered architecture -- Commands delegate to services (Indexing, Parser, Storage) with shared Infrastructure and Security cross-cutting concerns.

**Key Characteristics:**
- Single .NET 9 console application with no dependency injection (manual wiring in commands)
- All output is JSON to stdout (consumed by Claude Code plugin); diagnostics go to stderr
- Persistent index stored as JSON files under `~/.token-squeeze/projects/`
- Incremental indexing via SHA-256 file hash comparison
- Tree-sitter native interop for AST parsing across 6+ languages

```
┌─────────────────────────────────────────────┐
│  Claude Code Plugin (skills/hooks)          │
│  Invokes CLI binary via Bash                │
├─────────────────────────────────────────────┤
│  Program.cs  (Spectre.Console.Cli router)   │
├─────────────────────────────────────────────┤
│  Commands/   (7 command handlers)           │
├──────────┬──────────┬───────────────────────┤
│ Indexing/ │ Parser/  │ Storage/              │
│ (walker,  │ (tree-   │ (JSON index           │
│  indexer) │  sitter) │  persistence)         │
├──────────┴──────────┴───────────────────────┤
│ Infrastructure/  │  Security/               │
│ (JSON output)    │  (path validation,       │
│                  │   secret detection)       │
├──────────────────┴──────────────────────────┤
│ Models/  (Symbol, CodeIndex, IndexedFile)   │
└─────────────────────────────────────────────┘
```

## Layers

**Commands Layer:**
- Purpose: CLI entry points. Each command validates input, wires up dependencies, calls services, formats JSON output.
- Location: `src/TokenSqueeze/Commands/`
- Contains: 7 command classes, each with a nested `Settings` class for argument binding
- Depends on: Indexing, Parser, Storage, Infrastructure, Models
- Used by: `Program.cs` via Spectre.Console.Cli routing

**Indexing Layer:**
- Purpose: Orchestrates directory walking and file-by-file symbol extraction. Handles incremental indexing.
- Location: `src/TokenSqueeze/Indexing/`
- Contains: `ProjectIndexer` (orchestrator), `DirectoryWalker` (file enumeration with filtering)
- Depends on: Parser, Storage, Security, Models
- Used by: `IndexCommand`

**Parser Layer:**
- Purpose: Tree-sitter AST parsing and symbol extraction. Language-agnostic via `LanguageSpec` configuration.
- Location: `src/TokenSqueeze/Parser/`
- Contains: `SymbolExtractor` (AST walker), `LanguageRegistry` (language config + parser pooling), `LanguageSpec` (per-language grammar rules)
- Depends on: Models, TreeSitter.DotNet (native interop)
- Used by: Indexing, `ParseTestCommand`

**Storage Layer:**
- Purpose: JSON serialization/deserialization of `CodeIndex` to disk at `~/.token-squeeze/projects/<name>/index.json`
- Location: `src/TokenSqueeze/Storage/`
- Contains: `IndexStore` (CRUD operations), `StoragePaths` (path resolution)
- Depends on: Models
- Used by: Commands, Indexing

**Models Layer:**
- Purpose: Shared data types. No behavior, pure records/enums.
- Location: `src/TokenSqueeze/Models/`
- Contains: `Symbol` (record with ID, location, byte offsets), `CodeIndex` (project-level aggregate), `IndexedFile` (per-file metadata), `SymbolKind` (enum)
- Depends on: Nothing
- Used by: All other layers

**Infrastructure Layer:**
- Purpose: Cross-cutting output formatting
- Location: `src/TokenSqueeze/Infrastructure/`
- Contains: `JsonOutput` (static helper for JSON stdout + error formatting)
- Depends on: Nothing
- Used by: All Commands

**Security Layer:**
- Purpose: Path traversal prevention and secret file detection
- Location: `src/TokenSqueeze/Security/`
- Contains: `PathValidator` (symlink escape detection, root boundary enforcement), `SecretDetector` (blocks indexing of .env, .pem, credentials files)
- Depends on: Nothing
- Used by: Indexing (DirectoryWalker)

## Data Flow

**Index Flow (core pipeline):**

1. `IndexCommand` receives `<path>` argument, resolves to absolute path
2. `IndexCommand` creates `LanguageRegistry`, `IndexStore`, `ProjectIndexer`
3. `ProjectIndexer.Index()` loads existing index (if any) for incremental comparison
4. `DirectoryWalker.Walk()` enumerates files, filtering by: skipped dirs, .gitignore, secret files, supported extensions, binary detection, symlink escapes
5. For each file, `ProjectIndexer` computes SHA-256 hash. If unchanged from existing index, reuses cached symbols.
6. Changed files go through `SymbolExtractor.ExtractSymbols()` which tree-sitter parses and walks the AST
7. `SymbolExtractor` maps AST node types to `SymbolKind` via `LanguageSpec` configuration, builds `Symbol` records with byte offsets
8. `ProjectIndexer` assembles `CodeIndex` and calls `IndexStore.Save()` (atomic write via temp file + move)
9. `IndexCommand` outputs JSON summary to stdout

**Extract Flow (symbol retrieval):**

1. `ExtractCommand` loads `CodeIndex` from `IndexStore`
2. Looks up `Symbol` by ID (format: `file::qualifiedName#Kind`)
3. Reads source file bytes, validates SHA-256 hash against stored hash (staleness detection)
4. Extracts source text via `Symbol.ByteOffset` + `Symbol.ByteLength` from raw bytes
5. Returns JSON with source code, metadata, and optional `stale: true` warning

**Find Flow (symbol search):**

1. `FindCommand` loads `CodeIndex` from `IndexStore`
2. Iterates all symbols, scoring each against query string (exact match: 200, contains: 100, qualified name: 75, signature: 50, docstring: 25)
3. Optionally filters by `--kind` enum and `--path` glob pattern (converted to regex)
4. Returns top N results ordered by score

**State Management:**
- No in-memory state between invocations. Each CLI run is stateless.
- All persistent state lives in `~/.token-squeeze/projects/<projectName>/index.json`
- `IndexStore` handles atomic writes (write to .tmp, then move)

## Key Abstractions

**LanguageSpec:**
- Purpose: Declarative per-language configuration for tree-sitter symbol extraction
- Examples: Defined inline in `src/TokenSqueeze/Parser/LanguageRegistry.cs` (RegisterPython, RegisterTypeScript, etc.)
- Pattern: Data-driven. Maps AST node types to `SymbolKind`, specifies field names for name/params/return-type extraction, docstring strategy, and constant/type patterns. Adding a new language = adding a new `Register*()` method.

**Symbol:**
- Purpose: Core data unit. Represents one extractable code entity (function, class, method, constant, type).
- Examples: `src/TokenSqueeze/Models/Symbol.cs`
- Pattern: Immutable record with `required` properties. ID format: `filePath::qualifiedName#Kind`. Includes byte offsets for precise source extraction without re-parsing.

**CodeIndex:**
- Purpose: Project-level aggregate containing all files and symbols for a codebase
- Examples: `src/TokenSqueeze/Models/CodeIndex.cs`
- Pattern: Immutable record. Dictionary of files keyed by relative path, flat list of symbols. Serialized as single JSON file.

**DirectoryWalker:**
- Purpose: Multi-layered file filter pipeline
- Examples: `src/TokenSqueeze/Indexing/DirectoryWalker.cs`
- Pattern: Iterator (yield return). Applies 6 filter stages: skipped dirs, .gitignore, secrets, extensions, binary check, symlink escape. Uses `Ignore` NuGet package for .gitignore parsing.

## Entry Points

**CLI Entry (Program.cs):**
- Location: `src/TokenSqueeze/Program.cs`
- Triggers: Direct invocation as `token-squeeze <command> [args]` or via Claude Code plugin skills
- Responsibilities: Tree-sitter native library smoke test on startup, Spectre.Console.Cli command routing to 7 commands

**Plugin Entry (skills/):**
- Location: `plugin/skills/*/SKILL.md`
- Triggers: Claude Code slash commands (e.g., `/token-squeeze:index`, `/token-squeeze:find`)
- Responsibilities: Each SKILL.md instructs Claude to detect platform, invoke the appropriate binary, and format output for the user

**Plugin Hook (SessionStart):**
- Location: `plugin/hooks/hooks.json`
- Triggers: Claude Code session startup
- Responsibilities: Runs `plugin/scripts/auto-index.sh` to check/refresh index for current project

## Error Handling

**Strategy:** Try-catch at command level, JSON error output to stdout (not stderr)

**Patterns:**
- Commands wrap execution in `try/catch(Exception)` and call `JsonOutput.WriteError(ex.Message)` with return code 1
- Security failures throw `SecurityException` (caught at command level)
- Tree-sitter native load failure caught at startup in `Program.cs`, exits with code 1
- `ExtractCommand` handles staleness gracefully: returns data with `stale: true` + warning rather than failing
- `DirectoryWalker` fails safe: treats unreadable files as binary, treats symlink resolution errors as escapes

## Cross-Cutting Concerns

**Logging:** Diagnostic output to stderr via `Console.Error.WriteLine()`. All structured output is JSON to stdout via `JsonOutput.Write()`. No logging framework.

**Validation:** Input validation in command `Execute()` methods (directory existence, project existence). `PathValidator` for security boundary checks. `LanguageSpec` field lookups are null-safe via `TryGetField()`.

**Authentication:** Not applicable. Local CLI tool with no network calls.

**Serialization:** System.Text.Json throughout. `JsonNamingPolicy.CamelCase`, nulls omitted via `WhenWritingNull`. Duplicate `JsonSerializerOptions` instances in `JsonOutput` and `IndexStore` (same config).

---

*Architecture analysis: 2026-03-08*
