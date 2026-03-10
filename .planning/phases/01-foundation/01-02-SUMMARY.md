---
phase: 01-foundation
plan: 02
subsystem: storage
tags: [csharp, storage, cacheDir, security, indexing]

# Dependency graph
requires:
  - phase: 01-foundation plan 01
    provides: CodeIndex/Manifest without ProjectName, StoragePaths with cacheDir
provides:
  - IndexStore with cacheDir parameter threading
  - ProjectIndexer without project name resolution
  - IncrementalReindexer with cacheDir
  - QueryReindexer with cacheDir
  - Commands using local .cache directory
affects: [02-storage plan, 03-commands plan, all commands]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "cacheDir parameter: all store/indexer methods receive cache directory path"
    - "ResolveCacheDir in commands: derives .cache path from source directory"
    - "PathValidator.ValidateWithinRoot uses cacheDir as security boundary"

key-files:
  created: []
  modified:
    - src/TokenSqueeze/Storage/IndexStore.cs
    - src/TokenSqueeze/Indexing/ProjectIndexer.cs
    - src/TokenSqueeze/Indexing/IncrementalReindexer.cs
    - src/TokenSqueeze/Storage/QueryReindexer.cs
    - src/TokenSqueeze/Commands/IndexCommand.cs
    - src/TokenSqueeze/Commands/OutlineCommand.cs
    - src/TokenSqueeze/Commands/ExtractCommand.cs
    - src/TokenSqueeze/Commands/FindCommand.cs
    - src/TokenSqueeze/Commands/ListCommand.cs
    - src/TokenSqueeze/Commands/PurgeCommand.cs

key-decisions:
  - "Deleted LegacyMigration.cs entirely (depends on removed IndexStore methods)"
  - "Commands derive cacheDir as Path.Combine(sourceDir, '.cache') -- Phase 3 will finalize"
  - "ListCommand returns empty, PurgeCommand returns deprecation error (both removed Phase 3)"
  - "FormatVersion bumped to 3 (incompatible with old format)"
  - "Deleted ProjectNameSanitizationTests and LegacyMigrationTests (test removed functionality)"

patterns-established:
  - "cacheDir as root boundary: PathValidator.ValidateWithinRoot validates fragment paths against cacheDir"
  - "ResolveCacheDir pattern in commands: if name is directory, use it; otherwise derive from cwd"

requirements-completed: [SEC-01, STOR-01, STOR-02]

# Metrics
duration: 13min
completed: 2026-03-10
---

# Phase 1 Plan 2: Thread cacheDir Through All Callers Summary

**Rewrote IndexStore, ProjectIndexer, IncrementalReindexer, and all commands to use cacheDir parameter -- solution compiles and 266 tests pass**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-10T00:18:10Z
- **Completed:** 2026-03-10T00:31:00Z
- **Tasks:** 3
- **Files modified:** 32 (2 deleted)

## Accomplishments
- Rewrote IndexStore with all methods taking cacheDir instead of projectName, FormatVersion 3
- Removed all global storage concepts: ListProjects, Delete, UpdateCatalog, LoadCatalogJson, legacy migration
- Updated ProjectIndexer, IncrementalReindexer, QueryReindexer to thread cacheDir
- Updated all 6 CLI commands to use cacheDir (derived from source path)
- Updated all 32 test files to use new cacheDir-based API
- Full test suite passes: 266 passed, 0 failed, 0 skipped

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite IndexStore to use cacheDir parameter** - `a6e690f` (feat)
2. **Task 2: Update ProjectIndexer, IncrementalReindexer, and all callers** - `02c3bfe` (feat)
3. **Task 3: Verify all tests pass and clean up** - `7c8bacb` (fix)

## Files Created/Modified
- `src/TokenSqueeze/Storage/IndexStore.cs` - All methods take cacheDir, removed 10 deleted methods
- `src/TokenSqueeze/Indexing/ProjectIndexer.cs` - Removed ResolveProjectName/SanitizeName, takes cacheDir
- `src/TokenSqueeze/Indexing/IncrementalReindexer.cs` - ReindexFiles takes cacheDir parameter
- `src/TokenSqueeze/Storage/QueryReindexer.cs` - EnsureFresh takes cacheDir
- `src/TokenSqueeze/Storage/LegacyMigration.cs` - DELETED (referenced removed methods)
- `src/TokenSqueeze/Commands/IndexCommand.cs` - Derives cacheDir from indexed path
- `src/TokenSqueeze/Commands/OutlineCommand.cs` - ResolveCacheDir from name argument
- `src/TokenSqueeze/Commands/ExtractCommand.cs` - ResolveCacheDir from name argument
- `src/TokenSqueeze/Commands/FindCommand.cs` - ResolveCacheDir from name argument
- `src/TokenSqueeze/Commands/ListCommand.cs` - Stubbed (returns empty, deprecated)
- `src/TokenSqueeze/Commands/PurgeCommand.cs` - Stubbed (returns deprecation error)
- `src/TokenSqueeze.Tests/Storage/LegacyMigrationTests.cs` - DELETED
- `src/TokenSqueeze.Tests/Security/ProjectNameSanitizationTests.cs` - DELETED
- Plus 19 test files updated for new API signatures

## Decisions Made
- Deleted LegacyMigration.cs rather than stubbing (all its method calls were to deleted IndexStore methods)
- Commands derive cacheDir as `Path.Combine(sourceDir, ".cache")` -- minimal viable for compilation, Phase 3 will finalize command rewrites
- ListCommand and PurgeCommand stubbed rather than deleted (Program.cs registers them; full removal in Phase 3)
- Deleted test files for removed functionality rather than skipping (cleaner than Skip annotations for entirely dead code)
- Fixed traversal test assertion: single-level `..` in storage key stays within cacheDir boundary, changed to `../../` for proper escape test

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed path traversal test case for new cacheDir boundary**
- **Found during:** Task 3
- **Issue:** `..\\windows\\system32` as storage key resolved within cacheDir (files/../windows = cacheDir/windows), not outside it
- **Fix:** Changed to `..\\..\\windows\\system32` which properly escapes the cacheDir boundary
- **Files modified:** src/TokenSqueeze.Tests/Storage/IndexStoreValidationTests.cs
- **Verification:** Test passes, SecurityException thrown for escaping paths
- **Committed in:** 7c8bacb

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Minor test data correction for the new security boundary model. No scope creep.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All production code uses cacheDir pattern, no ProjectName references anywhere
- Commands have minimal cacheDir resolution (Phase 3 will do proper command rewrites)
- ListCommand/PurgeCommand stubbed for compilation (Phase 3 removes them)
- 266 tests pass with zero failures

---
*Phase: 01-foundation*
*Completed: 2026-03-10*
