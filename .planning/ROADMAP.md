# Roadmap: Die Hook Die — Local Storage Simplification

## Overview

This milestone strips TokenSqueeze down to its essential job: index a directory, answer symbol queries. The work follows bottom-up dependency order — reshape models and paths first, then rebuild the storage layer on top, then rewire CLI commands, and finally update the external interface (MCP server, plugin, docs, tests). Four phases, each delivering a compilable, testable state.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Foundation** - Reshape models, paths, and security boundaries for local storage
- [x] **Phase 2: Storage Layer** - Rewrite IndexStore with constructor injection and local cache semantics
- [ ] **Phase 3: CLI Commands** - Simplify all commands, delete list/purge, add existence guards
- [x] **Phase 4: Integration** - Update MCP server, plugin, tests, and documentation (completed 2026-03-10)

## Phase Details

### Phase 1: Foundation
**Goal**: All data models, path resolution, and security boundaries reflect the new local-storage-only world
**Depends on**: Nothing (first phase)
**Requirements**: MODL-01, MODL-02, MODL-03, STOR-01, STOR-02, SEC-01, SEC-02
**Success Criteria** (what must be TRUE):
  1. `CodeIndex` and `Manifest`/`ManifestHeader` have no `ProjectName` property — serialization round-trips without it
  2. Storage path resolution produces `<cwd>/.cache/` with no project name segment in any path
  3. `DirectoryWalker` skips the `.cache/` directory when walking a project tree
  4. `PathValidator.ValidateWithinRoot` rejects paths that escape the cache directory boundary
**Plans**: 2 plans

Plans:
- [x] 01-01-PLAN.md — Strip ProjectName from models, rewrite StoragePaths, add .cache to DirectoryWalker
- [x] 01-02-PLAN.md — Thread cacheDir through IndexStore/ProjectIndexer/IncrementalReindexer, fix tests

### Phase 2: Storage Layer
**Goal**: IndexStore operates entirely via constructor-injected cache directory with no project-name concept
**Depends on**: Phase 1
**Requirements**: STOR-03, STOR-04, STOR-05, STOR-06, TEST-02
**Success Criteria** (what must be TRUE):
  1. `IndexStore` constructor accepts a cache directory path and never references a project name
  2. Cache directory is created only during `Save()` — constructing an IndexStore for queries does not create directories
  3. A newly created cache directory contains both `CACHEDIR.TAG` and a self-ignoring `.gitignore`
  4. Tests use constructor-injected temp directories instead of `TestRootOverride` or static state
**Plans**: 1 plan

Plans:
- [x] 02-01-PLAN.md — Refactor IndexStore to constructor injection, add cache markers, update all callers and tests

### Phase 3: CLI Commands
**Goal**: All CLI commands work against local `.cache/` with no project name argument, and removed commands are gone
**Depends on**: Phase 2
**Requirements**: CLI-01, CLI-02, CLI-03, CLI-04, CLI-05, CLI-06, CLI-07, CLI-08
**Success Criteria** (what must be TRUE):
  1. `index <path>` writes output to `<target>/.cache/` with no project naming prompt or argument
  2. `outline`, `extract`, and `find` work with zero arguments beyond the query — no `<name>` parameter
  3. Running any query command without a `.cache/` directory returns a JSON error: "No index found. Run /token-squeeze:index"
  4. `list` and `purge` commands are gone — invoking them produces an unknown-command error
  5. `Program.cs` registers only: index, outline, extract, find
**Plans**: 1 plan

Plans:
- [ ] 03-01-PLAN.md — Refactor query commands (drop name arg, add cache guard), delete list/purge commands

### Phase 4: Integration
**Goal**: MCP server, plugin skills/hooks, tests, and documentation all reflect the simplified local-storage model
**Depends on**: Phase 3
**Requirements**: MCP-01, MCP-02, MCP-03, MCP-04, MCP-05, MCP-06, PLUG-01, PLUG-02, PLUG-03, PLUG-04, PLUG-05, PLUG-06, DOC-01, DOC-02, TEST-01
**Success Criteria** (what must be TRUE):
  1. MCP tools `read_file_outline`, `read_symbol_source`, and `search_symbols` accept no `project` parameter — requests work against cwd
  2. `list_projects` MCP tool is gone — calling it returns a method-not-found error
  3. MCP server returns a clear error when `.cache/manifest.json` is missing, telling the user to run index
  4. SessionStart hook and auto-index scripts are deleted — no hook files exist in `plugin/hooks/` or `plugin/scripts/`
  5. All tests pass against the new storage model and CLAUDE.md documents the current command signatures
**Plans**: 3 plans

Plans:
- [ ] 04-01-PLAN.md — Rewire MCP server (drop project param, delete list_projects, add manifest guard)
- [ ] 04-02-PLAN.md — Plugin cleanup (delete hooks/scripts/purge, update skills and agents)
- [ ] 04-03-PLAN.md — Update CLAUDE.md and README.md, verify all tests pass

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 2/2 | Complete    | 2026-03-10 |
| 2. Storage Layer | 1/1 | Complete | 2026-03-10 |
| 3. CLI Commands | 0/1 | Not started | - |
| 4. Integration | 3/3 | Complete    | 2026-03-10 |
