# Codebase Concerns

**Analysis Date:** 2026-03-08

## Tech Debt

**No Automated Test Suite:**
- Issue: The project has zero unit or integration tests. The `tests/` directory contains only sample fixture files (`sample.py`, `sample.ts`, etc.) used for manual `parse-test` invocations. There is no test project, no test framework dependency, and no test runner configuration.
- Files: `tests/sample.*` (fixture files only), no `*.Tests.csproj` exists
- Impact: Every change to parsing logic, command behavior, or storage is validated manually. Regressions in symbol extraction across 7 language specs go undetected. The `SymbolExtractor` (472 lines) and `LanguageRegistry` (315 lines) are the most complex files and the most likely to regress.
- Fix approach: Add an xUnit/NUnit test project under `tests/TokenSqueeze.Tests/`. Priority targets: `SymbolExtractor.ExtractSymbols()` against each language fixture, `FindCommand` scoring logic, `DirectoryWalker` filtering, `PathValidator` edge cases.

**LanguageRegistry Duplication (TypeScript vs TSX):**
- Issue: `RegisterTypeScript()` and `RegisterTsx()` are nearly identical -- same `SymbolNodeTypes`, `NameFields`, `ParamFields`, `ReturnTypeFields`, `ContainerNodeTypes`, `ConstantPatterns`, and `TypePatterns`. Only `LanguageId`, `DisplayName`, and `Extensions` differ.
- Files: `src/TokenSqueeze/Parser/LanguageRegistry.cs` (lines 119-195)
- Impact: Any fix to TypeScript parsing must be duplicated to TSX. Easy to forget one.
- Fix approach: Extract a shared helper like `BuildTypeScriptSpec(string id, string display, string[] extensions)` that both call.

**IndexStore Creates New Instance Per Command:**
- Issue: Every command (`IndexCommand`, `ListCommand`, `FindCommand`, etc.) creates `new IndexStore()` directly instead of receiving it via injection. This makes testing impossible without hitting the filesystem and violates DI principles.
- Files: `src/TokenSqueeze/Commands/IndexCommand.cs` (line 36), `src/TokenSqueeze/Commands/ListCommand.cs` (line 17), `src/TokenSqueeze/Commands/FindCommand.cs` (line 38), `src/TokenSqueeze/Commands/ExtractCommand.cs` (line 30), `src/TokenSqueeze/Commands/OutlineCommand.cs` (line 22), `src/TokenSqueeze/Commands/PurgeCommand.cs` (line 21)
- Impact: Cannot unit test commands without filesystem side effects. Spectre.Console.Cli supports DI via `TypeRegistrar` but it is not used.
- Fix approach: Register `IndexStore` (and `LanguageRegistry`) via Spectre's `TypeRegistrar` and inject through command constructors.

**ParseTestCommand Left in Production Build:**
- Issue: `parse-test` is registered as a CLI command in `Program.cs` with a comment "(hidden)" but it is not actually hidden -- it appears in help output and is fully accessible. It is a debugging/development tool that should not ship.
- Files: `src/TokenSqueeze/Program.cs` (line 38), `src/TokenSqueeze/Commands/ParseTestCommand.cs`
- Impact: Clutters the CLI interface. Exposes internal parsing details to end users.
- Fix approach: Either gate behind `#if DEBUG` like the exception propagation on line 43, or use Spectre's `.IsHidden()` to hide it from help while keeping it accessible.

## Known Bugs

