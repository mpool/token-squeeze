---
phase: 03-cli-commands
plan: 01
subsystem: cli
tags: [spectre-console, commands, cwd-resolution]

# Dependency graph
requires:
  - phase: 02-storage-layer
    provides: IndexStore with constructor-injected cacheDir
provides:
  - Simplified query commands (outline, extract, find) resolving cache from cwd
  - Cache-existence guards with standard error message
  - Removed list/purge dead commands
affects: [04-plugin-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [cwd-based cache resolution, cache-existence guard pattern]

key-files:
  created: []
  modified:
    - src/TokenSqueeze/Commands/OutlineCommand.cs
    - src/TokenSqueeze/Commands/ExtractCommand.cs
    - src/TokenSqueeze/Commands/FindCommand.cs
    - src/TokenSqueeze/Program.cs
    - src/TokenSqueeze.Tests/Helpers/CliTestHarness.cs
    - src/TokenSqueeze.Tests/Commands/CliIntegrationTests.cs
    - src/TokenSqueeze.Tests/Commands/ExtractCommandTests.cs
    - src/TokenSqueeze.Tests/Commands/FindCommandTests.cs
    - src/TokenSqueeze.Tests/Commands/QueryErrorTests.cs
    - src/TokenSqueeze.Tests/Commands/IndexCommandTests.cs

key-decisions:
  - "Added RunInDir to CliTestHarness for cwd-based query testing"

patterns-established:
  - "cwd cache resolution: var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), \".cache\")"
  - "cache guard: if (!Directory.Exists(cacheDir)) return error"

requirements-completed: [CLI-01, CLI-02, CLI-03, CLI-04, CLI-05, CLI-06, CLI-07, CLI-08]

# Metrics
duration: 6min
completed: 2026-03-10
---

# Phase 3 Plan 1: CLI Command Simplification Summary

**Dropped name argument from query commands, added cwd-based .cache resolution with existence guards, deleted list/purge commands**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-10T01:18:00Z
- **Completed:** 2026-03-10T01:24:00Z
- **Tasks:** 2
- **Files modified:** 15 (including deleted files)

## Accomplishments
- All three query commands (outline, extract, find) resolve cache from cwd with no name argument
- Standard cache-existence guard returning "No index found. Run /token-squeeze:index"
- ListCommand.cs and PurgeCommand.cs deleted, registrations removed from Program.cs
- All 265 tests pass with updated test harness

## Task Commits

Each task was committed atomically:

1. **Task 1: Refactor query commands to drop name argument and add cache guard** - `08a86a4` (feat)
2. **Task 2: Delete list/purge commands and clean up Program.cs** - `8c88705` (feat)

## Files Created/Modified
- `src/TokenSqueeze/Commands/OutlineCommand.cs` - Removed Name arg, added cwd cache resolution + guard
- `src/TokenSqueeze/Commands/ExtractCommand.cs` - Removed Name arg, added cwd cache resolution + guard
- `src/TokenSqueeze/Commands/FindCommand.cs` - Removed Name arg, added cwd cache resolution + guard
- `src/TokenSqueeze/Commands/ListCommand.cs` - Deleted
- `src/TokenSqueeze/Commands/PurgeCommand.cs` - Deleted
- `src/TokenSqueeze/Program.cs` - Removed list/purge registrations
- `src/TokenSqueeze.Tests/Helpers/CliTestHarness.cs` - Added RunInDir, removed list/purge registrations
- `src/TokenSqueeze.Tests/Commands/CliIntegrationTests.cs` - Updated for cwd-based query commands
- `src/TokenSqueeze.Tests/Commands/ExtractCommandTests.cs` - Updated for cwd-based query commands
- `src/TokenSqueeze.Tests/Commands/FindCommandTests.cs` - Updated for cwd-based query commands
- `src/TokenSqueeze.Tests/Commands/QueryErrorTests.cs` - Updated for cwd-based query commands
- `src/TokenSqueeze.Tests/Commands/IndexCommandTests.cs` - Updated outline call to use RunInDir
- `src/TokenSqueeze.Tests/Commands/ListCommandProjectionTests.cs` - Deleted
- `src/TokenSqueeze.Tests/Commands/PurgeCommandTests.cs` - Deleted

## Decisions Made
- Added `RunInDir` method to CliTestHarness to support testing commands that resolve cache from cwd (Directory.GetCurrentDirectory())

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated test harness and tests for cwd-based resolution**
- **Found during:** Task 2 (Delete list/purge and clean up)
- **Issue:** Test harness registered ListCommand/PurgeCommand and all query command tests passed sourceDir as the name argument. After removing list/purge commands and dropping the name argument, 6 tests failed with parser errors.
- **Fix:** Added RunInDir method to CliTestHarness, updated all query command test calls to use RunInDir, deleted ListCommandProjectionTests and PurgeCommandTests, replaced list/purge integration tests with no-index error tests.
- **Files modified:** CliTestHarness.cs, CliIntegrationTests.cs, ExtractCommandTests.cs, FindCommandTests.cs, QueryErrorTests.cs, IndexCommandTests.cs, ListCommandProjectionTests.cs (deleted), PurgeCommandTests.cs (deleted)
- **Verification:** All 265 tests pass
- **Committed in:** 8c88705 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary test infrastructure update for the new cwd-based resolution model. No scope creep.

## Issues Encountered
None beyond the auto-fixed test updates.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- CLI surface area complete for local-storage model
- Commands: index, outline, extract, find (+ hidden parse-test)
- Ready for Phase 4 plugin integration

---
*Phase: 03-cli-commands*
*Completed: 2026-03-10*

## Self-Check: PASSED
