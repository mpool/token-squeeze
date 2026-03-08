# TokenSqueeze

## Project Overview

C# .NET 9 CLI + Claude Code plugin for tree-sitter-based codebase indexing and symbol retrieval. 

## Architecture

```
token-squeeze/
├── src/
│   └── TokenSqueeze/          # .NET 9 console app (single project)
│       ├── TokenSqueeze.csproj
│       ├── Program.cs          # CLI entry point (Spectre.Console.Cli)
│       ├── Commands/           # CLI command handlers
│       ├── Infrastructure/     # Shared helpers
│       │   └── JsonOutput.cs
│       ├── Parser/             # tree-sitter AST extraction
│       │   ├── LanguageSpec.cs
│       │   ├── LanguageRegistry.cs
│       │   └── SymbolExtractor.cs
│       ├── Models/             # Symbol, CodeIndex, etc.
│       ├── Storage/            # IndexStore (JSON + raw files)
│       └── Security/           # Path traversal, symlink, secrets
├── .planning/                  # GSD planning docs
└── CLAUDE.md                   # This file
```

## Build & Run

```bash
dotnet build src/TokenSqueeze/TokenSqueeze.csproj
dotnet run --project src/TokenSqueeze/TokenSqueeze.csproj -- <command> [args]

# Release (cross-platform self-contained binaries)
dotnet publish src/TokenSqueeze/TokenSqueeze.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish src/TokenSqueeze/TokenSqueeze.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

## Key Conventions

- **All C# source goes in `src/`** — single solution, single project
- **Target:** net9.0
- **CLI framework:** Spectre.Console.Cli
- **Parsing:** TreeSitter.DotNet
- **Storage:** `~/.token-squeeze/` (JSON index + raw files)

### Output Rules (critical)

- ALL stdout output MUST be valid JSON via `JsonOutput.Write()` or `JsonOutput.WriteError()` — never raw `Console.WriteLine()`
- Diagnostic/progress messages go to `Console.Error.WriteLine()` (stderr) only
- Use anonymous types for command output; named records for internal models only
- JSON uses camelCase naming, null properties omitted, single-line (no indentation)

### Code Style

- `sealed` on all concrete classes; `internal` for non-model types
- Records with `required` init-only properties for models
- Private fields prefixed with `_`; static readonly fields without prefix
- File-scoped namespaces: `namespace TokenSqueeze.Commands;`

## CLI Commands

| Command | Description |
|---------|-------------|
| `index <path>` | Index a local folder |
| `list` | List indexed folders |
| `purge <name>` | Delete index for a folder |
| `outline <name> <file>` | Show symbols in a file |
| `extract <name> <id>` | Get full source of a symbol |
| `find <name> <query>` | Search symbols by query |

## Languages Supported

Python, JavaScript, TypeScript, C#, C, C++

## Adding New Code

**New CLI command:**
1. Create `src/TokenSqueeze/Commands/FooCommand.cs` implementing `Command<FooCommand.Settings>`
2. Register in `Program.cs` via `config.AddCommand<FooCommand>("foo")`
3. Create `plugin/skills/foo/SKILL.md` for Claude Code integration

**New language:**
1. Add `RegisterLanguageName()` in `LanguageRegistry.cs`, call from constructor
2. Add test fixture `tests/sample.ext`
3. Search all existing language ID strings (`"Python"`, `"JavaScript"`, etc.) to find every branch in `SymbolExtractor` that needs a case

## Known Bugs

- **OutlineCommand hierarchy broken:** `childrenByParent` keys on `s.Parent` (full ID like `path::ClassName#Class`) but lookups use `root.Name` (just `ClassName`). All symbols render flat — no nesting.
- **Method detection logic wrong:** `SymbolExtractor.cs:50` — `spec.ContainerNodeTypes.Any(ct => scopeParts.Count > 0)` ignores the `ct` parameter. Works by coincidence in typical cases.
- **GlobToRegex anchoring:** `FindCommand` `--path` patterns require full path match (`^...$`). Users must prefix with `**/` for non-rooted searches.

## What This Project Does NOT Have

- No MCP server protocol
- No telemetry / token tracking
- No file tree or full-text search commands (Claude has Glob/Grep)
- No automated tests (zero unit/integration tests — `tests/` contains only parser fixture files)
