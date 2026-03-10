# Die Hook Die — Local Storage Simplification

## What This Is

A major simplification of TokenSqueeze that kills the SessionStart hook, removes the "project" abstraction (named projects in `~/.token-squeeze/`), and moves to local `.cache/` storage in the project root. The user runs `/token-squeeze:index` once per project — no hooks, no prompts, no opt-in dance.

## Core Value

Eliminate all complexity that doesn't serve the core job: index a codebase directory and answer symbol queries against it. One directory, one index, no indirection.

## Requirements

### Validated

- ✓ Multi-language AST parsing (Python, JS, TS, C#, C, C++) — existing
- ✓ Incremental indexing via SHA-256 content hashing — existing
- ✓ Symbol extraction (functions, classes, methods, constants, types) — existing
- ✓ CLI commands: index, outline, extract, find — existing
- ✓ MCP server wrapping CLI for Claude Code integration — existing
- ✓ Security: path traversal prevention, symlink escape detection, secret filtering — existing
- ✓ Cross-platform self-contained binaries (win-x64, osx-arm64) — existing
- ✓ Crash-safe atomic writes — existing
- ✓ Query-time staleness detection and incremental reindex — existing

### Active

- [ ] Storage moved from `~/.token-squeeze/projects/<name>/` to `<project-root>/.cache/`
- [ ] CLI commands drop project name argument (operate on cwd `.cache/`)
- [ ] `list` and `purge` CLI commands removed
- [ ] MCP tools drop `project` parameter, `list_projects` tool removed
- [ ] MCP server checks for `.cache/` existence and returns helpful error if missing
- [ ] SessionStart hook and auto-index script deleted
- [ ] Models (`CodeIndex`, `Manifest`, `ManifestHeader`) drop `ProjectName`
- [ ] Index skill offers `.gitignore` update after successful index
- [ ] README and CLAUDE.md updated to emphasize index-first requirement
- [ ] Tests updated for new storage model

### Out of Scope

- New language support — not part of this simplification
- Token tracking / telemetry — deliberately excluded
- File tree commands — Claude has Glob/Grep
- Full-text search — Claude has Grep
- Global project catalog — the whole point is killing this

## Context

The SessionStart hook has been unreliable — it injects a prompt asking the user to enable token-squeeze, but the model ignores it. UserPromptSubmit with additionalContext had the same problem. Hook-injected context is unreliable for driving model behavior.

Meanwhile, the `enabled: false` setting in `settings.local.json` doesn't actually disable MCP tools — they're always registered. The opt-in/opt-out machinery is theater.

The "project" abstraction adds complexity with no benefit — you always index cwd and query cwd. Named projects, catalog management, list/purge commands are all overhead for a mapping that's always 1:1.

Existing codebase map at `.planning/codebase/` documents current architecture. Key areas affected: Storage layer (`StoragePaths`, `IndexStore`), Commands (all query commands), Models, MCP server, plugin hooks/skills.

## Constraints

- **Backwards compatibility**: None required — this is a breaking change. Old `~/.token-squeeze/` indexes are abandoned.
- **Storage location**: `.cache/` in project root (not `.token-squeeze/` — shorter, conventional for caches)
- **No runtime dependencies**: CLI remains self-contained single-file binary
- **JSON output contract**: All stdout remains valid JSON via `JsonOutput.Write()`

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| `.cache/` not `.token-squeeze/` for local dir | Shorter, conventional cache directory name | — Pending |
| No migration from old storage | Clean break simpler than migration code; users just re-index | — Pending |
| No hook replacement | Explicit `/token-squeeze:index` is more reliable than any hook mechanism | — Pending |
| MCP tools return error when no index | Better UX than silent failure; tells user exactly what to do | — Pending |

---
*Last updated: 2026-03-09 after initialization*
