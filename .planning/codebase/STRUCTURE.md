# Codebase Structure

**Analysis Date:** 2026-03-08

## Directory Layout

```
token-squeeze/
├── src/
│   ├── token-squeeze.sln       # Solution file (single project)
│   └── TokenSqueeze/           # Main .NET 9 console application
│       ├── TokenSqueeze.csproj  # Project file (net9.0, self-contained publish)
│       ├── Program.cs           # CLI entry point
│       ├── Commands/            # Spectre.Console.Cli command handlers
│       ├── Indexing/            # Directory walking + project indexer orchestration
│       ├── Infrastructure/      # Cross-cutting helpers (JSON output)
│       ├── Models/              # Data records (Symbol, CodeIndex, IndexedFile)
│       ├── Parser/              # Tree-sitter integration + symbol extraction
│       ├── Security/            # Path validation + secret detection
│       └── Storage/             # JSON index persistence to ~/.token-squeeze/
├── plugin/                      # Claude Code plugin
│   ├── .claude-plugin/
│   │   └── plugin.json          # Plugin manifest
│   ├── bin/                     # Published platform binaries
│   │   ├── osx-arm64/           # macOS ARM binary
│   │   └── win-x64/             # Windows x64 binary
│   ├── hooks/
│   │   └── hooks.json           # SessionStart hook config
│   ├── scripts/
│   │   ├── auto-index.sh        # Auto-index on session start (bash)
│   │   └── auto-index.ps1       # Auto-index on session start (PowerShell)
│   ├── settings.json            # Plugin settings (auto_reindex toggle)
│   └── skills/                  # Slash command definitions
│       ├── extract/SKILL.md
│       ├── find/SKILL.md
│       ├── index/SKILL.md
│       ├── list/SKILL.md
│       ├── outline/SKILL.md
│       └── purge/SKILL.md
├── tests/                       # Test fixture files (sample source files)
│   ├── sample.py
│   ├── sample.ts
│   ├── sample.tsx
│   ├── sample.js
│   ├── sample.cs
│   ├── sample.c
│   ├── sample.cpp
│   └── sample.h
├── .planning/                   # GSD planning documents
│   ├── codebase/                # Codebase analysis (this file lives here)
│   └── research/                # Research notes
├── CLAUDE.md                    # Project instructions for Claude
└── .gitignore
```

## Directory Purposes

**`src/TokenSqueeze/Commands/`:**
- Purpose: One file per CLI command, each a Spectre.Console.Cli `Command<TSettings>` subclass
- Contains: 7 command files, each with nested `Settings` class for argument/option binding
- Key files: `IndexCommand.cs` (core indexing), `ExtractCommand.cs` (symbol source retrieval), `FindCommand.cs` (search with scoring)

**`src/TokenSqueeze/Indexing/`:**
- Purpose: Orchestration layer between CLI commands and Parser/Storage
- Contains: `ProjectIndexer.cs` (incremental indexing logic), `DirectoryWalker.cs` (filtered file enumeration)
- Key files: `ProjectIndexer.cs` is the main indexing pipeline

**`src/TokenSqueeze/Parser/`:**
- Purpose: Tree-sitter AST parsing and symbol extraction, language configurations
- Contains: `SymbolExtractor.cs` (AST walker, ~470 lines -- largest file), `LanguageRegistry.cs` (all language specs + parser pooling), `LanguageSpec.cs` (config record)
- Key files: `LanguageRegistry.cs` is where new language support is added

**`src/TokenSqueeze/Models/`:**
- Purpose: Pure data types shared across all layers
- Contains: `Symbol.cs` (core record + SymbolKind enum), `CodeIndex.cs` (project aggregate), `IndexedFile.cs` (file metadata)
- Key files: `Symbol.cs` defines the `SymbolKind` enum and ID format

**`src/TokenSqueeze/Storage/`:**
- Purpose: Persistence to `~/.token-squeeze/projects/<name>/index.json`
- Contains: `IndexStore.cs` (Save/Load/List/Delete), `StoragePaths.cs` (path constants)
- Key files: `StoragePaths.cs` defines the root directory `~/.token-squeeze/projects/`

**`src/TokenSqueeze/Infrastructure/`:**
- Purpose: Shared output formatting
- Contains: `JsonOutput.cs` only (static helper)
- Key files: `JsonOutput.cs` -- all stdout output goes through this

