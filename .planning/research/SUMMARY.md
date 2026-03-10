# Project Research Summary

**Project:** TokenSqueeze - Local Storage Simplification
**Domain:** CLI tool storage migration (global to local project storage)
**Researched:** 2026-03-09
**Confidence:** HIGH

## Executive Summary

TokenSqueeze is migrating from a global named-project storage model (`~/.token-squeeze/projects/<name>/`) to a local CWD-relative model where the index lives inside the project directory itself. This is a pure refactor with zero new dependencies -- the entire change uses existing .NET 9 `System.IO` APIs. The migration simplifies every API surface: CLI commands drop the `<name>` argument, MCP tools drop the `project` parameter, and two commands (`list`, `purge`) are deleted entirely along with the catalog infrastructure. The recommended approach follows bottom-up dependency order: Models -> StoragePaths -> IndexStore -> Indexing -> Commands -> MCP/Plugin.

The highest-risk area is the MCP server integration. The CLI works fine when invoked directly (the user's terminal is already in the project directory), but the MCP server spawns the CLI as a child process whose CWD may not be the project root. This is the single most important thing to get right, and it must be verified through MCP-level integration testing, not just CLI tests. A secondary risk is the security validation boundary: the path validator must be scoped to the cache directory specifically, not the project root, to prevent storage key manipulation from touching source files.

There is one unresolved naming decision: STACK.md recommends `.token-squeeze/` (zero collision risk, self-documenting), while FEATURES.md and ARCHITECTURE.md use `.cache/`. PITFALLS.md flags `.cache/` collision risk and suggests `.cache/token-squeeze/` as a compromise. **Recommendation: use `.token-squeeze/`** -- it eliminates collision concerns, is self-documenting, and avoids the extra nesting of `.cache/token-squeeze/`. This must be settled before implementation begins since it propagates through every path in the codebase.

## Key Findings

### Recommended Stack

No new dependencies. The migration uses `Directory.GetCurrentDirectory()` and `Path.Combine()` from `System.IO`, which are already in use. JSON serialization, atomic writes, security validation, and the build/publish pipeline are all unchanged.

**Core technologies (unchanged):**
- **.NET 9 / System.IO** -- CWD resolution via `Directory.GetCurrentDirectory()`, directory creation, path joining
- **System.Text.Json** -- existing `JsonDefaults.cs` configuration, no changes needed
- **Spectre.Console.Cli** -- command registration changes (remove two commands, simplify argument shapes)

**Key technical decision:** Use `Directory.GetCurrentDirectory()` for storage root resolution. This matches how `dotnet`, `cargo`, `git`, and every other project-scoped CLI works. Do NOT use `AppDomain.CurrentDomain.BaseDirectory` (returns executable location, not user CWD).

### Expected Features

**Must have (table stakes):**
- CWD-relative storage in a dot-directory at project root
- No project name argument on any command -- directory identity IS project identity
- Index-exists check with clear "run index first" error on all query commands
- Atomic/crash-safe writes (already implemented, keep as-is)
- Incremental reindex via content hashing (already implemented, keep as-is)
- Query-time staleness detection (already implemented, keep as-is)
- Gitignore-friendly cache directory

**Should have (low cost, high polish):**
- `CACHEDIR.TAG` file in cache directory per Cache Directory Tagging Specification
- Self-ignoring `.gitignore` inside cache directory (contains `*`)
- Offer root `.gitignore` update after first index via skill

**Defer:**
- Cache size reporting
- Elapsed time in index output
- Lock file for concurrent indexing (same theoretical exposure as current code)

**Anti-features (explicitly remove):**
- `list` command, `purge` command, global catalog, named project aliases, legacy migration code, background/watch mode, global configuration, token counting

### Architecture Approach

The refactor follows a strict bottom-up dependency order across 6 phases. The key structural change is that `IndexStore` receives its cache directory via constructor injection (computed once in `Program.cs`), replacing the current static `StoragePaths` class with its `projectName`-threaded methods. StoragePaths collapses into IndexStore as 4 trivial private path-join methods plus one `PathToStorageKey()` utility. Commands never compute the cache path themselves -- they receive an `IndexStore` that already knows where to look.

**Major components and their changes:**
1. **Models** -- drop `ProjectName` from `CodeIndex` and `Manifest`, delete `ManifestHeader`
2. **IndexStore** -- constructor injection of `cacheDir`, drop `projectName` from all methods, delete catalog/legacy/list/purge logic, inline StoragePaths
3. **Commands** -- drop `<name>` argument, add existence guard, delete `ListCommand` and `PurgeCommand`
4. **MCP server** -- drop `project` parameter from all tools, remove `list_projects` tool, pass `cwd` to CLI subprocess
5. **Plugin/Skills** -- remove project references, delete hooks/ and scripts/ directories

### Critical Pitfalls

1. **MCP server CWD mismatch** -- The MCP server spawns CLI without setting `cwd` to project root. After migration, every MCP tool call looks for the cache in the wrong directory. Prevention: pass `cwd: projectRoot` to `execFileSync`, verify Claude Code sets project dir for MCP servers. Test via MCP, not just CLI.

2. **Eager directory creation in IndexStore constructor** -- Current constructor calls `EnsureRootExists()`. After migration, this creates phantom cache directories in un-indexed projects. Prevention: create cache directory only during `Save()`, never during construction or queries.

3. **Security validation boundary wrong** -- Path validator must scope to the cache directory, not the project root. Otherwise, crafted storage keys could read/overwrite source files. Prevention: all `ValidateWithinRoot` calls use the cache directory as boundary.

4. **MCP/CLI interface desync** -- CLI drops `<name>` argument but MCP server still passes it (or vice versa). Prevention: ship MCP and CLI changes in the same release. The existing `build.sh` already bundles both.

5. **Index indexes itself** -- DirectoryWalker walks into the cache directory and indexes JSON fragments. Prevention: hardcode cache directory as an excluded path in DirectoryWalker, alongside `.git/`. Do this before or simultaneously with the storage migration.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Foundation -- Models + StoragePaths + DirectoryWalker

**Rationale:** Everything depends on Models and StoragePaths. Changing them first means every subsequent change compiles against the new shape. The compiler tells you exactly what to fix next via cascading errors. DirectoryWalker exclusion must land before or with storage migration (Pitfall 5).
**Delivers:** New model shapes without `ProjectName`, cache directory resolution logic, walker exclusion for cache directory.
**Addresses:** CWD-relative storage (table stakes), cache directory naming decision.
**Avoids:** Index-indexes-itself (Pitfall 5), security validation boundary (Pitfall 3).

### Phase 2: Storage Layer -- IndexStore Rewrite

**Rationale:** Commands and Indexing both depend on IndexStore. Fix it before fixing its consumers.
**Delivers:** Constructor-injected IndexStore, no project name in any method signature, StoragePaths collapsed into private methods, `TestRootOverride` replaced with DI.
**Addresses:** Atomic writes (keep), incremental reindex (keep), orphan cleanup (keep).
**Avoids:** Eager directory creation (Pitfall 2), test infrastructure breakage (Pitfall 8), dead catalog code (Pitfall 12).

### Phase 3: Indexing + Commands

**Rationale:** Both depend on IndexStore (fixed in Phase 2) and Models (fixed in Phase 1). Can be done together since they're the same compilation unit.
**Delivers:** Simplified CLI interface -- no `<name>` argument, no `list`/`purge` commands, existence guard on all query commands, `CACHEDIR.TAG` and self-ignoring `.gitignore` on index creation.
**Addresses:** No project name argument (table stakes), index-exists check (table stakes), anti-features removal (list, purge, catalog, legacy migration), differentiators (CACHEDIR.TAG, self-ignoring gitignore).
**Avoids:** Legacy migration code referencing old paths (Pitfall 9), SourcePath absolute path issues (Pitfall 7).

### Phase 4: MCP Server + Plugin

**Rationale:** External interface -- change only after CLI is stable. Must be atomic with the CLI changes from Phase 3.
**Delivers:** Updated MCP tools without `project` parameter, `list_projects` removed, CWD propagation to CLI subprocess, updated skills, hooks/scripts deletion.
**Addresses:** MCP tool parameter simplification, skill updates for new workflow.
**Avoids:** MCP CWD mismatch (Pitfall 1), MCP/CLI interface desync (Pitfall 4).

### Phase 5: Tests + Documentation

**Rationale:** After all production code is migrated, update tests to use the new DI pattern and verify the full integration path.
**Delivers:** Updated test fixtures using temp cache directories via constructor injection, MCP integration test coverage, updated CLAUDE.md with new command signatures.
**Addresses:** Test reliability, documentation accuracy.
**Avoids:** Test interference from static mutable state (Pitfall 8).

### Phase Ordering Rationale

- Bottom-up dependency order: Models -> Storage -> Indexing/Commands -> MCP/Plugin -> Tests. Each phase compiles and passes tests before the next begins.
- Phases 3 and 4 must ship together as one release to avoid MCP/CLI desync (Pitfall 4).
- The cache directory naming decision (`.token-squeeze/` vs `.cache/`) must be finalized before Phase 1 since it propagates everywhere.
- DirectoryWalker exclusion in Phase 1 prevents the index-indexes-itself problem from ever manifesting.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 4 (MCP Server):** Need to verify how Claude Code sets CWD for MCP servers. If `CLAUDE_PROJECT_DIR` or equivalent is not available, may need a `--root` CLI flag as fallback. Test empirically.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation):** Straightforward model field removal and path resolution. Compiler-driven.
- **Phase 2 (Storage Layer):** Constructor injection replacing static state. Well-documented DI pattern.
- **Phase 3 (Indexing + Commands):** Argument removal and guard clauses. Mechanical.
- **Phase 5 (Tests):** Standard test refactoring.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Zero new dependencies. All APIs are standard .NET 9 framework classes with Microsoft documentation. |
| Features | HIGH | Feature list derived from direct codebase analysis and established CLI tool conventions. Anti-features clearly justified. |
| Architecture | HIGH | Refactor order derived from actual dependency graph of existing code. Component boundaries well understood from source analysis. |
| Pitfalls | HIGH for pitfalls 1-5, MEDIUM for 6-12 | Critical pitfalls identified from structural analysis of MCP server, storage layer, and security model. Moderate pitfalls are lower risk. |

