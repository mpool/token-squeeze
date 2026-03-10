# Phase 3: CLI Commands - Research

**Researched:** 2026-03-09
**Domain:** Spectre.Console.Cli command refactoring, C# .NET 9
**Confidence:** HIGH

## Summary

Phase 3 is a straightforward refactoring phase. The heavy lifting (storage layer, models, path resolution) was completed in Phases 1-2. What remains is mechanical: remove the `<name>` argument from query commands (outline, extract, find), delete two commands (list, purge) and their files, add a cache-existence guard, and clean up Program.cs registrations.

The codebase is already 80% there. The IndexCommand already correctly derives `cacheDir` from the target path. The query commands already have `ResolveCacheDir` helper methods and TODO comments marking them for Phase 3 cleanup. ListCommand and PurgeCommand are already gutted stubs.

**Primary recommendation:** Single plan, four tasks: (1) refactor query command Settings to drop `<name>`, (2) add shared cache-existence guard, (3) delete list/purge files and registrations, (4) verify build + existing tests pass.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CLI-01 | `index` stores output in `<target>/.cache/`, no project naming | Already implemented in Phase 2. IndexCommand derives `cacheDir = Path.Combine(path, ".cache")`. No changes needed. |
| CLI-02 | `outline` drops `<name>` argument, reads from cwd `.cache/` | Remove `Name` from Settings, resolve cacheDir from cwd. See Architecture Patterns. |
| CLI-03 | `extract` drops `<name>` argument, reads from cwd `.cache/` | Remove `Name` from Settings, resolve cacheDir from cwd. See Architecture Patterns. |
| CLI-04 | `find` drops `<name>` argument, reads from cwd `.cache/` | Remove `Name` from Settings, resolve cacheDir from cwd. See Architecture Patterns. |
| CLI-05 | `list` command deleted | Delete ListCommand.cs file |
| CLI-06 | `purge` command deleted | Delete PurgeCommand.cs file |
| CLI-07 | Query commands return clear error when `.cache/` doesn't exist | Shared guard pattern. See Architecture Patterns. |
| CLI-08 | `Program.cs` removes `list` and `purge` registrations | Remove two `config.AddCommand` lines |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Spectre.Console.Cli | 0.49.1 | CLI command framework | Already in use, handles argument parsing and command routing |

No new libraries needed. This phase is pure refactoring of existing code.

## Architecture Patterns

### Current State Analysis

```
IndexCommand:     path arg -> cacheDir = Path.Combine(path, ".cache")     [DONE - no changes]
OutlineCommand:   <name> <file> -> ResolveCacheDir(name)                  [NEEDS: drop <name>]
ExtractCommand:   <name> [id] --batch -> ResolveCacheDir(name)            [NEEDS: drop <name>]
FindCommand:      <name> <query> -> ResolveCacheDir(name)                 [NEEDS: drop <name>]
ListCommand:      (stub)                                                  [DELETE]
PurgeCommand:     (stub)                                                  [DELETE]
```

### Pattern 1: Query Command Settings After Refactor

Each query command's Settings class drops the `Name` property. The first `CommandArgument(0, ...)` shifts down.

**OutlineCommand.Settings (before):**
```csharp
[CommandArgument(0, "<name>")]
public string Name { get; init; } = string.Empty;

[CommandArgument(1, "<file>")]
public string File { get; init; } = string.Empty;
```

**OutlineCommand.Settings (after):**
```csharp
[CommandArgument(0, "<file>")]
[Description("The file to show symbols for")]
public string File { get; init; } = string.Empty;
```

**ExtractCommand.Settings (after):**
```csharp
[CommandArgument(0, "[id]")]
[Description("The symbol ID to extract")]
public string? Id { get; init; }

[CommandOption("--batch <IDS>")]
[Description("Extract multiple symbols by ID")]
public string[]? Batch { get; init; }
```

**FindCommand.Settings (after):**
```csharp
[CommandArgument(0, "<query>")]
[Description("The search query")]
public string Query { get; init; } = string.Empty;
// --kind, --path, --limit options unchanged
```

### Pattern 2: Cache Directory Resolution (Simplified)

All query commands use the same one-liner. The `ResolveCacheDir(string name)` methods are replaced with a direct cwd-based resolve:

```csharp
var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache");
```

No helper method needed -- it's a single line.

### Pattern 3: Cache Existence Guard

All three query commands need the same guard before constructing IndexStore. This replaces the current "Project not found" error:

```csharp
var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache");
if (!Directory.Exists(cacheDir))
{
    JsonOutput.WriteError("No index found. Run /token-squeeze:index");
    return 1;
}
var store = new IndexStore(cacheDir);
```

**Key detail:** The error message is specified in the requirements as exactly `"No index found. Run /token-squeeze:index"`. Use this exact string.

**Design choice -- inline vs. shared method:** The guard is 5 lines. Three commands use it. A shared static method would be clean but adds a new file/class for minimal benefit. Recommendation: inline in each command. It's simple, obvious, and avoids indirection for 5 lines of code.

### Pattern 4: Program.cs After Cleanup

```csharp
config.AddCommand<IndexCommand>("index")
    .WithDescription("Index a local folder");
config.AddCommand<OutlineCommand>("outline")
    .WithDescription("Show symbols in a file");
config.AddCommand<ExtractCommand>("extract")
    .WithDescription("Get full source of a symbol");
config.AddCommand<FindCommand>("find")
    .WithDescription("Search symbols by query");
config.AddCommand<ParseTestCommand>("parse-test")
    .IsHidden()
    .WithDescription("Parse a file and dump extracted symbols as JSON");
```

