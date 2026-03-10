# Feature Landscape

**Domain:** CLI codebase indexing tool -- local storage simplification
**Researched:** 2026-03-09

## Table Stakes

Features users expect. Missing = the local storage model feels broken or incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| CWD-relative storage (`.cache/`) | Every tool that stores project-local data uses a dot-directory in the project root (ctags: `tags`, Cargo: `target/`, Next.js: `.next/`, ESLint: `.eslintcache`). Users expect the cache to live next to the code. | Low | Already decided in PROJECT.md. `.cache/` is short and conventional. |
| No project name argument | When storage is local, the project identity is the directory itself. Requiring a name is redundant indirection -- ctags doesn't name your tag set. | Low | Drop `<name>` from all query commands, derive from cwd. |
| Index-exists check with clear error | If no `.cache/` exists, every query command must fail fast with "Run `index` first" guidance. This is what LSPs do when no index is built. | Low | MCP tools must do this too. |
| Atomic/crash-safe writes | Already exists. Any local cache that corrupts on interrupted writes is unusable. Cargo, webpack caches, and this tool all use temp-file-then-rename. | None (keep) | Already implemented via `AtomicWrite`. |
| Incremental reindex via content hashing | Already exists. Full re-parse on every invocation is a non-starter for repos with >100 files. SHA-256 content hashing is the standard pattern (Cargo fingerprinting, webpack content hashes, existing TokenSqueeze approach). | None (keep) | Already implemented. |
| Query-time staleness detection | Already exists. When files change between `index` runs, queries should detect stale entries and incrementally reindex. Without this, users get wrong answers silently. | None (keep) | `QueryReindexer.EnsureFresh` already handles this. |
| `.gitignore`-friendliness | The `.cache/` directory must be easy to exclude from version control. Every local-cache tool does this: Cargo adds `/target` to `.gitignore`, Next.js adds `/.next`. | Low | Skill should offer to add `.cache/` to `.gitignore` after first index (already in PROJECT.md requirements). |
| Orphan cleanup | When files are deleted from the repo, their fragments must be cleaned from the cache. Otherwise the cache grows unboundedly. | None (keep) | Already implemented -- manifest-based orphan detection on save. |

## Differentiators

Features that improve UX beyond the baseline. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `CACHEDIR.TAG` file | Place a `CACHEDIR.TAG` in `.cache/` per the Cache Directory Tagging Specification. Backup tools (rsync, tar, borg, restic) recognize this and auto-exclude. Zero cost, signals "this is regenerable." | Very Low | Single static file write during index. Spec at bford.info/cachedir/. |
| Self-contained `.gitignore` inside `.cache/` | Instead of modifying the project's root `.gitignore`, write a `.gitignore` containing `*` inside `.cache/` itself. Cargo community discussed this pattern -- the directory self-ignores regardless of whether the project `.gitignore` mentions it. | Very Low | Do BOTH: offer root `.gitignore` update AND place a self-ignoring `.gitignore` inside `.cache/`. Belt and suspenders. |
| Index summary in output | After indexing, show file count, symbol count, languages detected, time elapsed. The user invoked `index` explicitly -- give them useful feedback. Already partially done. | None (keep) | Already outputs `filesIndexed`, `symbolsExtracted`. Could add `elapsedMs`, `languages`. |
| Cache size reporting | A way to see how much disk space `.cache/` uses. Useful for large repos. Not a separate command -- just include in index output. | Low | `du -sh .cache/` equivalent. |
| Storage key collision handling | Already exists. When different file paths map to the same storage key (after normalization), suffix with counter. Edge case but important for monorepos with deep nesting. | None (keep) | Already implemented in `Save()`. |

## Anti-Features

Features to deliberately NOT build. These are traps that add complexity without serving the "index once, query many" model.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| `list` command (list indexed projects) | There is no concept of "projects" anymore. One directory = one index. `list` only made sense with global `~/.token-squeeze/` storage. | Remove entirely. The user knows what directory they're in. |
| `purge` command (delete a project's index) | With local storage, purging is `rm -rf .cache/`. No CLI command needed for deleting a directory. | Remove entirely. Document `rm -rf .cache/` if users ask. |
| Global catalog (`catalog.json`) | Existed to support `list`. With local storage there's nothing to catalog. | Remove entirely, including the `UpdateCatalog()` method. |
| Named project aliases (`--name` flag) | The `--name` flag on `index` let users alias a project. With local storage the project IS the directory. Naming adds confusion. | Remove the flag. Project identity = directory path. |
| Legacy format migration | The old `~/.token-squeeze/` format is being abandoned. Migration code adds complexity for a one-time operation. Users just re-index. | Remove all `LegacyMigration` code. Clean break. |
| Background/watch mode | File watchers add process management complexity, platform-specific behavior, and battery drain. The tool's model is explicit: run `index`, then query. `QueryReindexer.EnsureFresh` handles staleness at query time. | Keep the explicit index + query-time staleness model. |
| Global configuration | No `~/.config/token-squeeze/` or global settings. The tool has no configuration surface -- it indexes the current directory with hardcoded behavior. Zero config is a feature. | If configuration ever needed, use CLI flags, not config files. |
| Remote/network storage | The cache is local. No syncing, no cloud, no shared indexes. | Local `.cache/` only. |
| Token counting/telemetry | Explicitly out of scope per PROJECT.md. The tool extracts symbols; it doesn't count tokens or track usage. | Keep out of scope. |

## Feature Dependencies

```
.cache/ directory creation  -->  All query commands (outline, extract, find)
                            -->  .gitignore update offer
                            -->  CACHEDIR.TAG placement
                            -->  Self-ignoring .gitignore inside .cache/

Manifest (manifest.json)    -->  Staleness detection (QueryReindexer)
                            -->  File fragment loading
                            -->  Search index

Drop project name arg       -->  Simplify all command signatures
                            -->  Simplify MCP tool parameters
                            -->  Remove list/purge commands
```

## MVP Recommendation

**Prioritize (do first):**
1. Move storage to `.cache/` in project root -- this is the core change everything depends on
2. Drop `<name>` argument from all query commands (outline, extract, find) -- operate on cwd
3. Remove `list`, `purge` commands and catalog infrastructure
4. Index-exists check with clear error message on all query paths
5. Update MCP tools to drop `project` parameter

**Include (low cost, high polish):**
6. Self-ignoring `.gitignore` inside `.cache/` (write `*` on creation)
7. `CACHEDIR.TAG` file in `.cache/` (write on creation)
8. Offer root `.gitignore` update after first index

**Defer (not needed for this milestone):**
- Cache size reporting -- nice but not essential
- Elapsed time in index output -- minor polish, can add anytime
- New language support -- explicitly out of scope

## Sources

- [Cache Directory Tagging Specification](https://bford.info/cachedir/)
- [Cargo .gitignore convention](https://github.com/rust-lang/cargo/issues/11548) -- self-ignoring directory pattern
- [ESLint cache location discussion](https://github.com/eslint/eslint/issues/13897) -- conventions for local cache placement
- [Universal Ctags docs](https://docs.ctags.io/en/latest/man/ctags.1.html) -- local `tags` file convention
- [XDG Base Directory convention](https://atmos.tools/changelog/macos-xdg-cli-conventions) -- cache directory standards
- Existing TokenSqueeze codebase: `IndexStore.cs`, `StoragePaths.cs`, `IndexCommand.cs`, `FindCommand.cs`, `OutlineCommand.cs`
