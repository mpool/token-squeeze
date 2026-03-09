# TokenSqueeze

## Project Overview

C# .NET 9 CLI + Claude Code plugin for codebase indexing and symbol retrieval for token optimization. 

## Architecture

```
token-squeeze/
├── src/
│   ├── TokenSqueeze/              # .NET 9 console app
│   │   ├── TokenSqueeze.csproj
│   │   ├── Program.cs             # CLI entry point (Spectre.Console.Cli)
│   │   ├── Commands/              # CLI command handlers
│   │   ├── Infrastructure/        # Shared helpers
│   │   │   └── JsonOutput.cs
│   │   ├── Parser/                # AST extraction
│   │   │   ├── LanguageSpec.cs
│   │   │   ├── LanguageRegistry.cs
│   │   │   └── SymbolExtractor.cs
│   │   ├── Models/                # Symbol, CodeIndex, etc.
│   │   ├── Storage/               # IndexStore (JSON + raw files)
│   │   └── Security/              # Path traversal, symlink, secrets
│   ├── TokenSqueeze.Tests/        # xUnit test project
│   │   ├── TokenSqueeze.Tests.csproj
│   │   ├── SmokeTest.cs
│   │   └── Fixtures/              # Parser test fixture files (sample.*)
│   └── token-squeeze.sln
├── plugin/                        # Claude Code plugin (installed via npx)
│   ├── .claude-plugin/            # Plugin manifest
│   ├── .mcp.json                  # MCP server registration (stdio)
│   ├── mcp-server.js              # MCP server wrapping CLI binary
│   ├── skills/                    # Skill definitions
│   ├── hooks/                     # Hooks (auto-index on session start)
│   ├── scripts/                   # Hook helper scripts
│   ├── bin/                       # Published platform binaries
│   └── build.sh                   # Cross-platform build script
├── installer/                     # npx installer (copies plugin to ~/.claude/)
│   └── install.js
├── package.json                   # npm package for `npx token-squeeze`
├── .planning/                     # GSD planning docs
└── CLAUDE.md                      # This file
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

- **All C# source goes in `src/`** — single solution, two projects (app + tests)
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
2. Add test fixture `src/TokenSqueeze.Tests/Fixtures/sample.ext`
3. Search all existing language ID strings (`"Python"`, `"JavaScript"`, etc.) to find every branch in `SymbolExtractor` that needs a case

## What This Project Does NOT Have

- MCP server is a thin stdio wrapper around the CLI (plugin/mcp-server.js, zero dependencies)
- No telemetry / token tracking (/token-squeeze:savings for savings estimates)
- No file tree commands (use CLAUDE.md or other artifact)
- No full-text search commands (Claude has Glob/Grep)
