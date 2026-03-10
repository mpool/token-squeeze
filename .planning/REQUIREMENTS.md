# Requirements: Die Hook Die — Local Storage Simplification

**Defined:** 2026-03-09
**Core Value:** Eliminate all complexity that doesn't serve the core job: index a codebase directory and answer symbol queries against it.

## v1 Requirements

### Storage

- [x] **STOR-01**: Index data stored in `<project-root>/.cache/` instead of `~/.token-squeeze/projects/<name>/`
- [x] **STOR-02**: `StoragePaths` resolves root from cwd, no project name in any path
- [x] **STOR-03**: `IndexStore` receives cache directory via constructor injection, no project-name parameters
- [x] **STOR-04**: Cache directory created only during `Save()`, not during construction or queries
- [x] **STOR-05**: `CACHEDIR.TAG` file written to `.cache/` on index creation
- [x] **STOR-06**: Self-ignoring `.gitignore` (containing `*`) written inside `.cache/` on creation

### Models

- [x] **MODL-01**: `CodeIndex` model drops `ProjectName` property
- [x] **MODL-02**: `Manifest` / `ManifestHeader` drop `ProjectName` property
- [x] **MODL-03**: `ManifestHeader` deleted if no longer needed

### CLI Commands

- [x] **CLI-01**: `index` command stores output in `<target>/.cache/`, no project naming
- [x] **CLI-02**: `outline` command drops `<name>` argument, reads from cwd `.cache/`
- [x] **CLI-03**: `extract` command drops `<name>` argument, reads from cwd `.cache/`
- [x] **CLI-04**: `find` command drops `<name>` argument, reads from cwd `.cache/`
- [x] **CLI-05**: `list` command deleted
- [x] **CLI-06**: `purge` command deleted
- [x] **CLI-07**: All query commands return clear error when `.cache/` doesn't exist: "No index found. Run /token-squeeze:index"
- [x] **CLI-08**: `Program.cs` removes `list` and `purge` command registrations

### MCP Server

- [x] **MCP-01**: `read_file_outline` tool drops `project` parameter
- [x] **MCP-02**: `read_symbol_source` tool drops `project` parameter
- [x] **MCP-03**: `search_symbols` tool drops `project` parameter
- [x] **MCP-04**: `list_projects` tool removed entirely
- [x] **MCP-05**: MCP server checks `.cache/manifest.json` exists before proceeding; returns helpful error if missing
- [x] **MCP-06**: MCP server passes correct cwd to CLI subprocess

### Plugin

- [x] **PLUG-01**: `hooks/hooks.json` deleted (SessionStart hook)
- [x] **PLUG-02**: `scripts/auto-index.sh` and `scripts/auto-index.ps1` deleted
- [x] **PLUG-03**: Index skill updated — emphasizes "run this first", includes `.gitignore` offer
- [x] **PLUG-04**: Explore skill updated — removes project name references
- [x] **PLUG-05**: Savings skill updated — removes project name references
- [x] **PLUG-06**: Purge skill deleted

### Security

- [x] **SEC-01**: `PathValidator.ValidateWithinRoot` scoped to cache directory, not project root
- [x] **SEC-02**: `DirectoryWalker` excludes `.cache/` directory (prevents index-indexes-itself)

### Documentation

- [x] **DOC-01**: `CLAUDE.md` updated with new CLI command table (no list/purge, no project name args)
- [x] **DOC-02**: `README.md` updated to emphasize index-first requirement

### Tests

- [x] **TEST-01**: Tests updated for new storage model (`.cache/` instead of global paths)
- [x] **TEST-02**: Test infrastructure uses constructor-injected cache directory (no `TestRootOverride` hack)

## v2 Requirements

### Polish

- **PLSH-01**: Cache size reporting in index output
- **PLSH-02**: Elapsed time in index output
- **PLSH-03**: Lock file for concurrent indexing prevention

## Out of Scope

| Feature | Reason |
|---------|--------|
| Migration from old `~/.token-squeeze/` format | Clean break — users just re-index. Migration code adds complexity for a one-time operation. |
| New language support | Not part of this simplification milestone |
| Token tracking / telemetry | Deliberately excluded from project scope |
| Background/watch mode | Explicit index + query-time staleness model is simpler and sufficient |
| Global configuration | Zero config is a feature — no `~/.config/token-squeeze/` |
| Named project aliases | The directory IS the project identity |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| STOR-01 | Phase 1 | Complete |
| STOR-02 | Phase 1 | Complete |
| STOR-03 | Phase 2 | Complete |
| STOR-04 | Phase 2 | Complete |
| STOR-05 | Phase 2 | Complete |
| STOR-06 | Phase 2 | Complete |
| MODL-01 | Phase 1 | Complete |
| MODL-02 | Phase 1 | Complete |
| MODL-03 | Phase 1 | Complete |
| CLI-01 | Phase 3 | Complete |
| CLI-02 | Phase 3 | Complete |
| CLI-03 | Phase 3 | Complete |
| CLI-04 | Phase 3 | Complete |
| CLI-05 | Phase 3 | Complete |
| CLI-06 | Phase 3 | Complete |
| CLI-07 | Phase 3 | Complete |
| CLI-08 | Phase 3 | Complete |
| MCP-01 | Phase 4 | Complete |
| MCP-02 | Phase 4 | Complete |
| MCP-03 | Phase 4 | Complete |
| MCP-04 | Phase 4 | Complete |
| MCP-05 | Phase 4 | Complete |
| MCP-06 | Phase 4 | Complete |
| PLUG-01 | Phase 4 | Complete |
| PLUG-02 | Phase 4 | Complete |
| PLUG-03 | Phase 4 | Complete |
| PLUG-04 | Phase 4 | Complete |
| PLUG-05 | Phase 4 | Complete |
| PLUG-06 | Phase 4 | Complete |
| SEC-01 | Phase 1 | Complete |
| SEC-02 | Phase 1 | Complete |
| DOC-01 | Phase 4 | Complete |
| DOC-02 | Phase 4 | Complete |
| TEST-01 | Phase 4 | Complete |
| TEST-02 | Phase 2 | Complete |

**Coverage:**
- v1 requirements: 35 total
- Mapped to phases: 35
- Unmapped: 0

---
*Requirements defined: 2026-03-09*
*Last updated: 2026-03-09 after roadmap creation*
