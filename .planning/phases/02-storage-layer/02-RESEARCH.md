# Phase 2: Storage Layer - Research

**Researched:** 2026-03-09
**Domain:** C# .NET 9 — IndexStore refactoring, cache directory lifecycle, test infrastructure
**Confidence:** HIGH

## Summary

Phase 2 is a focused refactoring of `IndexStore` to use constructor-injected cache directories, add cache directory markers (CACHEDIR.TAG, .gitignore), and ensure lazy directory creation. The current codebase is already 80% there from Phase 1 — `IndexStore` already accepts `cacheDir` as a method parameter everywhere. The remaining work is: (1) move `cacheDir` to a constructor parameter and remove it from all method signatures, (2) add CACHEDIR.TAG and .gitignore creation in `Save()`, (3) ensure construction doesn't create directories, and (4) update tests to construct `IndexStore(tempDir)` instead of `new IndexStore()` + passing cacheDir to every call.

This is a mechanical refactoring with no new libraries needed. The risk is low — it's signature changes and file creation, not algorithmic work.

**Primary recommendation:** Single plan, two waves — first refactor IndexStore constructor + method signatures, then add cache markers and update tests.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| STOR-03 | IndexStore receives cache directory via constructor injection, no project-name parameters | Constructor refactoring pattern documented below; 15 production call sites + ~30 test call sites to update |
| STOR-04 | Cache directory created only during Save(), not during construction or queries | Guard against Directory.CreateDirectory in constructor; only Save() creates dirs |
| STOR-05 | CACHEDIR.TAG file written to .cache/ on index creation | Standard CACHEDIR.TAG spec documented below |
| STOR-06 | Self-ignoring .gitignore (containing `*`) written inside .cache/ on creation | Single-line file write in Save() |
| TEST-02 | Test infrastructure uses constructor-injected cache directory (no TestRootOverride hack) | TestRootOverride already removed in Phase 1; tests need IndexStore constructor update |
</phase_requirements>

## Standard Stack

No new libraries needed. This phase uses only what's already in the project.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9 | net9.0 | Runtime | Already in use |
| xUnit | (existing) | Tests | Already in use |
| System.Text.Json | (built-in) | Serialization | Already in use |

### Supporting
None new.

### Alternatives Considered
None — this is a refactoring phase, not a technology choice.

## Architecture Patterns

### Current State (What Exists)

```
IndexStore (stateless singleton)
  ├── Save(index, cacheDir)          # cacheDir passed per-call
  ├── Load(cacheDir)                 # cacheDir passed per-call
  ├── LoadManifest(cacheDir)         # cacheDir passed per-call
  ├── LoadFileSymbols(cacheDir, ...) # cacheDir passed per-call
  ├── LoadAllSymbols(cacheDir)       # cacheDir passed per-call
  ├── SaveManifest(cacheDir, ...)    # cacheDir passed per-call
  └── ... (9 more methods with cacheDir)
```

DI registration: `services.AddSingleton<IndexStore>()` — single instance shared across commands.

### Target State (What Phase 2 Produces)

```
IndexStore (instance per cache directory)
  ├── constructor(cacheDir)          # cacheDir stored as readonly field
  ├── Save(index)                    # uses _cacheDir
  ├── Load()                         # uses _cacheDir
  ├── LoadManifest()                 # uses _cacheDir
  ├── LoadFileSymbols(filePath)      # uses _cacheDir
  ├── LoadAllSymbols()               # uses _cacheDir
  └── ... (no cacheDir in signatures)
```

**Key design decision:** IndexStore can no longer be a singleton in the DI container. It becomes a transient/factory-created instance because each command determines its own cacheDir at runtime. Commands will construct `new IndexStore(cacheDir)` directly, or use a factory method.

### Pattern: Constructor-Injected Cache Directory

```csharp
internal sealed class IndexStore
{
    private readonly string _cacheDir;

    public IndexStore(string cacheDir)
    {
        // Store path only — do NOT create directory here (STOR-04)
        _cacheDir = Path.GetFullPath(cacheDir);
    }

    public void Save(CodeIndex index)
    {
        // Directory creation happens HERE, not in constructor
        Directory.CreateDirectory(_cacheDir);
        WriteCacheMarkers();  // CACHEDIR.TAG + .gitignore
        // ... rest of save logic using _cacheDir
    }
}
```

### Pattern: DI Registration Change

Since IndexStore is no longer a singleton (it needs a cacheDir per instance), remove it from DI. Commands and indexers construct it directly:

