# Phase 1: Foundation - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Reshape all data models, path resolution, and security boundaries to reflect the local-storage-only world. After this phase, `ProjectName` is gone from all models, storage paths resolve to `<cwd>/.cache/`, DirectoryWalker skips `.cache/`, and PathValidator scopes to the cache directory. No command-level changes yet — those are Phase 3.

</domain>

<decisions>
## Implementation Decisions

### Cache directory naming
- Use `.cache/` as specified in the original design doc
- Research flagged collision risk with other tools (Prettier, ESLint, pytest use `.cache/<toolname>/`)
- Decision: stick with `.cache/` — TokenSqueeze owns the full directory, and the self-ignoring `.gitignore` + `CACHEDIR.TAG` (Phase 2) make it clear this is regenerable data
- If collision becomes a real problem, it's a simple rename later

### Model changes
- `CodeIndex`: remove `ProjectName` property entirely, keep `SourcePath`
- `Manifest`: remove `ProjectName` property, keep `SourcePath` and `FormatVersion`
- `ManifestHeader`: delete entirely — it existed only for the `list` command which is being removed
- `ManifestFileEntry`: unchanged (no project reference)

### StoragePaths restructure
- Replace static class with instance-based or collapse into IndexStore (Phase 2 decides final home)
- For this phase: make `StoragePaths` resolve all paths relative to a cache directory parameter instead of `~/.token-squeeze/projects/<name>/`
- Remove: `RootDir` (global), `CatalogPath`, `GetProjectDir()`, `GetLegacyIndexPath()`, `GetMetadataPath()`, `EnsureRootExists()`
- Keep: `PathToStorageKey()` (pure utility, no project concept), `GetManifestPath()`, `GetFilesDir()`, `GetFileFragmentPath()`, `GetSearchIndexPath()` — but all take a cache directory root instead of project name
- Remove `TestRootOverride` hack — constructor injection replaces it in Phase 2

### DirectoryWalker exclusion
- Add `.cache` to `SkippedDirectories` set in `DirectoryWalker.cs`
- Must happen in this phase to prevent index-indexes-itself when storage moves to project root

### Security boundary
- `PathValidator.ValidateWithinRoot` calls in `IndexStore` must use the cache directory (`.cache/`) as the root boundary, not the project root
- This prevents crafted storage keys from reading/overwriting source files
- `PathValidator` class itself is unchanged — it's generic. The callers change what they pass as `rootDir`

### SourcePath handling
- Keep `SourcePath` in `CodeIndex` and `Manifest` — it records what directory was indexed
- Store as absolute path (same as current behavior)
- `ExtractCommand` and `StalenessChecker` both use `SourcePath` to locate original files on disk

### Claude's Discretion
- Whether to make `StoragePaths` an instance class or keep it static with a parameter — as long as it doesn't depend on project names
- Internal naming of path resolution methods
- Whether to bump `FormatVersion` in Manifest (probably yes, since the format is incompatible)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PathValidator.ValidateWithinRoot()`: Generic path containment check — works with any root directory, no changes needed to the method itself
- `PathValidator.IsSymlinkEscape()`: Same — generic, no changes needed
- `StoragePaths.PathToStorageKey()`: Pure function converting relative paths to storage keys — completely independent of project concept, keep as-is

### Established Patterns
- Models use `sealed record` with `required init` properties — follow same pattern for any model changes
- `StoragePaths` is currently a `static class` — all methods are pure functions given a project name
- `DirectoryWalker.SkippedDirectories` is a `static readonly HashSet<string>` with case-insensitive comparison — just add `.cache` to it

### Integration Points
- `IndexStore` is the primary consumer of `StoragePaths` — every method calls into it
- `IndexCommand` calls `StoragePaths.EnsureRootExists()` — this call chain will change
- `ListCommand` uses `ManifestHeader` — both are being deleted (Phase 3)
- `Manifest.ProjectName` is read during `IndexStore.Load()` and written during `IndexStore.Save()` — serialization changes

</code_context>

<specifics>
## Specific Ideas

- The die-hook-die doc is explicit: `.cache/` not `.token-squeeze/` for the directory name — "shorter, conventional for caches"
- No migration code — clean break from old `~/.token-squeeze/` format
- Research recommended `.token-squeeze/` but the doc author chose `.cache/` — respect that decision

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-foundation*
*Context gathered: 2026-03-09*