**`src/TokenSqueeze/Security/`:**
- Purpose: Defensive checks for path traversal and secret file exposure
- Contains: `PathValidator.cs` (symlink escape, root boundary), `SecretDetector.cs` (known secret filenames/extensions)
- Key files: Both files are small, static utility classes

**`plugin/`:**
- Purpose: Claude Code plugin that wraps the CLI binary as slash commands
- Contains: Plugin manifest, 6 skill definitions (SKILL.md files), session hook, platform binaries
- Key files: `plugin/skills/*/SKILL.md` define the slash command behavior

**`tests/`:**
- Purpose: Sample source files in each supported language for manual/automated testing of the parser
- Contains: 8 sample files (`.py`, `.ts`, `.tsx`, `.js`, `.cs`, `.c`, `.cpp`, `.h`)
- Key files: Used as input for `parse-test` command and parser verification

## Key File Locations

**Entry Points:**
- `src/TokenSqueeze/Program.cs`: CLI entry point, command routing, tree-sitter smoke test

**Configuration:**
- `src/TokenSqueeze/TokenSqueeze.csproj`: .NET 9 project, dependencies (Spectre.Console.Cli, TreeSitter.DotNet, Ignore)
- `src/token-squeeze.sln`: Solution file (single project)
- `plugin/settings.json`: Plugin runtime settings
- `plugin/.claude-plugin/plugin.json`: Plugin manifest

**Core Logic:**
- `src/TokenSqueeze/Parser/SymbolExtractor.cs`: AST walking + symbol building (~470 lines)
- `src/TokenSqueeze/Parser/LanguageRegistry.cs`: All language definitions (~315 lines)
- `src/TokenSqueeze/Indexing/ProjectIndexer.cs`: Incremental indexing pipeline
- `src/TokenSqueeze/Indexing/DirectoryWalker.cs`: Multi-stage file filtering

**Testing:**
- `tests/sample.*`: Parser test fixtures (one per supported language)

## Naming Conventions

**Files:**
- PascalCase for all C# source files: `SymbolExtractor.cs`, `IndexCommand.cs`
- One class per file, file name matches class name
- Commands suffixed with `Command`: `IndexCommand.cs`, `FindCommand.cs`

**Directories:**
- PascalCase for C# source directories: `Commands/`, `Parser/`, `Models/`
- lowercase for non-C# directories: `plugin/`, `tests/`, `scripts/`

**Namespaces:**
- Follow directory structure: `TokenSqueeze.Commands`, `TokenSqueeze.Parser`, `TokenSqueeze.Models`
- File-scoped namespace declarations throughout: `namespace TokenSqueeze.Commands;`

**Skills:**
- Directory name matches command name: `skills/index/`, `skills/find/`
- Each contains a single `SKILL.md`

## Where to Add New Code

**New CLI Command:**
1. Create `src/TokenSqueeze/Commands/FooCommand.cs` implementing `Command<FooCommand.Settings>`
2. Register in `src/TokenSqueeze/Program.cs` via `config.AddCommand<FooCommand>("foo")`
3. Create corresponding `plugin/skills/foo/SKILL.md` for Claude Code integration

**New Language Support:**
1. Add a `RegisterLanguageName()` method in `src/TokenSqueeze/Parser/LanguageRegistry.cs`
2. Call it from the `LanguageRegistry` constructor
3. Add test fixture file in `tests/sample.ext`
4. No changes needed elsewhere -- `DirectoryWalker` and `SymbolExtractor` are language-agnostic

**New Model/Data Type:**
- Add to `src/TokenSqueeze/Models/` as a sealed record

**New Security Check:**
- Add to `src/TokenSqueeze/Security/` as a static utility class
- Wire into `DirectoryWalker.Walk()` filter pipeline

**New Infrastructure/Helper:**
- Add to `src/TokenSqueeze/Infrastructure/`

**New Test Fixture:**
- Add sample file to `tests/` directory

## Special Directories

**`plugin/bin/`:**
- Purpose: Platform-specific published binaries (self-contained .NET executables)
- Generated: Yes, via `dotnet publish` / `plugin/build.sh`
- Committed: Yes (binaries are tracked for plugin distribution)

**`~/.token-squeeze/projects/`:**
- Purpose: Runtime data directory for persisted indexes
- Generated: Yes, created on first `index` command run
- Committed: No (lives in user home directory, not in repo)

**`.planning/`:**
- Purpose: GSD planning and analysis documents
- Generated: By GSD workflow tools
- Committed: Varies (some phases tracked, codebase docs generated per-session)

---

*Structure analysis: 2026-03-08*