```csharp
// Program.cs: REMOVE this line
// services.AddSingleton<IndexStore>();

// Commands construct IndexStore with resolved cacheDir:
var cacheDir = Path.Combine(path, ".cache");
var store = new IndexStore(cacheDir);
var indexer = new ProjectIndexer(store, registry);
```

### Pattern: Cache Directory Markers

```csharp
private void WriteCacheMarkers()
{
    // CACHEDIR.TAG (standard spec: https://bford.info/cachedir/)
    var tagPath = Path.Combine(_cacheDir, "CACHEDIR.TAG");
    if (!File.Exists(tagPath))
    {
        File.WriteAllText(tagPath,
            "Signature: 8a477f597d28d172789f06886806bc55\n" +
            "# This file is a cache directory tag created by TokenSqueeze.\n" +
            "# For information see https://bford.info/cachedir/\n");
    }

    // Self-ignoring .gitignore
    var gitignorePath = Path.Combine(_cacheDir, ".gitignore");
    if (!File.Exists(gitignorePath))
    {
        File.WriteAllText(gitignorePath, "*\n");
    }
}
```

### Anti-Patterns to Avoid

- **Creating directories in the constructor:** STOR-04 explicitly forbids this. Construction is for queries too — creating dirs would pollute the filesystem on read-only operations.
- **Keeping IndexStore as a singleton:** With cacheDir baked in, it can't be shared across commands that might target different directories.
- **Writing CACHEDIR.TAG with wrong signature:** The spec requires the file to start with exactly `Signature: 8a477f597d28d172789f06886806bc55`. This is a fixed string, not a hash of anything.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CACHEDIR.TAG format | Custom format | Standard spec (bford.info/cachedir) | Fixed signature string required for tool interop |
| Atomic file writes | New implementation | Existing `AtomicWrite` / `AtomicWriteRaw` | Already battle-tested in codebase |

## Common Pitfalls

### Pitfall 1: Breaking Command DI Injection
**What goes wrong:** Commands currently receive `IndexStore` via constructor injection from DI. Removing IndexStore from DI breaks all command constructors.
**Why it happens:** Commands like `IndexCommand(IndexStore store, LanguageRegistry registry)` expect DI to provide IndexStore.
**How to avoid:** Update command constructors to only receive LanguageRegistry, then construct IndexStore locally after resolving cacheDir from command arguments/cwd.
**Warning signs:** Compile errors in all command classes.

### Pitfall 2: ProjectIndexer and IncrementalReindexer Constructor Signatures
**What goes wrong:** These classes currently receive IndexStore via constructor and pass cacheDir to its methods. With constructor-injected cacheDir, IndexStore is created first, then passed to these classes.
**Why it happens:** The dependency chain is: Command -> resolves cacheDir -> creates IndexStore(cacheDir) -> creates ProjectIndexer(store, registry).
**How to avoid:** Update ProjectIndexer and IncrementalReindexer to accept the new IndexStore (which already knows its cacheDir), and remove cacheDir from their method signatures too.

### Pitfall 3: QueryReindexer Static Method
**What goes wrong:** `QueryReindexer.EnsureFresh(cacheDir, store, registry, ct)` passes both cacheDir and store. After refactoring, cacheDir is redundant.
**How to avoid:** Update signature to `EnsureFresh(store, registry, ct)` — store already knows its cacheDir.

### Pitfall 4: CACHEDIR.TAG Written on Every Save
**What goes wrong:** Writing markers on every save is wasteful and causes unnecessary disk I/O.
**How to avoid:** Check `File.Exists()` before writing. The markers are write-once files.

### Pitfall 5: Test Temp Directory Cleanup
**What goes wrong:** Tests that create IndexStore with temp dirs must clean up properly.
**Why it happens:** IDisposable pattern already used in existing tests (e.g., SplitStorageTests). Consistent with existing patterns.
**How to avoid:** Follow existing test pattern: constructor creates temp dir, Dispose() deletes it.

## Code Examples

### Current Call Pattern (Before)
```csharp
// In IndexCommand.Execute():
var cacheDir = Path.Combine(path, ".cache");
var indexer = new ProjectIndexer(store, registry);  // store from DI
var result = indexer.Index(path, cacheDir);          // cacheDir passed through

// In ProjectIndexer.Index():
_store.Save(index, cacheDir);                        // cacheDir threaded down

// In OutlineCommand:
store.LoadFileSymbols(cacheDir, matchedKey);          // cacheDir everywhere
```

