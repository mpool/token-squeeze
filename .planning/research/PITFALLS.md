# Domain Pitfalls

**Domain:** CLI tool storage migration (global to local project storage)
**Project:** TokenSqueeze - Local Storage Simplification
**Researched:** 2026-03-09

## Critical Pitfalls

Mistakes that cause rewrites, broken integrations, or data loss.

### Pitfall 1: MCP Server CWD Is Not the Project Root

**What goes wrong:** The MCP server (`mcp-server.js`) spawns CLI invocations via `execFileSync(BINARY, args)` without setting `cwd`. The child process inherits the MCP server's working directory, which is whatever directory the MCP host (Claude Code) started the server in -- typically the plugin install directory (`~/.claude/plugins/token-squeeze/`), NOT the user's project. When the CLI switches to "operate on `.cache/` relative to cwd," every MCP tool call looks for `.cache/` in the wrong directory and returns "index not found."

**Why it happens:** Today the CLI takes an explicit project name and resolves storage via `StoragePaths.GetProjectDir(name)` against a fixed global root. CWD is irrelevant. After migration, CWD becomes the entire addressing mechanism. The MCP server has never needed to care about CWD, so this is an invisible dependency.

**Consequences:** All MCP tools silently fail post-migration. The CLI works fine when invoked directly (user is already in project dir), but the MCP integration -- which is the primary usage path -- is completely broken.

**Prevention:**
1. The MCP server must pass `cwd: projectRoot` to `execFileSync`. This means the MCP server needs to know the project root.
2. Claude Code sets `CLAUDE_PROJECT_DIR` (or equivalent env var) when launching MCP servers. Verify this exists and use it. If it does not exist, the MCP server must receive the project root via tool arguments or a separate mechanism.
3. Alternative: add an explicit `--root <path>` CLI flag that overrides CWD-based resolution. The MCP server passes it on every call. This is more robust than depending on CWD.

**Detection:** First integration test of MCP tools after migration. If you only test the CLI directly, you will miss this entirely.

**Phase:** Must be solved in the very first implementation phase, before any MCP tool changes.

---

### Pitfall 2: IndexStore Constructor Creates Directories Eagerly

**What goes wrong:** `IndexStore` is a DI singleton. Its constructor calls `StoragePaths.EnsureRootExists()`, which calls `Directory.CreateDirectory(RootDir)`. After migration, if `RootDir` becomes `.cache/` relative to CWD, the constructor creates `.cache/` in whatever directory the process started in -- even for query commands that should fail gracefully when no index exists.

**Why it happens:** The current design assumes the global storage root always exists (creating `~/.token-squeeze/projects/` is harmless). With local storage, creating `.cache/` in the project root before the user explicitly indexes is wrong behavior -- it creates phantom directories and confuses "is there an index?" checks.

**Consequences:**
- Empty `.cache/` directories appear in projects that have never been indexed, polluting the workspace.
- "No index found" error logic breaks because the directory exists but is empty.
- If CWD is wrong (see Pitfall 1), orphan `.cache/` directories get created in random locations.

**Prevention:**
1. Remove eager directory creation from the `IndexStore` constructor. Create `.cache/` only in the `Save` path (during indexing).
2. Query methods (`LoadManifest`, `LoadAllSymbols`, etc.) should check for directory/file existence and return null without creating anything.
3. Add a method like `IndexExists()` that checks for `manifest.json` existence without side effects.

**Detection:** Run any query command in an un-indexed project and check whether `.cache/` was created.

**Phase:** Must be addressed in the storage layer refactor phase, before MCP changes.

---

### Pitfall 3: Security Model Inversion -- Index Data Inside the Validated Root

**What goes wrong:** Currently, `PathValidator.ValidateWithinRoot(path, StoragePaths.RootDir)` ensures all storage operations stay inside `~/.token-squeeze/projects/`. The storage root is a dedicated, non-project directory. After migration, the storage root IS the project root (or a subdirectory of it). The path validator's root becomes the project directory itself, which means "validate within root" becomes nearly meaningless -- almost any path within the project passes validation.

