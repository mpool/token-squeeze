# Technology Stack

**Analysis Date:** 2026-03-08

## Languages

**Primary:**
- C# 13 (implied by net9.0) - All application logic in `src/TokenSqueeze/`

**Secondary:**
- Bash - Build script (`plugin/build.sh`), auto-index hook (`plugin/scripts/auto-index.sh`)
- PowerShell - Auto-index hook for Windows (`plugin/scripts/auto-index.ps1`)
- JSON - Configuration, index storage, CLI output format

## Runtime

**Environment:**
- .NET 9.0 (target framework: `net9.0`)
- Self-contained deployment (no runtime dependency on target machines)

**Package Manager:**
- NuGet (implicit via .NET SDK)
- Lockfile: Not present (no `packages.lock.json`)

## Frameworks

**Core:**
- Spectre.Console.Cli 0.53.1 - CLI command routing and argument parsing (`src/TokenSqueeze/Program.cs`)

**Parsing:**
- TreeSitter.DotNet 1.3.0 - Tree-sitter bindings for AST parsing (`src/TokenSqueeze/Parser/`)

**Build/Dev:**
- .NET SDK (MSBuild) - Build system via `TokenSqueeze.csproj`
- No test framework detected in the project

## Key Dependencies

**Critical (3 total NuGet packages):**
- `Spectre.Console.Cli` 0.53.1 - CLI framework; defines the entire command structure. All commands inherit from `Command<TSettings>`.
- `TreeSitter.DotNet` 1.3.0 - Core parsing engine; provides `Language`, `Parser`, `Node` types used in `src/TokenSqueeze/Parser/SymbolExtractor.cs` and `src/TokenSqueeze/Parser/LanguageRegistry.cs`. Ships native binaries for tree-sitter grammars.
- `Ignore` 0.2.1 - .gitignore pattern matching; used in `src/TokenSqueeze/Indexing/DirectoryWalker.cs` to respect `.gitignore` rules during indexing.

**Standard Library Usage (no external packages):**
- `System.Text.Json` - All JSON serialization/deserialization (`src/TokenSqueeze/Infrastructure/JsonOutput.cs`, `src/TokenSqueeze/Storage/IndexStore.cs`)
- `System.Security.Cryptography` (SHA256) - File and symbol content hashing (`src/TokenSqueeze/Indexing/ProjectIndexer.cs`, `src/TokenSqueeze/Parser/SymbolExtractor.cs`)

## Configuration

**Project Settings:**
- `src/TokenSqueeze/TokenSqueeze.csproj`: Target framework, publish settings, package references
- `src/token-squeeze.sln`: Solution file (single project)
- `plugin/settings.json`: Plugin-level settings (currently only `auto_reindex: false`)
- `plugin/.claude-plugin/plugin.json`: Claude Code plugin manifest (name, version, description)
- `plugin/hooks/hooks.json`: Claude Code session hooks (auto-index on session start)

**Environment Variables:**
- None required. The application is fully self-contained with no external service dependencies.

**Build Configuration:**
- ImplicitUsings: enabled
- Nullable: enabled
- PublishSingleFile: true
- SelfContained: true
- IncludeNativeLibrariesForSelfExtract: true
- IncludeAllContentForSelfExtract: true

## Build & Publish

**Development:**
```bash
dotnet build src/TokenSqueeze/TokenSqueeze.csproj
dotnet run --project src/TokenSqueeze/TokenSqueeze.csproj -- <command> [args]
```

**Release (cross-platform self-contained binaries):**
```bash
# Uses plugin/build.sh which publishes for both targets:
dotnet publish src/TokenSqueeze/TokenSqueeze.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish src/TokenSqueeze/TokenSqueeze.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

**Output Locations:**
- `plugin/bin/win-x64/TokenSqueeze.exe`
- `plugin/bin/osx-arm64/TokenSqueeze`

## Platform Requirements

**Development:**
- .NET 9.0 SDK
- Windows or macOS (cross-compilation supported via `dotnet publish -r <RID>`)

**Production/Distribution:**
- No runtime needed (self-contained single-file executables)
- Target platforms: Windows x64, macOS ARM64
- Distributed as Claude Code plugin with pre-built binaries in `plugin/bin/`

## Data Storage

**Index Location:** `~/.token-squeeze/projects/<project-name>/index.json`
- Defined in `src/TokenSqueeze/Storage/StoragePaths.cs`
- JSON format with camelCase property naming
- Atomic writes via temp file + rename pattern (`src/TokenSqueeze/Storage/IndexStore.cs`)

---

*Stack analysis: 2026-03-08*