Note: `ParseTestCommand` stays -- it's a hidden debug command unrelated to this refactoring.

### Anti-Patterns to Avoid
- **Leaving dead `using` statements:** After removing `Name` from Settings, the `ResolveCacheDir` private methods should be deleted. Ensure no orphaned imports remain.
- **Forgetting to update error messages:** Current error messages say "Project not found: {name}". These must change to the cache-not-found message since there's no project name concept anymore.

## Don't Hand-Roll

Not applicable for this phase. No complex problems to solve -- it's mechanical refactoring.

## Common Pitfalls

### Pitfall 1: Argument Position Mismatch
**What goes wrong:** After removing `<name>`, forgetting to renumber `CommandArgument` positions from 0.
**Why it happens:** Spectre.Console.Cli uses positional indices. If `<file>` stays at position 1 but there's no position 0, the command breaks.
**How to avoid:** Every Settings class must have arguments numbered starting from 0 with no gaps.
**Warning signs:** Runtime error "Could not match argument" or wrong values bound to wrong properties.

### Pitfall 2: ExtractCommand Has Optional First Arg
**What goes wrong:** ExtractCommand's `[id]` is optional (square brackets), with `--batch` as alternative. After removing `<name>`, the `[id]` becomes position 0. This is fine -- Spectre.Console handles optional args at any position.
**How to avoid:** Keep `[id]` (optional) at position 0. The existing "no ID provided" guard handles the case where neither `id` nor `--batch` is supplied.

### Pitfall 3: Not Deleting the Files
**What goes wrong:** Removing registrations from Program.cs but leaving ListCommand.cs and PurgeCommand.cs in the project. Dead code accumulates.
**How to avoid:** Delete the .cs files entirely. The compiler won't complain (nothing references them), but they're clutter.

## Code Examples

### Complete OutlineCommand After Refactor
```csharp
internal sealed class OutlineCommand(LanguageRegistry registry) : Command<OutlineCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The file to show symbols for")]
        public string File { get; init; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        try
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache");
            if (!Directory.Exists(cacheDir))
            {
                JsonOutput.WriteError("No index found. Run /token-squeeze:index");
                return 1;
            }
            var store = new IndexStore(cacheDir);

            var manifest = QueryReindexer.EnsureFresh(store, registry, cancellation);
            if (manifest is null)
            {
                JsonOutput.WriteError("No index found. Run /token-squeeze:index");
                return 1;
            }

            // ... rest of file matching and symbol loading unchanged ...
        }
        catch (Exception ex)
        {
            JsonOutput.WriteError(ex.Message);
            return 1;
        }
    }
}
```

## State of the Art

No framework changes relevant. Spectre.Console.Cli 0.49.1 is current. The command attribute model hasn't changed.

## Open Questions

1. **Should `index` also work with cwd (no path arg)?**
   - What we know: Current `index` requires `<path>`. Requirements say CLI-01 is "stores output in `<target>/.cache/`" -- the `<path>` argument stays.
   - Recommendation: Leave `index <path>` as-is. No change needed for CLI-01 -- it already works correctly.

2. **Should the manifest-null guard after EnsureFresh also use the standard error message?**
   - What we know: If `.cache/` exists but `manifest.json` is missing or corrupt, `EnsureFresh` returns null.
   - Recommendation: Yes, use the same error message. From the user's perspective, a corrupt/missing manifest is the same as no index.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| Quick run command | `dotnet test src/TokenSqueeze.Tests --no-build -x` |
| Full suite command | `dotnet test src/TokenSqueeze.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CLI-01 | index writes to `<target>/.cache/` | Already covered | N/A (no change) | N/A |
| CLI-02 | outline drops `<name>`, reads from cwd | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | No -- Wave 0 |
| CLI-03 | extract drops `<name>`, reads from cwd | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | No -- Wave 0 |
| CLI-04 | find drops `<name>`, reads from cwd | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | No -- Wave 0 |
| CLI-05 | list command deleted | manual-only | Verify `ListCommand.cs` file doesn't exist | N/A |
| CLI-06 | purge command deleted | manual-only | Verify `PurgeCommand.cs` file doesn't exist | N/A |
| CLI-07 | error when `.cache/` missing | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | No -- Wave 0 |
| CLI-08 | Program.cs only registers 4 commands | manual-only | Inspect Program.cs | N/A |

### Sampling Rate
- **Per task commit:** `dotnet build src/token-squeeze.sln && dotnet test src/TokenSqueeze.Tests --no-build`
- **Per wave merge:** `dotnet test src/TokenSqueeze.Tests`
- **Phase gate:** Full suite green before verify

### Wave 0 Gaps
- [ ] `src/TokenSqueeze.Tests/CliCommandTests.cs` -- covers CLI-02, CLI-03, CLI-04, CLI-07
  - Note: Testing CLI commands via Spectre.Console.Testing `CommandAppTester` is possible but may be heavyweight for this phase. Alternative: test the cache guard logic directly, verify Settings classes compile with correct argument positions. The existing test suite validates lower-level storage/parser behavior. Command-level integration tests can be deferred to TEST-01 in Phase 4.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection of all command files, Program.cs, IndexStore, StoragePaths
- Spectre.Console.Cli attribute model observed in existing working code

### Secondary (MEDIUM confidence)
- Spectre.Console.Testing package already in test project -- available for command-level tests if needed

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new libraries, pure refactoring
- Architecture: HIGH - patterns derived from existing working code and explicit TODO markers
- Pitfalls: HIGH - argument position numbering is the only real gotcha, well-documented in Spectre.Console

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable -- no external dependencies changing)
