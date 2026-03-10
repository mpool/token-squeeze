# Architecture Patterns

**Domain:** CLI tool storage layer restructuring (global named-project -> local .cache/)
**Researched:** 2026-03-09

## Recommended Architecture

The refactor replaces a globally-rooted, name-keyed storage model with a cwd-relative, implicit storage model. The storage location changes from `~/.token-squeeze/projects/<name>/` to `<cwd>/.cache/`. Every API that currently accepts `projectName` instead derives its storage root from the working directory.

### Before vs After

```
BEFORE:                                    AFTER:

  CLI args: index <path> --name foo          CLI args: index [path]
  CLI args: find <name> <query>              CLI args: find <query>
  CLI args: outline <name> <file>            CLI args: outline <file>

  Storage:                                   Storage:
  ~/.token-squeeze/                          <project-root>/
    projects/                                  .cache/
      foo/                                       manifest.json
        manifest.json                            search-index.json
        search-index.json                        files/
        files/                                     src-main.py.json
          src-main.py.json                         ...

  MCP tools:                                 MCP tools:
  search_symbols(project, query)             search_symbols(query)
  read_file_outline(project, file)           read_file_outline(file)
  read_symbol_source(project, ids)           read_symbol_source(ids)
  list_projects()                            [removed]
```

### Component Boundaries

The refactor touches 4 layers. Parser, Security, and Infrastructure are untouched.

| Component | Changes? | What Changes | What Stays |
|-----------|----------|--------------|------------|
| **StoragePaths** | REWRITE | All path resolution. Drop `projectName` param, resolve from cwd. Remove `RootDir`, `CatalogPath`, `EnsureRootExists`. Add `CacheDir(cwd)`. | `PathToStorageKey()` logic unchanged. |
| **IndexStore** | MAJOR | Every public method drops `projectName` param. Constructor takes `cacheDir` instead of creating global root. Remove `ListProjects()`, `Delete()`, `UpdateCatalog()`, `LoadCatalogJson()`, `IsLegacyFormat()`, `GetLegacySourcePath()`, `LoadManifestHeader()`. | `AtomicWrite`, `AtomicWriteRaw`, fragment read/write logic, crash safety. |
| **Models** | MINOR | `CodeIndex`: drop `ProjectName`. `Manifest`: drop `ProjectName`. `ManifestHeader`: delete entirely. | `Symbol`, `IndexedFile`, `ManifestFileEntry`, `FileSymbolData`, `SearchSymbol` unchanged. |
| **Commands** | MODERATE | All query commands drop `<name>` argument, resolve cwd. `IndexCommand` drops `--name`. Remove `ListCommand`, `PurgeCommand`. Add "no index" error guidance. | Core logic (scoring, hierarchy building, byte extraction) unchanged. |
| **Indexing** | MODERATE | `ProjectIndexer`: drop `ResolveProjectName()`, `SanitizeName()`. `Index()` drops name param. `IncrementalReindexer`: all `manifest.ProjectName` references removed from store calls. `StalenessChecker`: unchanged. | Parsing, hashing, incremental logic, parallelism all unchanged. |
| **QueryReindexer** | MINOR | Drop `projectName` param. | Staleness + reindex flow unchanged. |
| **LegacyMigration** | DELETE | Entire file removed. No migration path (clean break per PROJECT.md). | N/A |
| **Program.cs** | MINOR | Remove `ListCommand`, `PurgeCommand` registrations. `IndexStore` constructor wired with cwd-derived cache dir. | DI pattern, LanguageRegistry, disposal unchanged. |
| **MCP server** | MODERATE | Remove `list_projects` tool. Remove `project` param from all tools. Pass cwd to CLI (or CLI reads cwd itself). | JSON-RPC protocol, stdio transport, binary detection unchanged. |
| **Plugin** | MODERATE | Delete hooks/ and scripts/. Update skills to remove project param. Add `.cache/` existence check guidance. | Skill structure, plugin manifest format unchanged. |
| **Parser** | NONE | | Everything. |
| **Security** | NONE | | Everything. PathValidator still validates within cache root. |
| **Infrastructure** | NONE | | JsonOutput, JsonDefaults, TypeRegistrar, TypeResolver. |

### Data Flow Changes

**Index Command (after refactor):**

```
1. IndexCommand.Execute()
   - path = args[0] ?? cwd
   - cacheDir = Path.Combine(path, ".cache")     // NEW: local resolution

2. ProjectIndexer.Index(path)                     // no name param
   - Loads existing manifest from .cache/         // was: ~/.token-squeeze/projects/<name>/
   - DirectoryWalker.Walk() unchanged
   - Parallel parse unchanged

3. IndexStore.Save(index)
   - Writes to .cache/manifest.json               // was: ~/.token-squeeze/projects/<name>/manifest.json
   - Writes to .cache/files/*.json                // was: ~/.token-squeeze/projects/<name>/files/*.json
   - Writes to .cache/search-index.json           // was: ~/.token-squeeze/projects/<name>/search-index.json
   - NO catalog update                            // was: UpdateCatalog()
```

