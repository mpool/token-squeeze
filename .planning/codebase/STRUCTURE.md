# Codebase Structure

**Analysis Date:** 2026-03-08

## Directory Layout

```
token-squeeze/
├── src/
│   ├── TokenSqueeze/                  # Main .NET 9 console application
│   │   ├── Program.cs                 # CLI entry point, DI wiring, command registration
│   │   ├── TokenSqueeze.csproj        # Project file (net9.0)
│   │   ├── Commands/                  # CLI command handlers (one class per command)
│   │   ├── Indexing/                  # Directory walking, parsing orchestration, staleness detection
│   │   ├── Infrastructure/            # DI glue, JSON helpers, output formatting
│   │   ├── Models/                    # Shared data types (records)
│   │   ├── Parser/                    # tree-sitter language configs and symbol extraction
│   │   ├── Security/                  # Path validation, secret detection
│   │   └── Storage/                   # Filesystem persistence (IndexStore, StoragePaths)
│   ├── TokenSqueeze.Tests/            # xUnit test project
│   │   ├── TokenSqueeze.Tests.csproj
│   │   ├── Commands/                  # Command-level tests
│   │   ├── Fixtures/                  # Sample source files for parser tests (sample.*)
│   │   ├── Helpers/                   # Test utilities
│   │   ├── Indexing/                  # Indexing logic tests
│   │   ├── Models/                    # Model tests
│   │   ├── Parser/                    # Parser/extractor tests
│   │   ├── Security/                  # Security validation tests
│   │   ├── Storage/                   # Storage tests
│   │   ├── SmokeTest.cs              # End-to-end CLI smoke tests
│   │   ├── DisposalTests.cs          # Service provider disposal tests
│   │   ├── LanguageSpecValidationTests.cs
│   │   ├── OutlineHierarchyTests.cs
│   │   ├── ReindexOnQueryTests.cs
│   │   ├── RobustnessTests.cs
│   │   └── StalenessCheckerTests.cs
│   └── token-squeeze.sln             # Solution file
├── plugin/                            # Claude Code plugin
│   ├── .claude-plugin/
│   │   └── plugin.json               # Plugin manifest
│   ├── skills/                        # Skill definitions (one dir per command)
│   │   ├── index/SKILL.md
│   │   ├── list/SKILL.md
│   │   ├── purge/SKILL.md
│   │   ├── outline/SKILL.md
│   │   ├── extract/SKILL.md
│   │   └── find/SKILL.md
│   ├── hooks/
│   │   └── hooks.json                # Auto-index on session start
│   ├── scripts/
│   │   ├── auto-index.sh             # Hook script (Linux/Mac)
│   │   └── auto-index.ps1            # Hook script (Windows)
│   ├── bin/                           # Published platform binaries
│   └── build.sh                       # Cross-platform build script
├── installer/
│   └── install.js                     # npx installer (copies plugin to ~/.claude/)
├── package.json                       # npm package for `npx token-squeeze`
├── .planning/                         # GSD planning documents
│   └── codebase/                      # Codebase analysis docs
├── .claude/
│   └── rules/
│       └── security.md                # Security rules for Claude
├── CLAUDE.md                          # Project instructions
└── README.md
```

## Directory Purposes

**`src/TokenSqueeze/Commands/`:**
- Purpose: One command handler per CLI verb
- Contains: `IndexCommand.cs`, `ListCommand.cs`, `PurgeCommand.cs`, `OutlineCommand.cs`, `ExtractCommand.cs`, `FindCommand.cs`, `ParseTestCommand.cs`
- Key pattern: Each class extends `Command<T.Settings>` with a nested `Settings` class for argument parsing

**`src/TokenSqueeze/Indexing/`:**
- Purpose: Core indexing pipeline -- walking directories, detecting changes, re-parsing
- Contains: `ProjectIndexer.cs` (full index orchestrator), `DirectoryWalker.cs` (filtered recursive walk), `StalenessChecker.cs` (mtime/hash comparison), `IncrementalReindexer.cs` (query-time partial reindex)
- Key pattern: `DirectoryWalker` yields `WalkedFile` records lazily; `ProjectIndexer` consumes them in parallel

**`src/TokenSqueeze/Infrastructure/`:**
- Purpose: Framework plumbing not specific to domain logic
- Contains: `TypeRegistrar.cs` + `TypeResolver.cs` (Spectre.Console DI bridge), `JsonOutput.cs` (stdout JSON writer), `JsonDefaults.cs` (shared serializer options)
- Key pattern: `JsonOutput.Write()` is the ONLY way to write to stdout

**`src/TokenSqueeze/Models/`:**
- Purpose: Immutable domain data types shared across all layers
- Contains: `Symbol.cs`, `CodeIndex.cs`, `Manifest.cs` (+ `ManifestFileEntry`), `IndexedFile.cs`, `FileSymbolData.cs`, `ProjectMetadata.cs`
- Key pattern: All are `sealed record` with `required init` properties

**`src/TokenSqueeze/Parser/`:**
- Purpose: tree-sitter language configuration and AST-to-symbol extraction
- Contains: `LanguageRegistry.cs` (language registration, parser lifecycle), `SymbolExtractor.cs` (AST walker, signature builders), `LanguageSpec.cs` (declarative language config record)
- Key pattern: `LanguageSpec` is data-driven; add a language by registering a new spec in `LanguageRegistry`

**`src/TokenSqueeze/Security/`:**
- Purpose: Prevent path traversal, symlink escapes, and secret file indexing
- Contains: `PathValidator.cs` (root containment check, symlink detection), `SecretDetector.cs` (filename-based secret filtering)

