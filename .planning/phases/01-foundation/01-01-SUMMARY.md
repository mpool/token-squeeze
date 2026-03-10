---
phase: 01-foundation
plan: 01
subsystem: models
tags: [csharp, records, storage-paths, directory-walker]

# Dependency graph
requires: []
provides:
  - CodeIndex record without ProjectName
  - Manifest record without ProjectName, no ManifestHeader
  - StoragePaths methods taking cacheDir parameter
  - DirectoryWalker skipping .cache directory
affects: [01-foundation plan 02, all commands, IndexStore, ProjectIndexer]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "cacheDir parameter pattern for all path resolution"
    - "No global state in StoragePaths"

key-files:
  created: []
  modified:
    - src/TokenSqueeze/Models/CodeIndex.cs
    - src/TokenSqueeze/Models/Manifest.cs
    - src/TokenSqueeze/Storage/StoragePaths.cs
    - src/TokenSqueeze/Indexing/DirectoryWalker.cs

key-decisions:
  - "Kept StoragePaths as static class (simplest change for Phase 1)"
  - "Deleted ManifestHeader entirely rather than updating it"

patterns-established:
  - "cacheDir parameter: all storage path methods receive cache directory, no global state"

requirements-completed: [MODL-01, MODL-02, MODL-03, STOR-01, STOR-02, SEC-02]

# Metrics
duration: 1min
completed: 2026-03-10
---

# Phase 1 Plan 1: Models & StoragePaths Summary

**Stripped ProjectName from CodeIndex/Manifest, deleted ManifestHeader/ProjectMetadata, rewrote StoragePaths to resolve from cacheDir parameter**

## Performance

- **Duration:** 1 min
- **Started:** 2026-03-10T00:15:15Z
- **Completed:** 2026-03-10T00:16:27Z
- **Tasks:** 2
- **Files modified:** 4 (1 deleted)

## Accomplishments
- Removed ProjectName property from both CodeIndex and Manifest records
- Deleted ManifestHeader record and ProjectMetadata.cs file entirely
- Rewrote StoragePaths to use cacheDir parameter, eliminating all global state
- Added .cache to DirectoryWalker skip list

## Task Commits

Each task was committed atomically:

1. **Task 1: Strip ProjectName from models and delete dead model files** - `7fb8ec5` (feat)
2. **Task 2: Rewrite StoragePaths to take cacheDir parameter and add .cache to DirectoryWalker** - `f04c8c5` (feat)

## Files Created/Modified
- `src/TokenSqueeze/Models/CodeIndex.cs` - Removed ProjectName property
- `src/TokenSqueeze/Models/Manifest.cs` - Removed ProjectName, deleted ManifestHeader record
- `src/TokenSqueeze/Models/ProjectMetadata.cs` - Deleted entirely
- `src/TokenSqueeze/Storage/StoragePaths.cs` - Complete rewrite: 4 methods with cacheDir + PathToStorageKey
- `src/TokenSqueeze/Indexing/DirectoryWalker.cs` - Added .cache to SkippedDirectories

## Decisions Made
- Kept StoragePaths as static class (simplest change for Phase 1; Phase 2 may wrap in instance if needed)
- Deleted ManifestHeader entirely rather than stripping ProjectName -- it only existed for the list command being removed

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Models and StoragePaths are ready for Plan 02 to update callers (IndexStore, ProjectIndexer, commands)
- Build will NOT compile until Plan 02 fixes all references to removed members
- FormatVersion should be bumped to 3 in IndexStore.Save (noted for Plan 02)

---
*Phase: 01-foundation*
*Completed: 2026-03-10*