More concretely: `IndexStore` currently validates that storage paths don't escape `~/.token-squeeze/projects/<name>/`. If the new root is `<project>/.cache/`, the validator must ensure operations stay within `.cache/`, not within the project root. Getting this wrong means a crafted file path or storage key could write/read files outside `.cache/` but still within the project, which is a different threat model.

**Why it happens:** The current security boundary is "storage root = safe zone, everything outside = danger." The new model has "project root = source files, `.cache/` = storage zone" -- two zones within the same tree. The existing validator wasn't designed for this.

**Consequences:** Storage key manipulation could read/overwrite source files if validation root is set to the project directory instead of `.cache/`.

**Prevention:**
1. Set the validation root to `.cache/` specifically, not the project root. Every `PathValidator.ValidateWithinRoot` call in `IndexStore` should validate against the `.cache/` directory, not the project directory.
2. Add a test that constructs a malicious storage key (e.g., `../../src/Program.cs`) and verifies it's rejected.

**Detection:** Review all `ValidateWithinRoot` calls after migration. If any use the project root as the validation boundary instead of `.cache/`, flag it.

**Phase:** Storage layer refactor phase. Must be in the same changeset as the `StoragePaths` rewrite.

---

### Pitfall 4: Breaking the MCP Tool Interface Without Coordinated Plugin Update

**What goes wrong:** The MCP server hardcodes tool schemas with `project` as a required parameter. The CLI commands take project name as a positional argument. If the CLI drops the project name argument but the MCP server still passes it (or vice versa), every tool call fails with argument parsing errors.

**Why it happens:** The MCP server (`mcp-server.js`) and the CLI binary (`token-squeeze.exe`) are separate artifacts with separate update paths. The MCP server constructs CLI argument arrays directly (e.g., `["outline", args.project, args.file]`). If the CLI changes its argument structure but the MCP server isn't updated atomically, the integration breaks.

**Consequences:** Complete MCP integration failure. Users who update the binary but not the plugin (or vice versa) get cryptic errors.

**Prevention:**
1. Ship the MCP server changes and CLI changes in the same release. The `build.sh` script already bundles both -- use it.
2. Remove `project` from all MCP tool `inputSchema` definitions.
3. Remove `list_projects` tool entirely.
4. Update all `handleToolCall` argument construction to drop the project name positional arg.
5. Consider adding a version handshake or at minimum a clear error message when argument counts don't match.

**Detection:** Run MCP integration test (or manual test via Claude Code) after any CLI argument changes.

**Phase:** MCP/plugin update phase. Must be atomic with CLI command signature changes.

---

### Pitfall 5: Indexing Creates `.cache/` Containing the Index of Itself

**What goes wrong:** If `.cache/` lives inside the project root, and the directory walker indexes the project root, it will walk into `.cache/` and index the JSON fragment files as source code. On subsequent re-indexes, the `.cache/` directory grows (more fragments from indexing the fragments), creating a feedback loop. Even without the feedback loop, indexing `.cache/*.json` wastes time and pollutes the symbol index with noise.

**Why it happens:** The `DirectoryWalker` currently respects `.gitignore` patterns. If `.cache/` isn't in `.gitignore`, it gets walked. Even if the skill offers to add `.cache/` to `.gitignore` (as planned), the first index runs BEFORE that `.gitignore` update happens.

**Consequences:** Bloated index with hundreds of false symbols from JSON files. If JSON files match no language spec, they're just skipped (no `.json` language registered), but the walker still reads every file's bytes to check. For large indexes, this adds measurable overhead.

**Prevention:**
1. Hardcode `.cache/` as an excluded directory in `DirectoryWalker`, alongside other always-excluded directories (like `.git/`). Don't rely solely on `.gitignore`.
2. This exclusion should match the exact directory name used for storage, not a pattern.
3. Add the exclusion BEFORE the storage migration, so it's already in place when `.cache/` first appears.