**Query Commands (after refactor):**

```
1. FindCommand.Execute()
   - cacheDir = Path.Combine(cwd, ".cache")       // NEW: implicit from cwd
   - if (!Directory.Exists(cacheDir))              // NEW: helpful error
       return error "No index found. Run: /token-squeeze:index"

2. QueryReindexer.EnsureFresh(store, registry)     // no projectName
   - Loads .cache/manifest.json
   - StalenessChecker unchanged
   - IncrementalReindexer: store calls drop projectName

3. store.LoadAllSymbols()                          // no projectName
   - Reads .cache/search-index.json
```

**MCP Server (after refactor):**

```
1. Tool call arrives: search_symbols({ query: "foo" })
   - No project param needed

2. runCli(["find", args.query])                    // was: ["find", args.project, args.query]
   - CLI resolves cwd automatically
   - MCP server's cwd = project root (set by Claude Code)
```

### Key Design Decision: How IndexStore Gets Its Root

Two options for how `IndexStore` knows where `.cache/` is:

**Option A: Constructor injection (Recommended)**
```csharp
internal sealed class IndexStore
{
    private readonly string _cacheDir;

    public IndexStore(string cacheDir)
    {
        _cacheDir = cacheDir;
    }
}
```

Registered in DI:
```csharp
var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache");
services.AddSingleton(new IndexStore(cacheDir));
```

This is better because it's explicit, testable without `TestRootOverride` hacks, and the cache dir is determined once at startup.

**Option B: IndexStore reads cwd internally** -- Rejected. Ties IndexStore to ambient state, harder to test, cwd could change mid-process in theory.

### Key Design Decision: StoragePaths Becomes Instance or Disappears

`StoragePaths` is currently a static class with `projectName` threaded through every method. After the refactor, every path is relative to a single `cacheDir`. Two options:

**Option A: Collapse into IndexStore (Recommended)**
StoragePaths has 10 methods. After removing project-name variants, catalog, legacy, and metadata paths, only 4 remain:
- `GetManifestPath()` -> `Path.Combine(_cacheDir, "manifest.json")`
- `GetFilesDir()` -> `Path.Combine(_cacheDir, "files")`
- `GetFileFragmentPath(storageKey)` -> `Path.Combine(_cacheDir, "files", storageKey + ".json")`
- `GetSearchIndexPath()` -> `Path.Combine(_cacheDir, "search-index.json")`

These are one-liners. No need for a separate class. `PathToStorageKey()` is the only method with real logic and can be a private static method on IndexStore.

**Option B: Keep StoragePaths as instance class** -- Overkill for 4 trivial path joins.

## Suggested Refactor Order

The dependency graph dictates order. Changes propagate upward: Models -> StoragePaths -> IndexStore -> Indexing -> Commands -> Program.cs -> MCP/Plugin.

### Phase 1: Models + StoragePaths (foundation, no behavioral change yet)

**Why first:** Everything else depends on these. Changing them first means every subsequent change compiles against the new shape.

1. **Models:** Remove `ProjectName` from `CodeIndex` and `Manifest`. Delete `ManifestHeader`. Delete `ProjectMetadata` if it exists only for list/metadata purposes.
2. **StoragePaths:** Rewrite to take `cacheDir` instead of `projectName`. Remove catalog, legacy, metadata paths. Or delete the class entirely and inline into IndexStore.

**Risk:** Compile errors cascade through every file that references `ProjectName` or `StoragePaths.GetProjectDir()`. This is intentional -- the compiler tells you exactly what to fix next.

### Phase 2: IndexStore (core persistence layer)

**Why second:** Commands and Indexing both depend on IndexStore. Fix it before fixing its consumers.

1. Constructor takes `cacheDir` string. Remove `EnsureRootExists()` call (caller ensures `.cache/` exists).
2. Drop `projectName` from every public method signature.
3. Remove `ListProjects()`, `Delete()`, `UpdateCatalog()`, `LoadCatalogJson()`, `IsLegacyFormat()`, `GetLegacySourcePath()`, `LoadManifestHeader()`.
4. Inline remaining StoragePaths methods as private helpers.
5. Remove `TestRootOverride` -- tests pass `cacheDir` via constructor.

### Phase 3: Indexing layer

**Why third:** Depends on IndexStore (fixed in Phase 2) and Models (fixed in Phase 1).

