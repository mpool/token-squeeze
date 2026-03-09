# External Integrations

**Analysis Date:** 2026-03-08

## APIs & External Services

**GitHub Releases API (installer only):**
- Used by `installer/install.js` to download platform binaries during `npx token-squeeze` installation
- Endpoint: `https://api.github.com/repos/mpool/token-squeeze/releases/latest`
- Auth: None (public repo, unauthenticated API calls)
- Rate limit: Subject to GitHub's anonymous rate limit (60 req/hr)

**No runtime external APIs.** The CLI operates entirely offline against the local filesystem. No network calls are made during `index`, `list`, `purge`, `outline`, `extract`, or `find` commands.

## Data Storage

**Databases:**
- None. All data stored as JSON files on local filesystem.

**File Storage:**
- Local filesystem only
- Root: `~/.token-squeeze/projects/` (defined in `src/TokenSqueeze/Storage/StoragePaths.cs`)
- Structure per project:
  - `manifest.json` - File manifest with hashes, storage keys, timestamps
  - `search-index.json` - Lightweight symbol index for search queries
  - `files/*.json` - Per-file symbol fragment files
- Atomic writes via temp file + `File.Move` with retry logic in `src/TokenSqueeze/Storage/IndexStore.cs`

**Caching:**
- None. Index is the cache. Staleness checked via file hashes and timestamps at query time.

## Authentication & Identity

**Auth Provider:**
- Not applicable. CLI tool with no authentication. Operates under the invoking user's filesystem permissions.

## Monitoring & Observability

**Error Tracking:**
- None. Errors written to stdout as JSON via `JsonOutput.WriteError()` in `src/TokenSqueeze/Infrastructure/JsonOutput.cs`

**Logs:**
- stderr only, via `Console.Error.WriteLine()` for diagnostic/progress messages
- No structured logging framework
- No log files

## CI/CD & Deployment

**Hosting:**
- GitHub repository: `mpool/token-squeeze`
- Binaries distributed via GitHub Releases (zip for Windows, tar.gz for macOS)

**CI Pipeline:**
- No CI/CD configuration detected (no `.github/workflows/`, no Dockerfile, no CI config files)

**Release Process:**
- Manual: run `plugin/build.sh` to produce binaries, then create GitHub Release with assets
- Installer (`installer/install.js`) fetches latest release assets at install time

## Claude Code Plugin Integration

**Plugin System:**
- Integrates as a Claude Code plugin installed to `~/.claude/plugins/token-squeeze/`
- Plugin manifest: `plugin/.claude-plugin/plugin.json`
- 6 skills defined in `plugin/skills/`:
  - `plugin/skills/index/SKILL.md` - Index a directory
  - `plugin/skills/outline/SKILL.md` - Show file symbols
  - `plugin/skills/extract/SKILL.md` - Get symbol source
  - `plugin/skills/find/SKILL.md` - Search symbols
  - `plugin/skills/list/SKILL.md` - List projects
  - `plugin/skills/purge/SKILL.md` - Remove index

**Hooks:**
- `SessionStart` hook defined in `plugin/hooks/hooks.json`
- Runs `plugin/scripts/auto-index.sh` (or `.ps1` on Windows) on Claude Code session start
- Timeout: 120 seconds
- Purpose: Auto-reindex current project if stale

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

## Environment Configuration

**Required env vars:**
- None for CLI operation

**Optional env vars:**
- `CLAUDE_CONFIG_DIR` - Override Claude Code config directory (used by installer only)

**Secrets location:**
- No secrets required. No API keys, tokens, or credentials needed for any operation.

## Tree-sitter Native Libraries

**Native dependency:**
- TreeSitter.DotNet 1.3.0 bundles native tree-sitter C libraries
- Platform-specific native binaries extracted at runtime from the self-contained publish
- `IncludeNativeLibrariesForSelfExtract=true` ensures native libs are included in single-file binary
- Language grammars loaded by language ID string (e.g., `"Python"`, `"JavaScript"`, `"C-Sharp"`)
- Native handles require explicit disposal via `LanguageRegistry.Dispose()` (`src/TokenSqueeze/Parser/LanguageRegistry.cs`)

---

*Integration audit: 2026-03-08*
