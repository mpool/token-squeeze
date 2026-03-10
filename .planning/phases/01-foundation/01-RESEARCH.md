# Phase 1: Foundation - Research

**Researched:** 2026-03-09
**Domain:** C# .NET 9 data model refactoring, path resolution, security boundaries
**Confidence:** HIGH

## Summary

Phase 1 is a surgical refactoring of models, path resolution, and directory walking. No new libraries, no new frameworks -- just reshaping existing code to remove the `ProjectName` concept and make all storage paths resolve relative to a cache directory parameter instead of the global `~/.token-squeeze/projects/<name>/` tree.

The codebase is small and well-structured. All changes are internal -- no public API changes, no dependency changes. The main risk is incomplete grep-and-replace of `ProjectName` references across files that won't be fully reworked until later phases (commands, MCP server). Phase 1 must change models and `StoragePaths` without breaking compilation of downstream consumers that still use project names (those consumers change in Phase 2-3).

**Primary recommendation:** Change models and `StoragePaths` first, then update `DirectoryWalker`, then verify `PathValidator` callers. The models are leaves in the dependency graph; `StoragePaths` is consumed everywhere. Changing `StoragePaths` will cause cascading compile errors in `IndexStore`, `ProjectIndexer`, `IncrementalReindexer`, and commands -- but Phase 1 only needs to make the *foundation* correct, not fix all callers (that's Phase 2-3).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Cache directory: `.cache/` (not `.token-squeeze/`)
- `CodeIndex`: remove `ProjectName` property entirely, keep `SourcePath`
- `Manifest`: remove `ProjectName` property, keep `SourcePath` and `FormatVersion`
- `ManifestHeader`: delete entirely
- `StoragePaths`: make methods take cache directory parameter instead of project name; remove `RootDir`, `CatalogPath`, `GetProjectDir()`, `GetLegacyIndexPath()`, `GetMetadataPath()`, `EnsureRootExists()`, `TestRootOverride`; keep `PathToStorageKey()`, `GetManifestPath()`, `GetFilesDir()`, `GetFileFragmentPath()`, `GetSearchIndexPath()` (all take cache dir root)
- `DirectoryWalker`: add `.cache` to `SkippedDirectories`
- Security: `PathValidator.ValidateWithinRoot` callers must pass cache directory as root, not project root
- `SourcePath`: keep as absolute path in `CodeIndex` and `Manifest`
- No migration code -- clean break

### Claude's Discretion
- Whether `StoragePaths` becomes instance class or stays static with parameter (as long as no project name dependency)
- Internal naming of path resolution methods
- Whether to bump `FormatVersion` in Manifest (likely yes -- format is incompatible)

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MODL-01 | `CodeIndex` model drops `ProjectName` property | Direct model edit -- remove property, update all construction sites |
| MODL-02 | `Manifest` / `ManifestHeader` drop `ProjectName` property | Remove from `Manifest`, delete `ManifestHeader` entirely (MODL-03) |
| MODL-03 | `ManifestHeader` deleted if no longer needed | Delete record from `Manifest.cs`, remove `LoadManifestHeader()` from `IndexStore`, remove `UpdateCatalog()` |
| STOR-01 | Index data stored in `<project-root>/.cache/` instead of `~/.token-squeeze/projects/<name>/` | `StoragePaths` method signatures change from `(string projectName)` to `(string cacheDir)` |
| STOR-02 | `StoragePaths` resolves root from cwd, no project name in any path | Remove `RootDir`, `CatalogPath`, `GetProjectDir()`, `EnsureRootExists()`, `TestRootOverride`; remaining methods take cache dir |
| SEC-01 | `PathValidator.ValidateWithinRoot` scoped to cache directory, not project root | `PathValidator` itself unchanged; callers in `IndexStore` must pass `.cache/` path as root boundary |
| SEC-02 | `DirectoryWalker` excludes `.cache/` directory | Add `".cache"` to `SkippedDirectories` HashSet |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9 | net9.0 | Runtime | Already in use |
| xUnit | 2.9.2 | Testing | Already in use |
| Spectre.Console.Cli | 0.49.1 | CLI framework | Already in use |

No new libraries needed for Phase 1. This is pure refactoring.

## Architecture Patterns

### Current Dependency Graph (relevant to Phase 1)

```
Commands (IndexCommand, etc.)
    |
    v
ProjectIndexer / IncrementalReindexer
    |
    v
IndexStore  ------>  StoragePaths (static)
    |                     |
    v                     v
Models (CodeIndex,    Path resolution
 Manifest, etc.)      (~/.token-squeeze/projects/<name>/...)
    |
    v
PathValidator (generic -- unchanged)
```

### After Phase 1

```
Commands (temporarily broken -- fixed in Phase 2/3)
    |
    v
ProjectIndexer / IncrementalReindexer (temporarily broken -- fixed in Phase 2)
    |
    v
IndexStore  ------>  StoragePaths (static, takes cacheDir param)
    |                     |
    v                     v
Models (no ProjectName)  Path resolution
                         (<cacheDir>/manifest.json, etc.)
    |
    v
PathValidator (unchanged -- callers pass cacheDir as root)
```

### Pattern: Static Utility with Parameter

Keep `StoragePaths` as a `static class` but change all methods from `GetXxx(string projectName)` to `GetXxx(string cacheDir)`. This is the simplest change and defers the instance-vs-static decision to Phase 2 (where `IndexStore` gets constructor injection).

```csharp
// BEFORE
public static string GetManifestPath(string projectName)
    => Path.Combine(GetProjectDir(projectName), "manifest.json");

// AFTER
public static string GetManifestPath(string cacheDir)
    => Path.Combine(cacheDir, "manifest.json");
```

**Rationale:** The CONTEXT.md says Claude's discretion on instance vs static. Static-with-parameter is the minimal Phase 1 change. Phase 2 can wrap it in an instance if needed when `IndexStore` gets constructor injection.

### Pattern: Compilation Firewall

Phase 1 changes will break callers (`IndexStore`, `ProjectIndexer`, `IncrementalReindexer`, commands). Two approaches:

1. **Fix callers minimally** -- add a temporary `cacheDir` parameter threading through `IndexStore` methods, keeping them compiling but not yet wired correctly end-to-end. This lets `dotnet build` pass.
2. **Accept broken build** -- change models and `StoragePaths`, leave callers broken, fix in Phase 2.

**Recommendation:** Option 1. The phase success criteria say "serialization round-trips without ProjectName" and "storage path resolution produces `<cwd>/.cache/`" -- both require compiling code. Thread a `cacheDir` parameter through `IndexStore` minimally. The `ProjectIndexer` and commands can receive temporary stubs or hardcoded values that Phase 2/3 will replace.

### Anti-Patterns to Avoid
- **Half-removing ProjectName:** Don't leave `ProjectName` as an optional/nullable property "for compatibility." The whole point is to delete it. Serialization of old manifests with `projectName` in JSON will simply ignore the unknown property (System.Text.Json default behavior).
- **Circular dependency on Phase 2:** Don't implement constructor injection in `IndexStore` yet. That's STOR-03/STOR-04 (Phase 2). Just make `StoragePaths` methods take a `cacheDir` string.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON unknown property handling | Custom deserialization for old format | System.Text.Json default (ignores unknown props) | Default `JsonSerializerOptions` already skips unknown properties -- old manifests with `projectName` key will deserialize fine into the new `Manifest` without that property |
| Path normalization | Custom path joining | `Path.Combine()` + `Path.GetFullPath()` | Already used throughout; consistent cross-platform behavior |

## Common Pitfalls

### Pitfall 1: ProjectName References Outside Models
**What goes wrong:** Removing `ProjectName` from `CodeIndex` and `Manifest` causes compile errors in 10+ locations across `IndexStore`, `ProjectIndexer`, `IncrementalReindexer`, and commands.
**Why it happens:** `ProjectName` is threaded through the entire storage layer.
**How to avoid:** Map all references first (grep done above -- 22 references across 6 files). Plan changes in dependency order: models first, then `StoragePaths`, then `IndexStore`, then callers.
**Warning signs:** Compile errors in files you didn't plan to touch.

**Full reference map (from grep):**

| File | References | Phase 1 Action |
|------|-----------|----------------|
| `Models/CodeIndex.cs` | Property definition | Remove property |
| `Models/Manifest.cs` | Property definition (Manifest + ManifestHeader) | Remove from Manifest, delete ManifestHeader |
| `Models/ProjectMetadata.cs` | Property definition | Delete entire file (used only by `LoadMetadata` which is being removed) |
| `Storage/IndexStore.cs` | 15+ references via `index.ProjectName`, `manifest.ProjectName` | Thread `cacheDir` parameter instead |
| `Indexing/ProjectIndexer.cs` | `ResolveProjectName()`, sets `ProjectName` on CodeIndex | Remove name resolution, remove ProjectName from construction |
| `Indexing/IncrementalReindexer.cs` | `manifest.ProjectName` in 4 calls | Thread `cacheDir` parameter instead |
| `Commands/IndexCommand.cs` | Reads `result.Index.ProjectName` for output | Adjust output (remove projectName field or use sourcePath) |

### Pitfall 2: FormatVersion Bump Forgotten
**What goes wrong:** Old manifests (version 2) load fine but have different semantics. No way to distinguish old vs new format.
**Why it happens:** The structural change (no ProjectName) isn't reflected in metadata.
**How to avoid:** Bump `FormatVersion` to 3 in `Save()`. Since there's no migration, this is purely documentation -- but it prevents confusion if someone manually inspects the JSON.

### Pitfall 3: DirectoryWalker Case Sensitivity
**What goes wrong:** `.cache` added to `SkippedDirectories` but doesn't match `.Cache` on case-sensitive filesystems.
**Why it happens:** Forgetting the set uses `StringComparer.OrdinalIgnoreCase`.
**How to avoid:** The existing `SkippedDirectories` HashSet already uses `OrdinalIgnoreCase`. Just add the lowercase string `".cache"` -- it will match `.Cache`, `.CACHE`, etc. No pitfall here, but worth noting for the planner.

### Pitfall 4: StoragePaths.CatalogPath and UpdateCatalog
**What goes wrong:** `IndexStore.Save()` calls `UpdateCatalog()` which uses `StoragePaths.CatalogPath`, `ListProjects()`, and `LoadManifestHeader()`. All three depend on the global root directory concept.
**Why it happens:** The catalog was for the `list` command (being deleted in Phase 3).
**How to avoid:** Remove `UpdateCatalog()`, `LoadCatalogJson()`, `ListProjects()`, `LoadManifestHeader()` from `IndexStore` in this phase. They serve no purpose without global storage. Also remove `StoragePaths.CatalogPath`.

### Pitfall 5: Legacy Format Methods
**What goes wrong:** `IsLegacyFormat()`, `GetLegacySourcePath()`, and legacy cleanup in `Save()` all reference `GetLegacyIndexPath()` and `GetMetadataPath()` -- both being removed from `StoragePaths`.
**Why it happens:** Clean break means no migration, so legacy detection code is dead.
**How to avoid:** Remove these methods from `IndexStore`: `IsLegacyFormat()`, `GetLegacySourcePath()`, `LoadMetadata()`. Remove legacy cleanup block from `Save()`.

## Code Examples

### Model Changes

```csharp
// CodeIndex.cs -- AFTER
namespace TokenSqueeze.Models;

public sealed record CodeIndex
{
    public required string SourcePath { get; init; }
    public required DateTime IndexedAt { get; init; }
    public required Dictionary<string, IndexedFile> Files { get; init; }
    public required List<Symbol> Symbols { get; init; }
}
```

```csharp
// Manifest.cs -- AFTER (ManifestHeader deleted, ProjectMetadata.cs deleted)
namespace TokenSqueeze.Models;

public sealed record Manifest
{
    public required int FormatVersion { get; init; }
    public required string SourcePath { get; init; }
    public required DateTime IndexedAt { get; init; }
    public required Dictionary<string, ManifestFileEntry> Files { get; init; }
}

// ManifestHeader record: DELETED
// ManifestFileEntry: UNCHANGED
```

### StoragePaths Changes

```csharp
// StoragePaths.cs -- AFTER
using System.Security.Cryptography;
using System.Text;

namespace TokenSqueeze.Storage;

internal static class StoragePaths
{
    // REMOVED: TestRootOverride, RootDir, CatalogPath, GetProjectDir(),
    //          GetLegacyIndexPath(), GetMetadataPath(), EnsureRootExists()

    public static string GetManifestPath(string cacheDir)
        => Path.Combine(cacheDir, "manifest.json");

    public static string GetFilesDir(string cacheDir)
        => Path.Combine(cacheDir, "files");

    public static string GetFileFragmentPath(string cacheDir, string storageKey)
        => Path.Combine(GetFilesDir(cacheDir), storageKey + ".json");

    public static string GetSearchIndexPath(string cacheDir)
        => Path.Combine(cacheDir, "search-index.json");

    private const int MaxStorageKeyLength = 200;

    public static string PathToStorageKey(string relativePath)
    {
        // UNCHANGED -- pure function, no project concept
        var key = relativePath
            .Replace('/', '-')
            .Replace('\\', '-')
            .TrimStart('-', '.');

        if (string.IsNullOrEmpty(key))
            return "_empty";

        if (key.Length > MaxStorageKeyLength)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            var hashHex = Convert.ToHexString(hash)[..16].ToLowerInvariant();
            return key[..100] + "-" + hashHex;
        }

        return key;
    }
}
```

### DirectoryWalker Change

```csharp
// Add ".cache" to the existing set -- single line change
internal static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
{
    "node_modules", ".git", "bin", "obj", ".vs", ".idea",
    "__pycache__", ".mypy_cache", ".pytest_cache",
    "dist", "build", ".next", ".nuxt",
    ".cache"  // <-- added
};
```

### IndexStore Security Boundary Change

```csharp
// BEFORE (in IndexStore.Save)
PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir);

// AFTER -- cacheDir IS the root boundary
// No ValidateWithinRoot needed for the cache dir itself (it IS the root).
// Fragment paths still validated against cache dir:
PathValidator.ValidateWithinRoot(fragmentPath, cacheDir);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Global `~/.token-squeeze/projects/<name>/` | Local `<cwd>/.cache/` | This refactoring | Eliminates project naming, global state, catalog |
| `ManifestHeader` for list command | Deleted | This refactoring | `list` command removed in Phase 3 |
| `ProjectMetadata` model | Deleted | This refactoring | Was only used by `LoadMetadata()` which is removed |

## Open Questions

1. **Should `IndexStore` compile at end of Phase 1?**
   - What we know: Changing models and `StoragePaths` will break `IndexStore` compilation. Phase 2 is specifically about `IndexStore` constructor injection (STOR-03, STOR-04).
   - What's unclear: Whether to thread `cacheDir` through `IndexStore` methods now or leave it broken for Phase 2.
   - Recommendation: Thread `cacheDir` minimally so the build passes. The success criteria require "storage path resolution produces `<cwd>/.cache/`" which implies working code, not just changed signatures. But keep it simple -- a `cacheDir` parameter on key methods, not full DI.

2. **FormatVersion value**
   - What we know: Currently version 2. Format is incompatible after removing `ProjectName`.
   - Recommendation: Bump to 3. Cheap change, good hygiene.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| Quick run command | `dotnet test src/TokenSqueeze.Tests --filter "Category!=Slow" --no-build -q` |
| Full suite command | `dotnet test src/TokenSqueeze.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MODL-01 | `CodeIndex` has no `ProjectName` -- serialization round-trips | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~ModelSerializationTests" -q` | No -- Wave 0 |
| MODL-02 | `Manifest` has no `ProjectName` -- serialization round-trips | unit | Same as above | No -- Wave 0 |
| MODL-03 | `ManifestHeader` deleted -- type does not exist | unit (compile-time) | `dotnet build src/TokenSqueeze/TokenSqueeze.csproj` (if it compiles without ManifestHeader, requirement met) | N/A |
| STOR-01 | Storage paths resolve to `<cwd>/.cache/` | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~StoragePathTests" -q` | No -- Wave 0 |
| STOR-02 | No project name segment in any path | unit | Same as STOR-01 | No -- Wave 0 |
| SEC-01 | `PathValidator.ValidateWithinRoot` rejects paths escaping cache dir | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SecurityTests" -q` | No -- Wave 0 |
| SEC-02 | `DirectoryWalker` skips `.cache/` | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~DirectoryWalkerTests" -q` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build src/token-squeeze.sln && dotnet test src/TokenSqueeze.Tests --no-build -q`
- **Per wave merge:** `dotnet test src/TokenSqueeze.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `src/TokenSqueeze.Tests/ModelSerializationTests.cs` -- covers MODL-01, MODL-02 (round-trip serialization without ProjectName)
- [ ] `src/TokenSqueeze.Tests/StoragePathTests.cs` -- covers STOR-01, STOR-02 (path resolution to `.cache/`)
- [ ] `src/TokenSqueeze.Tests/SecurityTests.cs` -- covers SEC-01 (PathValidator with cache dir boundary)
- [ ] `src/TokenSqueeze.Tests/DirectoryWalkerTests.cs` -- covers SEC-02 (`.cache` in SkippedDirectories)

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection of all affected source files
- `CodeIndex.cs`, `Manifest.cs`, `StoragePaths.cs`, `IndexStore.cs`, `PathValidator.cs`, `DirectoryWalker.cs`, `ProjectIndexer.cs`, `IncrementalReindexer.cs`, `IndexCommand.cs`, `ProjectMetadata.cs`

### Secondary (HIGH confidence)
- CONTEXT.md locked decisions (user-validated design choices)
- REQUIREMENTS.md traceability matrix

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, pure refactoring of existing C# code
- Architecture: HIGH -- complete dependency graph traced through actual source code
- Pitfalls: HIGH -- all `ProjectName` references grep'd and mapped; removal cascade is fully understood

**Research date:** 2026-03-09
**Valid until:** indefinite (internal codebase refactoring, no external dependency concerns)
