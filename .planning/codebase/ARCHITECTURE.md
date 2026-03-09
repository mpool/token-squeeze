# Architecture

**Analysis Date:** 2026-03-08

## Pattern Overview

**Overall:** CLI application using Command pattern with DI, layered into Commands -> Indexing/Storage/Parser -> Models

**Key Characteristics:**
- Spectre.Console.Cli command dispatch with constructor-injected services
- tree-sitter native bindings for multi-language AST parsing
- Per-file split storage on local filesystem (`~/.token-squeeze/projects/`)
- All stdout is structured JSON; diagnostics go to stderr
- Incremental indexing via SHA-256 content hashing and mtime comparison
- Parallel file parsing during full index; single-threaded for query-time reindex

```
                  CLI Entry (Program.cs)
                         |
              Spectre.Console.Cli routing
                         |
         +-------+-------+-------+-------+-------+-------+
         |       |       |       |       |       |       |
       index   list    purge  outline extract  find   parse-test
         |       |       |       |       |       |
         v       v       v       v       v       v
    +-----------+  +-----------+  +------------------+
    | Indexing   |  | Storage   |  | Parser           |
    +-----------+  +-----------+  +------------------+
    | ProjectIdx |  | IndexStore|  | LanguageRegistry |
    | DirWalker  |  | StorePaths|  | SymbolExtractor  |
    | StalenessChk| | QueryReidx|  | LanguageSpec     |
    | IncrReindxr| | LegacyMigr|  +------------------+
    +-----------+  +-----------+
         |              |
         v              v
    +-----------+  +------------------+
    | Security   |  | Models           |
    +-----------+  +------------------+
    | PathValid  |  | Symbol, CodeIndex|
    | SecretDet  |  | Manifest, etc.   |
    +-----------+  +------------------+
```

## Layers

**Commands (Presentation):**
- Purpose: Parse CLI arguments, orchestrate service calls, format JSON output
- Location: `src/TokenSqueeze/Commands/`
- Contains: 7 command classes, each with nested `Settings` class
- Depends on: Indexing, Storage, Parser, Infrastructure, Models, Security
- Used by: `Program.cs` via Spectre.Console.Cli routing

**Indexing (Core Business Logic):**
- Purpose: Walk directories, parse files, build/update indexes
- Location: `src/TokenSqueeze/Indexing/`
- Contains: `ProjectIndexer`, `DirectoryWalker`, `StalenessChecker`, `IncrementalReindexer`
- Depends on: Storage, Parser, Models, Security
- Used by: Commands (`IndexCommand`), Storage (`QueryReindexer`, `LegacyMigration`)

**Storage (Persistence):**
- Purpose: Read/write index data to `~/.token-squeeze/projects/<name>/`
- Location: `src/TokenSqueeze/Storage/`
- Contains: `IndexStore`, `StoragePaths`, `QueryReindexer`, `LegacyMigration`
- Depends on: Models, Security, Infrastructure (JsonDefaults)
- Used by: Commands, Indexing

**Parser (AST Extraction):**
- Purpose: Configure tree-sitter languages and extract symbols from source files
- Location: `src/TokenSqueeze/Parser/`
- Contains: `LanguageRegistry`, `SymbolExtractor`, `LanguageSpec`
- Depends on: Models, TreeSitter.DotNet native library
- Used by: Commands, Indexing, Storage (via reindexing)

**Models (Domain):**
- Purpose: Data types shared across all layers
- Location: `src/TokenSqueeze/Models/`
- Contains: `Symbol`, `CodeIndex`, `Manifest`, `ManifestFileEntry`, `IndexedFile`, `FileSymbolData`, `ProjectMetadata`
- Depends on: Nothing
- Used by: All other layers

**Security (Cross-Cutting):**
- Purpose: Path traversal prevention, symlink escape detection, secret file filtering
- Location: `src/TokenSqueeze/Security/`
- Contains: `PathValidator`, `SecretDetector`
- Depends on: Nothing
- Used by: Storage (`IndexStore`), Indexing (`DirectoryWalker`)

**Infrastructure (Framework Glue):**
- Purpose: DI wiring, JSON serialization defaults, standardized output
- Location: `src/TokenSqueeze/Infrastructure/`
- Contains: `TypeRegistrar`, `TypeResolver`, `JsonOutput`, `JsonDefaults`
- Depends on: Spectre.Console.Cli, Microsoft.Extensions.DependencyInjection
- Used by: `Program.cs`, all Commands

## Data Flow

**Index Command (Full Index):**

1. `IndexCommand.Execute()` resolves full path, creates `ProjectIndexer`
2. `ProjectIndexer.Index()` loads existing index for incremental comparison
3. `DirectoryWalker.Walk()` yields `WalkedFile(path, bytes)` lazily, applying filters: gitignore stack, secret detection, extension check, size limit, symlink escape, binary content check
4. `Parallel.ForEach` processes walked files: per-thread `LanguageRegistry` + `SymbolExtractor` instances, SHA-256 hash comparison skips unchanged files
5. `IndexStore.Save()` writes split storage: per-file fragment JSON files in `files/`, `search-index.json` (lightweight symbol list), `manifest.json` (written last for crash safety)
6. `JsonOutput.Write()` emits summary JSON to stdout