**OutlineCommand Parent-Child Matching Uses Name Instead of Id:**
- Symptoms: Child symbols may fail to group under their parent if the `Parent` field (which stores the parent's `Id`) does not match the parent's `Name`. The lookup on line 58 uses `root.Name` as the dictionary key but `s.Parent` stores a full symbol ID (format: `file::qualifiedName#Kind`).
- Files: `src/TokenSqueeze/Commands/OutlineCommand.cs` (lines 51-58)
- Trigger: Any file with nested symbols (e.g., methods inside a class). The `childrenByParent` dictionary keys on `s.Parent` (an Id string like `path::ClassName#Class`) but lookups use `root.Name` (just `ClassName`). These never match.
- Workaround: The outline still shows all symbols, but they all appear as root-level -- no hierarchy is ever displayed.

**Method Detection Logic is Wrong:**
- Symptoms: The condition `spec.ContainerNodeTypes.Any(ct => scopeParts.Count > 0)` on line 50 of `SymbolExtractor` checks whether `ContainerNodeTypes` is non-empty AND `scopeParts` is non-empty. The `ct` parameter is unused. The intent was likely to check if the function is inside a container scope, but the LINQ expression is semantically equivalent to just `scopeParts.Count > 0 && spec.ContainerNodeTypes.Count > 0`.
- Files: `src/TokenSqueeze/Parser/SymbolExtractor.cs` (lines 49-52)
- Trigger: Any function inside a scope will be marked as Method regardless of whether the parent scope is actually a container type. Works by coincidence for most cases but is logically incorrect.
- Workaround: Happens to produce correct results in typical scenarios because `scopeParts` is only populated when recursing into containers.

**GlobToRegex Does Not Anchor Partial Paths:**
- Symptoms: Path filter `--path "src/*.ts"` requires the pattern to match the entire file path from start to end due to `^` and `$` anchors. Users must use `**/src/*.ts` to match files in subdirectories.
- Files: `src/TokenSqueeze/Commands/FindCommand.cs` (lines 130-141)
- Trigger: Using `--path "src/utils.ts"` when the actual path is `project/src/utils.ts`.
- Workaround: Users must always prefix patterns with `**/` for non-rooted searches.

## Security Considerations

**Project Name Not Sanitized for Filesystem:**
- Risk: `ResolveProjectName` in `ProjectIndexer` uses the directory name or user-provided `--name` as a filesystem path component without sanitizing characters that are invalid or dangerous in paths (e.g., `..`, `/`, `\`, NUL).
- Files: `src/TokenSqueeze/Indexing/ProjectIndexer.cs` (lines 92-113), `src/TokenSqueeze/Storage/StoragePaths.cs` (line 11)
- Current mitigation: `StoragePaths.GetProjectDir` uses `Path.Combine` which does not prevent traversal. `PathValidator` exists but is not called on project names.
- Recommendations: Sanitize project names to alphanumeric + hyphens, or validate with `PathValidator.ValidateWithinRoot` before constructing storage paths.

**PurgeCommand Deletes Directory Without Path Validation:**
- Risk: `store.Delete(settings.Name)` calls `Directory.Delete(projectDir, recursive: true)` where `projectDir` is constructed from user input. A crafted project name could potentially target directories outside `~/.token-squeeze/projects/`.
- Files: `src/TokenSqueeze/Commands/PurgeCommand.cs` (line 30), `src/TokenSqueeze/Storage/IndexStore.cs` (lines 56-61)
- Current mitigation: The `Load` check on line 22 of PurgeCommand verifies the index exists first, but this only checks for `index.json` presence, not path safety.
- Recommendations: Add `PathValidator.ValidateWithinRoot(projectDir, StoragePaths.RootDir)` before deletion in `IndexStore.Delete()`.

**Only Root-Level .gitignore Is Respected:**
- Risk: `DirectoryWalker` reads only the `.gitignore` at the project root. Nested `.gitignore` files (common in monorepos) are ignored, potentially indexing files that git would exclude, including files containing secrets or generated code.
- Files: `src/TokenSqueeze/Indexing/DirectoryWalker.cs` (lines 24-35)
- Current mitigation: `SecretDetector` catches common secret file patterns.
- Recommendations: Walk up and down the directory tree collecting `.gitignore` rules, or use a library that implements full gitignore semantics (the `Ignore` NuGet package supports this but is called once with only the root file).

## Performance Bottlenecks

**Full Index Loaded Into Memory For Every Command:**
- Problem: `IndexStore.Load()` deserializes the entire `index.json` into memory including all symbols. For large codebases, this means every `find`, `outline`, or `extract` command pays the full deserialization cost.
- Files: `src/TokenSqueeze/Storage/IndexStore.cs` (lines 34-42), `src/TokenSqueeze/Models/CodeIndex.cs`
- Cause: Single monolithic JSON file with all file metadata and all symbol records. A 10K-file codebase could produce an index of tens of megabytes.
- Improvement path: Split storage into separate files (file index vs. symbol data), use streaming deserialization, or use SQLite. Short-term: the incremental indexing already mitigates reindex cost, but query commands still pay full load cost.

**ListCommand Loads Every Project's Full Index:**
- Problem: The `list` command iterates all project directories and calls `store.Load(name)` for each, deserializing every index just to extract summary metadata.
- Files: `src/TokenSqueeze/Commands/ListCommand.cs` (lines 21-24)
- Cause: No separate metadata/summary file; all data lives in `index.json`.
- Improvement path: Store a lightweight `metadata.json` alongside the full index, or extract summary data during `Save()`.

**Linear Symbol Search in FindCommand and ExtractCommand:**
- Problem: `FindCommand` iterates all symbols with `.Contains()` string matching. `ExtractCommand` uses `.FirstOrDefault()` linear scan by ID. Both are O(n) per query.
- Files: `src/TokenSqueeze/Commands/FindCommand.cs` (line 69), `src/TokenSqueeze/Commands/ExtractCommand.cs` (line 63)
- Cause: Symbols stored as a flat list. No indexing by ID or name.
- Improvement path: Build a `Dictionary<string, Symbol>` keyed by `Id` at load time. For find, consider a trie or inverted index for name-based search.

**DirectoryWalker Reads File Bytes Twice:**
- Problem: `DirectoryWalker.IsBinaryFile()` reads up to 8KB of each file to check for null bytes. Then `ProjectIndexer.Index()` reads the entire file again with `File.ReadAllBytes()`. Every file is opened and read from disk twice.
- Files: `src/TokenSqueeze/Indexing/DirectoryWalker.cs` (lines 91-107), `src/TokenSqueeze/Indexing/ProjectIndexer.cs` (line 46)
- Cause: Walker yields file paths, not file contents. Binary check happens in the walker, content reading in the indexer.
- Improvement path: Have the walker yield a struct containing the path and the already-read bytes, or defer the binary check to the indexer after reading the file once.

## Fragile Areas

**SymbolExtractor Language-Specific Branching:**
- Files: `src/TokenSqueeze/Parser/SymbolExtractor.cs` (lines 108-148, 289-399)
- Why fragile: `TryExtractConstant` and `BuildSignature` use `if/else` chains keyed on `spec.LanguageId` string values. Adding a new language requires adding branches in multiple methods. A typo in the language ID string causes silent failures (no constants extracted, wrong signature format).
- Safe modification: When adding a language, search for all occurrences of existing language IDs (`"Python"`, `"JavaScript"`, `"TypeScript"`, `"Tsx"`, `"C-Sharp"`, `"C"`, `"Cpp"`) to find every branch point.
- Test coverage: None. Every language-specific branch is untested.

**TreeSitter.DotNet Node Field Access:**
- Files: `src/TokenSqueeze/Parser/SymbolExtractor.cs` (lines 401-405)
- Why fragile: `TryGetField` catches `KeyNotFoundException` to handle missing fields. This means every field access is a try/catch in a hot loop during tree walking. If the TreeSitter.DotNet API changes how missing fields are reported, the entire extractor breaks silently.
- Safe modification: Check if newer TreeSitter.DotNet versions offer a `TryGetChild` or null-returning accessor to avoid exception-driven control flow.
- Test coverage: None.

## Scaling Limits

**Single JSON Index File:**
- Current capacity: Works well for small-to-medium projects (hundreds of files, thousands of symbols).
- Limit: For monorepos with 50K+ files, the single `index.json` will become multiple hundreds of MB. Serialization/deserialization becomes the bottleneck. Memory usage spikes on load.
- Scaling path: Migrate to SQLite or split into per-directory index shards. The `CodeIndex` model with `Dictionary<string, IndexedFile>` and `List<Symbol>` would need restructuring.

**`ResolveProjectName` Linear Probe:**
- Current capacity: Works fine with fewer than ~50 projects.
- Limit: If a user has many projects with colliding base names, the `while(true)` loop in `ResolveProjectName` calls `store.Load()` repeatedly (deserializing full indexes) to find an unused suffix.
- Scaling path: Check directory existence instead of loading full indexes. Or maintain a lightweight name registry.

## Dependencies at Risk

**TreeSitter.DotNet (v1.3.0):**
- Risk: Niche package with relatively small community. The `Node` API uses indexer-based field access that throws `KeyNotFoundException` -- non-standard and could change. Native library loading differs per platform and requires `IncludeNativeLibrariesForSelfExtract`.
- Impact: Core dependency. If it breaks or is abandoned, the entire parsing layer is dead.
- Migration plan: The `LanguageSpec` abstraction provides some insulation. A migration to tree-sitter WASM bindings or direct P/Invoke would require rewriting `LanguageRegistry` and `SymbolExtractor` but models and commands could survive.

**Ignore (v0.2.1):**
- Risk: Low version number (0.x) suggests pre-stable. Used for `.gitignore` pattern matching.
- Impact: If broken, files that should be excluded get indexed. Low blast radius since `SecretDetector` provides a second layer.
- Migration plan: Could be replaced with `Microsoft.Extensions.FileSystemGlobbing` or manual implementation.

## Missing Critical Features

**No File Watching / Auto-Reindex:**
- Problem: Index becomes stale as soon as files change. Users must manually re-run `index` command.
- Blocks: Real-time accuracy. The `stale` flag in `ExtractCommand` is a band-aid that only detects staleness at extraction time.

**No Support for Arrow Functions or Exported Default Functions (JS/TS):**
- Problem: `const handler = () => {}` and `export default function() {}` are not extracted as symbols. Only named `function_declaration` nodes are captured.
- Blocks: Incomplete symbol coverage for modern JavaScript/TypeScript codebases where arrow functions and anonymous exports are prevalent.

**No Go, Java, or Rust Support:**
- Problem: Only 6 languages supported (Python, JS, TS, C#, C, C++). Go, Java, and Rust are common targets for codebase indexing tools.
- Blocks: Adoption in polyglot codebases.

## Test Coverage Gaps

**Everything:**
- What's not tested: The entire codebase has zero automated tests. No unit tests, no integration tests, no end-to-end tests.
- Files: All files under `src/TokenSqueeze/`
- Risk: Any refactoring or feature addition could silently break existing functionality across all 7 language parsers, 7 CLI commands, storage, security, and indexing.
- Priority: High -- this is the single most impactful concern. Suggested priority order:
  1. `SymbolExtractor` per-language extraction (highest complexity, most likely to regress)
  2. `PathValidator` and `SecretDetector` (security-critical)
  3. `DirectoryWalker` filtering logic
  4. `FindCommand` scoring and `OutlineCommand` hierarchy (known bugs)
  5. `IndexStore` save/load round-trip

---

*Concerns audit: 2026-03-08*
