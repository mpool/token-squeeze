# External Integrations

**Analysis Date:** 2026-03-08

## APIs & External Services

**None.** TokenSqueeze is a fully offline CLI tool. It makes no network calls, connects to no APIs, and requires no external services.

## Data Storage

**Databases:**
- None. All data is stored as JSON files on the local filesystem.

**File Storage:**
- Local filesystem only
  - Index root: `~/.token-squeeze/projects/` (defined in `src/TokenSqueeze/Storage/StoragePaths.cs`)
  - Per-project index: `~/.token-squeeze/projects/<name>/index.json`
  - Managed by `src/TokenSqueeze/Storage/IndexStore.cs`
  - Uses atomic write pattern (write to `.tmp`, then `File.Move` with overwrite)

**Caching:**
- Incremental indexing serves as a cache: unchanged files (by SHA256 hash) reuse previously extracted symbols
  - Implemented in `src/TokenSqueeze/Indexing/ProjectIndexer.cs`

## Authentication & Identity

**Auth Provider:**
- Not applicable. No authentication required.

## Monitoring & Observability

**Error Tracking:**
- None. Errors are written as JSON to stdout via `src/TokenSqueeze/Infrastructure/JsonOutput.cs`

**Logs:**
- Diagnostic messages written to stderr (e.g., tree-sitter load check in `src/TokenSqueeze/Program.cs`, indexing stats in `src/TokenSqueeze/Indexing/ProjectIndexer.cs`)
- Structured JSON errors to stdout for machine consumption

## CI/CD & Deployment

**Hosting:**
- Distributed as a Claude Code plugin with pre-built self-contained binaries
- No server deployment; runs locally on developer machines

**CI Pipeline:**
- Not detected. No CI configuration files (`.github/workflows/`, `.gitlab-ci.yml`, etc.) present.

**Build:**
- Manual build via `plugin/build.sh`
- Produces self-contained single-file executables for win-x64 and osx-arm64

## Environment Configuration

**Required env vars:**
- None

**Secrets location:**
- Not applicable. No secrets needed.

## Claude Code Plugin Integration

**Plugin Manifest:** `plugin/.claude-plugin/plugin.json`
- Declares the plugin for Claude Code's plugin system
- Version: 1.0.0

**Skills (slash commands):**
- `plugin/skills/index/SKILL.md` - `/token-squeeze:index`
- `plugin/skills/list/SKILL.md` - `/token-squeeze:list`
- `plugin/skills/purge/SKILL.md` - `/token-squeeze:purge`
- `plugin/skills/outline/SKILL.md` - `/token-squeeze:outline`
- `plugin/skills/extract/SKILL.md` - `/token-squeeze:extract`
- `plugin/skills/find/SKILL.md` - `/token-squeeze:find`

**Hooks:**
- `plugin/hooks/hooks.json` - SessionStart hook runs auto-index script on Claude Code session start
- `plugin/scripts/auto-index.sh` - Unix auto-index trigger
- `plugin/scripts/auto-index.ps1` - Windows auto-index trigger

**Plugin Settings:**
- `plugin/settings.json` - Contains `auto_reindex: false`

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

## Native Library Dependencies

**TreeSitter.DotNet** bundles native tree-sitter grammar libraries:
- Loaded at startup via P/Invoke (validated in `src/TokenSqueeze/Program.cs` smoke test)
- Grammars for: Python, JavaScript, TypeScript, TSX, C-Sharp, C, Cpp
- Bundled into the self-contained executable via `IncludeNativeLibrariesForSelfExtract`
- Failure to load triggers a `DllNotFoundException` caught at startup with JSON error output

## Third-Party File Format Dependencies

**Gitignore Parsing:**
- `Ignore` NuGet package (0.2.1) used in `src/TokenSqueeze/Indexing/DirectoryWalker.cs`
- Reads `.gitignore` from the indexed project root to skip ignored files

---

*Integration audit: 2026-03-08*