1. **ProjectIndexer:** Remove `ResolveProjectName()` and `SanitizeName()`. `Index()` takes only `directoryPath`. Build `CodeIndex` without `ProjectName`.
2. **IncrementalReindexer:** Remove `manifest.ProjectName` from all `_store.*` calls.
3. **Delete LegacyMigration.cs** entirely.
4. **QueryReindexer:** Drop `projectName` param from `EnsureFresh()`.

### Phase 4: Commands + Program.cs

**Why fourth:** Depends on everything below.

1. **Delete** `ListCommand.cs` and `PurgeCommand.cs`.
2. **IndexCommand:** Drop `--name` option. Default path to cwd. Output drops `projectName` and `indexPath`.
3. **FindCommand, OutlineCommand, ExtractCommand:** Drop `<name>` argument. Remove legacy migration calls. Add `.cache/` existence check with helpful error. Replace `store.LoadX(settings.Name, ...)` with `store.LoadX(...)`.
4. **Program.cs:** Remove ListCommand/PurgeCommand registrations. Wire `IndexStore` with cwd-derived cache dir.

### Phase 5: MCP server + Plugin

**Why last:** External interface. Change only after CLI is stable.

1. **mcp-server.js:** Remove `list_projects` tool definition and handler. Remove `project` property from all tool schemas. Remove `args.project` from CLI arg construction.
2. **Plugin skills:** Update SKILL.md files to remove project references. Add "run index first" guidance.
3. **Delete** hooks/ directory and scripts/ directory.

### Phase 6: Tests + Cleanup

1. Update test fixtures to use temp `.cache/` directories instead of `TestRootOverride`.
2. Remove any remaining references to project names in test assertions.
3. Update CLAUDE.md to reflect new command signatures.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Partial Migration with Compatibility Shims
**What:** Keeping `projectName` as an optional/defaulted parameter "for now"
**Why bad:** You end up with two code paths indefinitely. The PROJECT.md explicitly says no backwards compatibility.
**Instead:** Rip out `projectName` completely in one pass per layer. The compiler enforces completeness.

### Anti-Pattern 2: Making StoragePaths an Instance Class
**What:** Converting StoragePaths from static to instance, injecting it via DI alongside IndexStore
**Why bad:** Over-engineering. After the refactor, StoragePaths has 4 trivial path joins and one hash function. A separate DI-registered class for `Path.Combine(dir, "manifest.json")` is ceremony for ceremony's sake.
**Instead:** Collapse into IndexStore as private methods.

### Anti-Pattern 3: Leaving Catalog Logic "Just In Case"
**What:** Keeping `UpdateCatalog()` or `catalog.json` logic around
**Why bad:** The catalog exists to support `list` across multiple projects. With single-project local storage, there's no catalog. Dead code invites confusion.
**Instead:** Delete it all. If you need project discovery later, that's a different feature.

### Anti-Pattern 4: Deriving Cache Dir at Multiple Points
**What:** Each command independently computing `Path.Combine(cwd, ".cache")`
**Why bad:** If the cache dir name ever changes, you're hunting through every command. Also makes testing inconsistent.
**Instead:** Compute once in Program.cs, inject via IndexStore constructor. Commands never know the path.

## Patterns to Follow

### Pattern 1: Existence Guard in Commands
**What:** Every query command checks for `.cache/` before proceeding.
**When:** All query commands (find, outline, extract).
**Example:**
```csharp
public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
{
    var manifest = store.LoadManifest();
    if (manifest is null)
    {
        JsonOutput.WriteError("No index found in current directory. Run: token-squeeze index");
        return 1;
    }
    // ... proceed
}
```
This replaces the current "Project not found: {name}" error with actionable guidance.

### Pattern 2: IndexCommand Creates .cache/ Directory
**What:** Only the index command creates `.cache/`. Query commands never create it.
**When:** During `IndexStore.Save()`.
**Why:** Prevents accidentally creating empty `.cache/` dirs in random directories when someone runs `find` in the wrong folder.

### Pattern 3: SourcePath Stays in Manifest
**What:** `Manifest.SourcePath` still records the absolute path that was indexed.
**Why:** `ExtractCommand` needs it to find source files for byte-offset extraction. `StalenessChecker` needs it to check file existence and mtimes. The manifest lives in `.cache/` but points back to the project root (its parent).
**After refactor:** `SourcePath` = parent of `.cache/` directory.

## Scalability Considerations

Not applicable -- this is a local CLI tool. The refactor simplifies; it doesn't change scale characteristics. Parallel parsing, incremental indexing, and split storage all remain unchanged.

## Sources

- Direct codebase analysis of `src/TokenSqueeze/` (all source files read)
- `.planning/PROJECT.md` for requirements and constraints
- `.planning/codebase/ARCHITECTURE.md` for current architecture baseline