### Target Call Pattern (After)
```csharp
// In IndexCommand.Execute():
var cacheDir = Path.Combine(path, ".cache");
var store = new IndexStore(cacheDir);
var indexer = new ProjectIndexer(store, registry);
var result = indexer.Index(path);                     // no cacheDir arg

// In ProjectIndexer.Index():
_store.Save(index);                                   // store knows its dir

// In OutlineCommand:
var store = new IndexStore(cacheDir);
store.LoadFileSymbols(matchedKey);                    // clean API
```

### Test Pattern (After)
```csharp
public sealed class SomeStorageTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly IndexStore _store;

    public SomeStorageTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "ts-test-" + Guid.NewGuid().ToString("N"));
        // Do NOT create directory — let Save() handle it (STOR-04 verification)
        _store = new IndexStore(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }
}
```

## Affected Files Inventory

### Production Code (must change)
| File | Change Type | Details |
|------|-------------|---------|
| `Storage/IndexStore.cs` | Major refactor | Add constructor(cacheDir), remove cacheDir from all method signatures, add WriteCacheMarkers() |
| `Program.cs` | Remove line | Delete `services.AddSingleton<IndexStore>()` |
| `Commands/IndexCommand.cs` | Update | Remove IndexStore from DI, construct locally |
| `Commands/OutlineCommand.cs` | Update | Remove IndexStore from DI, construct locally |
| `Commands/ExtractCommand.cs` | Update | Remove IndexStore from DI, construct locally |
| `Commands/FindCommand.cs` | Update | Remove IndexStore from DI, construct locally |
| `Commands/ListCommand.cs` | Update | Remove IndexStore from DI, construct locally |
| `Commands/PurgeCommand.cs` | Update | Remove IndexStore from DI, construct locally |
| `Indexing/ProjectIndexer.cs` | Update | Remove cacheDir from Index() method |
| `Indexing/IncrementalReindexer.cs` | Update | Remove cacheDir from ReindexFiles() |
| `Storage/QueryReindexer.cs` | Update | Remove cacheDir from EnsureFresh() |

### Test Code (must change)
| File | Change Type | Details |
|------|-------------|---------|
| All files using `new IndexStore()` | Update constructor call | ~30 call sites across ~10 test files |
| All files passing cacheDir to store methods | Remove cacheDir arg | Every store.Load/Save/etc call |

Approximate scope: 11 production files, ~10 test files, ~60 individual call sites.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| Quick run command | `dotnet test src/TokenSqueeze.Tests --no-build -x` |
| Full suite command | `dotnet test src/TokenSqueeze.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| STOR-03 | IndexStore constructor accepts cacheDir, no project-name | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | Existing (needs update) |
| STOR-04 | Constructor does not create directory; Save() does | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | New test needed |
| STOR-05 | CACHEDIR.TAG written on Save() | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | New test needed |
| STOR-06 | .gitignore with `*` written on Save() | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | New test needed |
| TEST-02 | Tests use constructor-injected temp dirs | structural | Visual inspection — all `new IndexStore()` calls take a cacheDir arg | N/A (structural) |

### Sampling Rate
- **Per task commit:** `dotnet build src/token-squeeze.sln && dotnet test src/TokenSqueeze.Tests --no-build`
- **Per wave merge:** `dotnet test src/TokenSqueeze.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] New test: `IndexStore_Constructor_DoesNotCreateDirectory` — covers STOR-04
- [ ] New test: `Save_WritesCachedirTag` — covers STOR-05
- [ ] New test: `Save_WritesGitignore` — covers STOR-06
- [ ] New test: `Save_DoesNotOverwriteExistingMarkers` — covers idempotency

## Open Questions

1. **Should IndexStore keep internal access or become public?**
   - What we know: Currently `internal sealed class`. Constructor injection doesn't change this.
   - Recommendation: Keep `internal` — no reason to expose it.

2. **Should WriteCacheMarkers use AtomicWrite?**
   - What we know: CACHEDIR.TAG and .gitignore are small, write-once files. AtomicWrite is for crash-safety of frequently updated files.
   - Recommendation: Use plain `File.WriteAllText` with existence check. These files are never updated, just created once. Simpler is better.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection: IndexStore.cs, all command files, all test files using IndexStore
- CACHEDIR.TAG spec: https://bford.info/cachedir/ (well-established standard, stable since 2004)

### Secondary (MEDIUM confidence)
- .NET Directory.CreateDirectory behavior: idempotent, creates parent dirs, no error if exists (standard .NET behavior, verified by existing code usage)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new libraries, pure refactoring
- Architecture: HIGH - mechanical signature change, pattern is obvious from current code
- Pitfalls: HIGH - identified from direct code inspection of all affected files

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable — internal refactoring, no external dependencies)