**Query Commands (find/outline/extract):**

1. Command calls `LegacyMigration.TryMigrateIfNeeded()` for old format indexes
2. Command calls `QueryReindexer.EnsureFresh()` which:
   a. Loads `manifest.json`
   b. `StalenessChecker.DetectStaleFiles()` compares mtime then SHA-256 hash, also detects deleted/new files
   c. If stale files found, `IncrementalReindexer.ReindexFiles()` re-parses up to 50 files and rebuilds `search-index.json`
3. Command loads symbols from `search-index.json` (find) or per-file fragments (outline/extract)
4. `ExtractCommand` reads original source bytes from disk, extracts by `ByteOffset`/`ByteLength`

**State Management:**
- No in-memory state between CLI invocations (process-per-command model)
- All state persisted to `~/.token-squeeze/projects/<projectName>/`
- `IndexStore` and `LanguageRegistry` are DI singletons within a single invocation
- `LanguageRegistry` owns native tree-sitter handles; disposed after `app.Run()` returns

## Key Abstractions

**LanguageSpec:**
- Purpose: Declarative configuration for a programming language's AST structure
- Examples: Registered in `src/TokenSqueeze/Parser/LanguageRegistry.cs` (one per language)
- Pattern: Data-driven extraction -- maps tree-sitter node types to symbol kinds, field names to name/param/return-type accessors, plus optional `ConstantExtractor` and `SignatureBuilder` delegates

**Symbol:**
- Purpose: A single extracted code symbol (function, class, method, constant, type)
- Definition: `src/TokenSqueeze/Models/Symbol.cs`
- Pattern: Immutable record with deterministic `Id` format: `{filePath}::{qualifiedName}#{kind}`

**Manifest / ManifestFileEntry:**
- Purpose: Lightweight index metadata mapping file paths to storage keys and hashes
- Definition: `src/TokenSqueeze/Models/Manifest.cs`
- Pattern: Written last during save for crash safety; loaded first during queries

**IndexStore:**
- Purpose: Single gateway for all filesystem persistence operations
- Definition: `src/TokenSqueeze/Storage/IndexStore.cs`
- Pattern: Validates paths via `PathValidator.ValidateWithinRoot()` on every public method; uses `AtomicWrite` (temp file + rename) for crash safety

## Entry Points

**CLI (`Program.cs`):**
- Location: `src/TokenSqueeze/Program.cs`
- Triggers: Direct CLI invocation or Claude Code plugin skill execution
- Responsibilities: Wire DI container (`LanguageRegistry`, `IndexStore`), register commands with Spectre.Console.Cli, run command, dispose service provider

**Claude Code Plugin:**
- Location: `plugin/.claude-plugin/plugin.json` + `plugin/skills/*/SKILL.md`
- Triggers: Claude Code skill invocations (e.g., "index this project", "find symbol")
- Responsibilities: Map natural language intents to CLI commands

**Auto-Index Hook:**
- Location: `plugin/hooks/hooks.json` + `plugin/scripts/auto-index.{sh,ps1}`
- Triggers: Claude Code session start
- Responsibilities: Automatically index the current project directory

## Error Handling

**Strategy:** Return JSON error objects via `JsonOutput.WriteError()` with exit code 1; never throw unhandled exceptions to stdout

**Patterns:**
- Commands wrap body in `try/catch(Exception)`, call `JsonOutput.WriteError(ex.Message)`, return 1
- `DirectoryWalker` silently skips unreadable files/directories (fail-safe)
- `ProjectIndexer.Index()` uses `Parallel.ForEach` with per-file `try/catch`; counts errors, continues processing
- `IncrementalReindexer` catches per-file exceptions, logs to stderr, continues
- `IndexStore.AtomicWrite` retries `File.Move` up to 5 times on `UnauthorizedAccessException` (Windows file locking), cleans up temp file on unrecoverable failure
- `PathValidator.ValidateWithinRoot` throws `SecurityException` on traversal attempts; callers catch and return error JSON
- `#if DEBUG` in `Program.cs` enables `PropagateExceptions()` for development

## Cross-Cutting Concerns

**Logging:** stderr only via `Console.Error.WriteLine()`. No logging framework. Used for progress messages, warnings, and per-file parse errors.

**Validation:** `PathValidator.ValidateWithinRoot()` called in nearly every `IndexStore` public method. `ProjectIndexer.SanitizeName()` strips dangerous characters from project names. `LanguageSpec.Validate()` checks cross-reference consistency at registration time.

**Authentication:** Not applicable (local CLI tool, no network auth).

**Serialization:** All JSON via `System.Text.Json` with shared `JsonDefaults.Options` (camelCase, no nulls, single-line). `AtomicWrite` pattern used everywhere for crash safety.

**Concurrency:** `Parallel.ForEach` in `ProjectIndexer.Index()` with per-thread `LanguageRegistry`/`SymbolExtractor` instances (tree-sitter parsers are not thread-safe). `ConcurrentBag` collects results, then deterministic sort.

---

*Architecture analysis: 2026-03-08*