**Detection:** Index a project, then re-index it. Check if any files from `.cache/` appear in the manifest.

**Phase:** Directory walker update, before or simultaneous with storage migration.

## Moderate Pitfalls

### Pitfall 6: Atomic Write Temp Files Visible to Git and Other Tools

**What goes wrong:** `AtomicWrite` creates temp files like `manifest.json.tmp-<guid>` in the same directory as the target. With global storage, these are in `~/.token-squeeze/` where no one notices. With local `.cache/`, they're in the project tree. If the process crashes mid-write, orphaned `.tmp-*` files remain in `.cache/`. If `.cache/` isn't gitignored, these show up in `git status`. Even if gitignored, file watchers (IDE, build tools) may react to them.

**Prevention:**
1. Ensure `.cache/` is added to `.gitignore` early (the skill already plans this).
2. The existing atomic write cleanup logic handles crashes -- no change needed there.
3. Consider whether `.cache/` should be in the project's `.gitignore` by default or added to a global gitignore. Project-level is more reliable.

**Phase:** Post-migration cleanup / skill update phase.

---

### Pitfall 7: ExtractCommand Reads Source Files Relative to SourcePath

**What goes wrong:** `ExtractCommand` reads original source bytes from disk using `ByteOffset`/`ByteLength` from the symbol record. The file path resolution depends on `SourcePath` stored in the manifest. If `SourcePath` was an absolute path (which it is today -- see `ProjectIndexer.Index()` using `Path.GetFullPath(directoryPath)`), and the project directory moves or is accessed from a different mount point, extract commands fail with "file not found."

This is the same behavior as today, but the failure mode is more visible with local storage: users expect local `.cache/` to "just work" even after moving the project, because the cache is co-located with the source. But the manifest still contains an absolute `SourcePath`.

