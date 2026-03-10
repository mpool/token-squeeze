---
phase: 02-storage-layer
plan: 01
subsystem: storage
tags: [indexstore, constructor-injection, cachedir-tag, gitignore, di-removal]

requires:
  - phase: 01-foundation
    provides: IndexStore with cacheDir threaded through all callers
provides:
  - IndexStore with constructor-injected cacheDir (no method-level cacheDir params)
  - CACHEDIR.TAG and .gitignore marker creation on Save()
  - Commands construct IndexStore locally (not from DI)
affects: [03-command-layer, 04-plugin-layer]

tech-stack:
  added: []
  patterns: [constructor-injection for IndexStore, cache markers on save]

key-files:
  created: []
  modified:
    - src/TokenSqueeze/Storage/IndexStore.cs
    - src/TokenSqueeze/Storage/QueryReindexer.cs
    - src/TokenSqueeze/Program.cs
    - src/TokenSqueeze/Commands/IndexCommand.cs
    - src/TokenSqueeze/Commands/OutlineCommand.cs
    - src/TokenSqueeze/Commands/ExtractCommand.cs
    - src/TokenSqueeze/Commands/FindCommand.cs
    - src/TokenSqueeze/Commands/ListCommand.cs
    - src/TokenSqueeze/Commands/PurgeCommand.cs
    - src/TokenSqueeze/Indexing/ProjectIndexer.cs
    - src/TokenSqueeze/Indexing/IncrementalReindexer.cs
    - src/TokenSqueeze.Tests/Storage/SplitStorageTests.cs
    - src/TokenSqueeze.Tests/Storage/SearchIndexTests.cs
    - src/TokenSqueeze.Tests/Storage/SelectiveLoadTests.cs
    - src/TokenSqueeze.Tests/Storage/IndexStoreValidationTests.cs
    - src/TokenSqueeze.Tests/ReindexOnQueryTests.cs
    - src/TokenSqueeze.Tests/StalenessCheckerTests.cs
    - src/TokenSqueeze.Tests/Indexing/ParallelIndexingTests.cs
    - src/TokenSqueeze.Tests/RobustnessTests.cs
    - src/TokenSqueeze.Tests/Models/SearchSymbolTests.cs
    - src/TokenSqueeze.Tests/Helpers/CliTestHarness.cs

key-decisions:
  - "IndexStore removed from DI -- commands construct locally after resolving cacheDir"
  - "WriteCacheMarkers called only from Save() to keep constructor side-effect free"
  - "RebuildSearchIndex validation tests updated to test storageKey traversal instead of cacheDir traversal"

patterns-established:
  - "Constructor-injection for IndexStore: all callers pass cacheDir at construction, never per-method"
  - "Cache markers: CACHEDIR.TAG + .gitignore written on first Save(), never overwritten"

requirements-completed: [STOR-03, STOR-04, STOR-05, STOR-06, TEST-02]

duration: 12min
completed: 2026-03-10
---

# Phase 2 Plan 1: Constructor-Injected IndexStore with Cache Markers Summary

**IndexStore refactored to constructor(cacheDir) with CACHEDIR.TAG and .gitignore markers written on Save(), all 60+ call sites updated**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-10T00:48:34Z
- **Completed:** 2026-03-10T01:00:34Z
- **Tasks:** 2
- **Files modified:** 21

## Accomplishments
- IndexStore accepts cacheDir in constructor, stores as readonly field, no method accepts cacheDir
- Save() creates directory + writes CACHEDIR.TAG (standard signature) and .gitignore ("*") if not already present
- Constructor does not create any directory on disk (lazy creation on Save)
- IndexStore removed from DI container; commands construct locally
- ListCommand and PurgeCommand no longer depend on IndexStore at all
- 4 new cache marker tests added; all 269 tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Refactor IndexStore constructor and update all production callers** - `5c3765e` (feat)
2. **Task 2: Add cache marker tests and update all test files for constructor injection** - `40820a6` (test)

## Files Created/Modified
- `src/TokenSqueeze/Storage/IndexStore.cs` - Constructor(cacheDir), WriteCacheMarkers(), CacheDir property
- `src/TokenSqueeze/Storage/QueryReindexer.cs` - Removed cacheDir parameter from EnsureFresh
- `src/TokenSqueeze/Program.cs` - Removed IndexStore from DI registration
- `src/TokenSqueeze/Commands/*.cs` - Construct IndexStore locally after resolving cacheDir
- `src/TokenSqueeze/Indexing/ProjectIndexer.cs` - Index() drops cacheDir parameter
- `src/TokenSqueeze/Indexing/IncrementalReindexer.cs` - ReindexFiles() drops cacheDir parameter
- `src/TokenSqueeze.Tests/**/*.cs` - All tests updated for new constructor and method signatures

## Decisions Made
- IndexStore removed from DI -- commands construct locally after resolving cacheDir (simplifies the API, prevents misuse of mixing cache directories)
- WriteCacheMarkers called only from Save() to keep constructor completely side-effect free (STOR-04)
- RebuildSearchIndex validation tests refactored to test storageKey traversal rather than cacheDir traversal (since cacheDir is now in constructor)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] RebuildSearchIndex_AcceptsValidManifest test directory creation**
- **Found during:** Task 2
- **Issue:** RebuildSearchIndex writes to disk but the constructor no longer creates the directory, causing DirectoryNotFoundException
- **Fix:** Added explicit Directory.CreateDirectory in the test since RebuildSearchIndex doesn't go through Save()
- **Files modified:** src/TokenSqueeze.Tests/Storage/IndexStoreValidationTests.cs
- **Committed in:** 40820a6

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Minor test fix necessary because constructor no longer creates directory. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- IndexStore API fully simplified with constructor injection
- Cache markers ensure backup tools skip the cache and git ignores it
- Ready for Phase 3 command layer changes