**Overall confidence:** HIGH

### Gaps to Address

- **MCP server CWD behavior:** Need empirical verification of how Claude Code sets working directory for MCP server processes. Research identified the risk but couldn't confirm the exact mechanism. Test during Phase 4 implementation.
- **Cache directory name:** `.token-squeeze/` vs `.cache/` vs `.cache/token-squeeze/` -- three options with trade-offs documented. Must be decided before implementation. Recommendation is `.token-squeeze/` but this contradicts PROJECT.md's initial `.cache/` preference. Needs explicit sign-off.
- **SourcePath handling:** Whether to store relative path, remove from manifest, or keep absolute. Pitfall 7 flags the issue but the optimal solution depends on how `ExtractCommand` and `StalenessChecker` use it in practice. Evaluate during Phase 3.

## Sources

### Primary (HIGH confidence)
- Direct codebase analysis of all `src/TokenSqueeze/` source files
- `.planning/PROJECT.md` requirements and constraints
- [Microsoft: Directory.GetCurrentDirectory()](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.getcurrentdirectory)
- [Microsoft: File path formats on Windows](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats)
- [git-scm: gitignore documentation](https://git-scm.com/docs/gitignore)

### Secondary (MEDIUM confidence)
- [Cache Directory Tagging Specification](https://bford.info/cachedir/) -- CACHEDIR.TAG convention
- [Cargo .gitignore convention](https://github.com/rust-lang/cargo/issues/11548) -- self-ignoring directory pattern
- [ESLint cache location discussion](https://github.com/eslint/eslint/issues/13897) -- `.cache/` collision concerns
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/) -- cache directory standards

### Tertiary (LOW confidence)
- MCP server CWD propagation from Claude Code -- inferred from code structure, not verified empirically

---
*Research completed: 2026-03-09*
*Ready for roadmap: yes*