**`src/TokenSqueeze/Storage/`:**
- Purpose: All filesystem I/O for index persistence
- Contains: `IndexStore.cs` (CRUD operations), `StoragePaths.cs` (path constants/helpers), `QueryReindexer.cs` (freshness check on read), `LegacyMigration.cs` (v1->v2 format upgrade)
- Key pattern: Split storage format -- `manifest.json` + `search-index.json` + `files/<key>.json` per file

**`src/TokenSqueeze.Tests/`:**
- Purpose: xUnit test project mirroring main project structure
- Contains: Test classes organized by feature area, plus cross-cutting test files at root
- Key files: `Fixtures/` contains sample source files (`sample.py`, `sample.js`, etc.)

**`plugin/`:**
- Purpose: Claude Code integration -- makes CLI commands available as Claude skills
- Contains: Plugin manifest, skill markdown files, auto-index hooks, build scripts

## Key File Locations

**Entry Points:**
- `src/TokenSqueeze/Program.cs`: CLI entry point -- DI setup, command registration, disposal
- `plugin/.claude-plugin/plugin.json`: Plugin manifest for Claude Code
- `installer/install.js`: npx installer entry point

**Configuration:**
- `src/TokenSqueeze/TokenSqueeze.csproj`: .NET project config (net9.0, dependencies)
- `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj`: Test project config
- `src/token-squeeze.sln`: Solution file
- `package.json`: npm package config for npx distribution

**Core Logic:**
- `src/TokenSqueeze/Indexing/ProjectIndexer.cs`: Full index orchestration (parallel walk + parse + save)
- `src/TokenSqueeze/Parser/SymbolExtractor.cs`: AST walking and symbol extraction (~470 lines, largest file)
- `src/TokenSqueeze/Parser/LanguageRegistry.cs`: Language definitions and tree-sitter parser lifecycle
- `src/TokenSqueeze/Storage/IndexStore.cs`: All persistence operations (~360 lines)

**Security:**
- `src/TokenSqueeze/Security/PathValidator.cs`: `ValidateWithinRoot()`, `IsSymlinkEscape()`
- `src/TokenSqueeze/Security/SecretDetector.cs`: `IsSecretFile()` filename-based filter

**Testing:**
- `src/TokenSqueeze.Tests/SmokeTest.cs`: End-to-end CLI tests
- `src/TokenSqueeze.Tests/Fixtures/`: Sample source files for all supported languages

## Naming Conventions

**Files:**
- PascalCase for C# files matching class name: `IndexCommand.cs`, `SymbolExtractor.cs`
- Nested `Settings` class lives inside its command file (not a separate file)
- Test files: `{ClassName}Tests.cs` or `{Feature}Tests.cs`

**Directories:**
- PascalCase matching namespace segment: `Commands/`, `Storage/`, `Parser/`
- Test directories mirror source: `Tests/Commands/`, `Tests/Storage/`, etc.
- Plugin directories are lowercase: `skills/`, `hooks/`, `scripts/`

**Namespaces:**
- `TokenSqueeze.{Directory}` -- e.g., `TokenSqueeze.Commands`, `TokenSqueeze.Storage`
- File-scoped namespace declarations: `namespace TokenSqueeze.Commands;`

## Where to Add New Code

**New CLI Command:**
1. Create `src/TokenSqueeze/Commands/FooCommand.cs`
   - Extend `Command<FooCommand.Settings>` with constructor-injected services
   - Nested `sealed class Settings : CommandSettings` for arguments
   - Use `JsonOutput.Write()` / `JsonOutput.WriteError()` for all output
2. Register in `src/TokenSqueeze/Program.cs`: `config.AddCommand<FooCommand>("foo")`
3. Create `plugin/skills/foo/SKILL.md` for Claude Code integration
4. Add tests in `src/TokenSqueeze.Tests/Commands/FooCommandTests.cs`

**New Language Support:**
1. Add `RegisterLanguageName()` method in `src/TokenSqueeze/Parser/LanguageRegistry.cs`, call from constructor
2. Create `LanguageSpec` record with node type mappings, field names, optional extractors
3. Add test fixture `src/TokenSqueeze.Tests/Fixtures/sample.ext`
4. Add parser test in `src/TokenSqueeze.Tests/Parser/`
5. Check all switch/if branches in `src/TokenSqueeze/Parser/SymbolExtractor.cs` for language-specific logic

**New Model:**
- Add to `src/TokenSqueeze/Models/` as `sealed record` with `required init` properties

**New Storage Operation:**
- Add method to `src/TokenSqueeze/Storage/IndexStore.cs`
- MUST call `PathValidator.ValidateWithinRoot()` on any path derived from user input
- Use `AtomicWrite()` for any file writes

**New Security Check:**
- Add to `src/TokenSqueeze/Security/` as `internal static class`
- Wire into `DirectoryWalker` (for indexing) or `IndexStore` (for persistence)

**Utilities:**
- Shared JSON helpers: `src/TokenSqueeze/Infrastructure/`
- Path/storage helpers: `src/TokenSqueeze/Storage/StoragePaths.cs`

## Special Directories

**`~/.token-squeeze/projects/` (runtime storage):**
- Purpose: Persisted index data per project
- Structure: `<projectName>/manifest.json`, `<projectName>/search-index.json`, `<projectName>/files/*.json`
- Generated: Yes (at runtime by `IndexStore`)
- Committed: No (user home directory)
- Test override: `StoragePaths.TestRootOverride` redirects to temp directory

**`plugin/bin/`:**
- Purpose: Published self-contained binaries (win-x64, osx-arm64)
- Generated: Yes (by `plugin/build.sh` / `dotnet publish`)
- Committed: Yes (distributed with plugin)

**`.planning/`:**
- Purpose: GSD planning and analysis documents
- Generated: Yes (by GSD tooling)
- Committed: Yes

---

*Structure analysis: 2026-03-08*
