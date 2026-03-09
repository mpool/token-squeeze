# Technology Stack

**Analysis Date:** 2026-03-08

## Languages

**Primary:**
- C# (.NET 9) - All application logic in `src/TokenSqueeze/` and `src/TokenSqueeze.Tests/`

**Secondary:**
- JavaScript (Node.js) - Installer script `installer/install.js` and npm package distribution
- Bash - Build script `plugin/build.sh`, hook script `plugin/scripts/auto-index.sh`
- PowerShell - Hook script `plugin/scripts/auto-index.ps1`

## Runtime

**Environment:**
- .NET 9.0 (target framework `net9.0`)
- Node.js (installer only, no runtime dependency for the CLI itself)

**Package Manager:**
- NuGet (via `dotnet` CLI) for C# dependencies
- npm for plugin distribution (`package.json` at repo root)
- Lockfile: No `packages.lock.json` detected; npm lockfile not committed

## Frameworks

**Core:**
- Spectre.Console.Cli 0.53.1 - CLI command parsing and routing (`src/TokenSqueeze/Program.cs`)
- TreeSitter.DotNet 1.3.0 - Tree-sitter bindings for AST parsing (`src/TokenSqueeze/Parser/`)

**Testing:**
- xUnit 2.9.2 - Test framework (`src/TokenSqueeze.Tests/`)
- Spectre.Console.Testing 0.49.1 - CLI command testing utilities
- Microsoft.NET.Test.Sdk 17.12.0 - Test runner infrastructure
- coverlet.collector 6.0.2 - Code coverage collection

**Build/Dev:**
- dotnet CLI - Build, test, publish
- dotnet publish - Self-contained single-file binaries (win-x64, osx-arm64)

## Key Dependencies

**Critical:**
- `TreeSitter.DotNet` 1.3.0 - Core parsing engine; wraps native tree-sitter C library with .NET bindings. Manages native handles requiring explicit disposal via `LanguageRegistry.Dispose()` in `src/TokenSqueeze/Parser/LanguageRegistry.cs`
- `Spectre.Console.Cli` 0.53.1 - Entire CLI command structure depends on this. Commands implement `Command<TSettings>` pattern. Registered in `src/TokenSqueeze/Program.cs`

**Infrastructure:**
- `Microsoft.Extensions.DependencyInjection` 9.0.* - Service registration for `LanguageRegistry` and `IndexStore` as singletons. Custom `TypeRegistrar` in `src/TokenSqueeze/Infrastructure/`
- `Ignore` 0.2.1 - `.gitignore` pattern matching for directory walking in `src/TokenSqueeze/Security/`
- `System.Text.Json` (framework) - All JSON serialization. Configured in `src/TokenSqueeze/Infrastructure/JsonDefaults.cs` with camelCase naming, no indentation, null omission

## Configuration

**Environment:**
- No environment variables required for the CLI itself
- `CLAUDE_CONFIG_DIR` - Optional; used by installer (`installer/install.js`) to locate Claude Code plugin directory (defaults to `~/.claude/`)

**Build:**
- `src/TokenSqueeze/TokenSqueeze.csproj` - Main project, self-contained publish settings
- `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` - Test project with `InternalsVisibleTo` from main project
- `src/token-squeeze.sln` - Solution file binding both projects

**JSON Serialization (global defaults in `src/TokenSqueeze/Infrastructure/JsonDefaults.cs`):**
- `WriteIndented = false`
- `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`

## Storage

**Data Location:** `~/.token-squeeze/projects/` (defined in `src/TokenSqueeze/Storage/StoragePaths.cs`)
- Per-project directories containing `manifest.json`, `search-index.json`, and `files/*.json` fragments
- No database; all persistence is JSON files on local filesystem
- `StoragePaths.TestRootOverride` allows test isolation

## Publish Targets

**Platforms (defined in `plugin/build.sh` and `installer/install.js`):**
- `win-x64` - Windows self-contained single-file EXE
- `osx-arm64` - macOS Apple Silicon self-contained binary

**Publish flags:**
- `PublishSingleFile=true`
- `SelfContained=true`
- `IncludeNativeLibrariesForSelfExtract=true` (required for TreeSitter native libs)
- `IncludeAllContentForSelfExtract=true`

## Plugin Distribution

**npm package:** `token-squeeze` (via `npx token-squeeze`)
- `package.json` at repo root defines bin entry pointing to `installer/install.js`
- Installer copies plugin files to `~/.claude/plugins/token-squeeze/`
- Downloads platform binary from GitHub Releases API
- Plugin manifest: `plugin/.claude-plugin/plugin.json`

## Platform Requirements

**Development:**
- .NET 9 SDK
- Node.js (for installer testing only)
- Git

**Production:**
- No runtime dependencies - self-contained single-file binary includes .NET runtime
- Filesystem access to `~/.token-squeeze/` for index storage

---

*Stack analysis: 2026-03-08*