**Prevention:**
1. After migration, `SourcePath` in the manifest should either be removed (it's always CWD) or stored as a relative marker (e.g., `.`).
2. `ExtractCommand` should resolve source file paths relative to CWD, not from a stored absolute path.
3. Consider whether the manifest needs `SourcePath` at all in the local model -- it's always the parent of `.cache/`.

**Phase:** Model simplification phase (dropping `ProjectName` is already planned; `SourcePath` changes should be evaluated at the same time).

---

### Pitfall 8: Test Suite Uses `StoragePaths.TestRootOverride` Extensively

**What goes wrong:** The current test infrastructure uses `StoragePaths.TestRootOverride` to redirect all storage to a temp directory. After migration, if storage is CWD-relative, this static override pattern may no longer work correctly -- tests need to set CWD (which affects the whole process) or the storage resolution logic needs a different injection point.

**Prevention:**
1. Replace `TestRootOverride` with a proper constructor-injected storage root path. `IndexStore` receives its base path via DI, defaulting to `.cache/` relative to CWD in production.
2. Tests inject a temp directory path. No static mutable state.
3. This is also an opportunity to clean up the `static` dependency in `StoragePaths` -- making it instance-based enables parallel test execution.

**Detection:** If tests start interfering with each other or creating `.cache/` in the test runner's working directory.

**Phase:** Storage layer refactor phase. Prerequisite for reliable test coverage of the new storage model.

---

### Pitfall 9: LegacyMigration and QueryReindexer Reference Old Storage Paths

**What goes wrong:** `LegacyMigration.TryMigrateIfNeeded()` and `QueryReindexer.EnsureFresh()` both take `projectName` as their first argument and use `StoragePaths` to locate project data. After migration, these need to work without project names. If legacy migration code is kept but not updated, it tries to access `~/.token-squeeze/projects/<name>/` which no longer exists, causing null reference exceptions or misleading error messages.

**Prevention:**
1. Delete `LegacyMigration` entirely. The PROJECT.md already states "no migration from old storage."
2. Update `QueryReindexer.EnsureFresh()` to accept a cache directory path instead of a project name.
3. Remove the legacy format detection path from `IndexStore.Load()`.

**Phase:** Storage layer refactor phase. Clean removal, not incremental update.

---

### Pitfall 10: Multiple Concurrent Claude Sessions Index the Same Project

**What goes wrong:** With global storage, concurrent sessions indexing the same project wrote to the same directory, but the atomic write pattern handled this. With local `.cache/`, the same concurrency applies -- two Claude sessions in the same project can trigger simultaneous indexing. The atomic write pattern still handles this at the file level, but `Parallel.ForEach` in `ProjectIndexer` combined with two concurrent `ProjectIndexer.Index()` calls could produce corrupt intermediate states where one process's fragments are partially overwritten by another's.

**Prevention:**
1. The existing atomic write pattern provides per-file safety. The manifest-last write order provides crash consistency.
2. Consider adding a `.cache/lock` file using `FileStream` with `FileShare.None` during indexing. Release it after manifest write.
3. Query commands should NOT acquire a lock -- only index should lock.

**Phase:** Nice-to-have hardening after core migration. Not blocking for initial release since the current code has the same theoretical exposure.

## Minor Pitfalls

### Pitfall 11: `.cache/` Name Collision with Other Tools

**What goes wrong:** Some projects already have a `.cache/` directory used by other tools (e.g., Prettier, ESLint, pytest). TokenSqueeze would either collide with these or its files would be mixed in with unrelated cache data.

**Prevention:**
1. Use `.cache/token-squeeze/` as the storage path instead of bare `.cache/`. This is the conventional pattern -- a tool-specific subdirectory within `.cache/`.
2. Alternatively, verify that no common tooling uses bare `.cache/` as its root (most use `.cache/<toolname>/`).
3. The PROJECT.md lists `.cache/` as pending decision -- this is the right time to settle on `.cache/token-squeeze/`.

**Phase:** First decision, before any implementation. Changing this after release is another breaking change.

---

### Pitfall 12: `UpdateCatalog()` Has No Purpose in Local Storage

**What goes wrong:** `IndexStore.Save()` calls `UpdateCatalog()` which writes `catalog.json` listing all projects. With local storage, there's only one "project" per `.cache/` directory. The catalog concept doesn't apply but the code still runs, potentially creating a useless `catalog.json` in `.cache/` or worse, trying to write to a global path that no longer exists.

**Prevention:**
1. Remove `UpdateCatalog()` and `LoadCatalogJson()` entirely.
2. Remove `CatalogPath` from `StoragePaths`.
3. These are dead code after `list` and `purge` commands are removed.

**Phase:** Storage layer refactor phase. Straightforward deletion.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| StoragePaths rewrite | Security validation root set wrong (Pitfall 3) | Validate against `.cache/` dir, not project root |
| StoragePaths rewrite | Eager directory creation (Pitfall 2) | Remove from constructor, create only on Save |
| StoragePaths rewrite | Test override pattern breaks (Pitfall 8) | Move to DI-injected path, not static override |
| CLI command signature changes | MCP server arg construction mismatch (Pitfall 4) | Atomic release of CLI + MCP server |
| MCP server update | CWD not set for child process (Pitfall 1) | Pass `cwd` or `--root` flag; test via MCP, not just CLI |
| DirectoryWalker update | Index indexes itself (Pitfall 5) | Hardcode `.cache/` exclusion before migration |
| Model simplification | SourcePath absolute path breaks after move (Pitfall 7) | Store relative or derive from CWD |
| Legacy cleanup | Dead migration code references old paths (Pitfall 9) | Delete entirely, don't update |
| Naming decision | `.cache/` collides with other tools (Pitfall 11) | Use `.cache/token-squeeze/` subdirectory |

## Sources

- Direct analysis of `StoragePaths.cs`, `IndexStore.cs`, `ProjectIndexer.cs`, `mcp-server.js`, `PathValidator.cs`, `FindCommand.cs`, `IndexCommand.cs`
- PROJECT.md requirements and constraints
- ARCHITECTURE.md current data flow documentation
