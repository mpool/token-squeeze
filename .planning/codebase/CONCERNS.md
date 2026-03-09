# Codebase Concerns

**Analysis Date:** 2026-03-09

## Tech Debt

**DEBT-01: Duplicated gitignore walking logic between DirectoryWalker and StalenessChecker:**
- Issue: `StalenessChecker.EnumerateNewFilesRecursive()` (lines 86-181 of `src/TokenSqueeze/Indexing/StalenessChecker.cs`) is a near-copy of `DirectoryWalker.WalkDirectory()` (lines 33-139 of `src/TokenSqueeze/Indexing/DirectoryWalker.cs`). Both implement gitignore stack parsing, secret detection, symlink checks, size limits, and skipped-directory filtering independently. The `GitignoreRule` record is even duplicated -- one in each file.
- Files: `src/TokenSqueeze/Indexing/DirectoryWalker.cs`, `src/TokenSqueeze/Indexing/StalenessChecker.cs`
- Impact: Any bug fix or new filter added to one location must be manually replicated in the other. The check ordering already differs slightly (secret check is step 2 in DirectoryWalker but step 3 in StalenessChecker). Future divergence is likely.
- Fix approach: Extract a shared `FileFilter` utility that both `DirectoryWalker` and `StalenessChecker` use, or have `StalenessChecker.DetectNewFiles` reuse `DirectoryWalker.Walk()` directly and diff against the manifest.

**DEBT-02: LanguageRegistry created per-thread during parallel indexing:**
- Issue: `ProjectIndexer.Index()` (line 52 of `src/TokenSqueeze/Indexing/ProjectIndexer.cs`) creates a new `LanguageRegistry` and `SymbolExtractor` per thread in `Parallel.ForEach`. Each `LanguageRegistry` allocates native tree-sitter `Language` and `Parser` handles. While these are disposed in `localFinally`, the per-thread allocation pattern is wasteful for thread pools that may recycle threads frequently.
- Files: `src/TokenSqueeze/Indexing/ProjectIndexer.cs`
- Impact: Memory pressure and native handle churn during large indexing operations. Not a leak, but inefficient.
- Fix approach: `TreeSitter.Parser` is not thread-safe, so per-thread parsers are necessary. However, `Language` objects could potentially be shared. Alternatively, use a parser pool with a fixed upper bound.

**DEBT-03: Legacy format migration still supported:**
- Issue: `LegacyMigration.cs`, `IndexStore.Load()` legacy fallback (lines 212-218), `IndexStore.IsLegacyFormat()`, `IndexStore.GetLegacySourcePath()`, and `StoragePaths.GetLegacyIndexPath()` all exist to support the old monolithic `index.json` format. Every query command calls `LegacyMigration.TryMigrateIfNeeded()` before proceeding.
- Files: `src/TokenSqueeze/Storage/LegacyMigration.cs`, `src/TokenSqueeze/Storage/IndexStore.cs`, `src/TokenSqueeze/Storage/StoragePaths.cs`
- Impact: Adds code paths that are increasingly unlikely to be exercised. Every command pays the cost of checking for legacy format.
- Fix approach: Set a deprecation version. After N releases, remove legacy support and have the tool print "please re-index" if it encounters the old format.

**DEBT-04: IncrementalReindexer duplicates hash/parse logic from ProjectIndexer:**
- Issue: `IncrementalReindexer.ReindexFiles()` (lines 63-178 of `src/TokenSqueeze/Indexing/IncrementalReindexer.cs`) reimplements file reading, hash computation, extension lookup, size checking, and symbol extraction independently from `ProjectIndexer.Index()`. The two code paths could diverge (e.g., `IncrementalReindexer` does not check for binary content or symlink escapes on stale/new files).
- Files: `src/TokenSqueeze/Indexing/IncrementalReindexer.cs`, `src/TokenSqueeze/Indexing/ProjectIndexer.cs`
- Impact: Security checks present in `DirectoryWalker` (binary detection, symlink escape) are not applied during incremental reindexing. A file that becomes a symlink after initial indexing would be reindexed without symlink validation.
- Fix approach: Extract a shared `FileProcessor` that encapsulates read + validate + hash + parse, used by both `ProjectIndexer` and `IncrementalReindexer`.

**DEBT-05: Inconsistent indentation in command try/catch blocks:**
- Issue: Several commands (`FindCommand`, `ExtractCommand`, `OutlineCommand`) have a `try` block that wraps the entire method body but with the inner code indented at the wrong level relative to the `try`. The code inside `try` is indented only one level instead of two.
- Files: `src/TokenSqueeze/Commands/FindCommand.cs`, `src/TokenSqueeze/Commands/ExtractCommand.cs`, `src/TokenSqueeze/Commands/OutlineCommand.cs`
- Impact: Cosmetic only, but makes the code harder to read and signals the try/catch was bolted on after the fact.
- Fix approach: Re-indent the inner blocks to match the try scope.

## Security Considerations

**SEC-01: IncrementalReindexer skips symlink and binary checks:**
- Risk: When `IncrementalReindexer` processes stale or new files (lines 63-178 of `src/TokenSqueeze/Indexing/IncrementalReindexer.cs`), it reads file bytes via `File.ReadAllBytes()` without checking `PathValidator.IsSymlinkEscape()` or `DirectoryWalker.IsBinaryContent()`. A file replaced with a symlink pointing outside the project root after initial indexing would be indexed, and its content stored in the index.
- Files: `src/TokenSqueeze/Indexing/IncrementalReindexer.cs`
- Current mitigation: `StalenessChecker.DetectNewFiles()` does check symlinks for new files. But stale files (existing files whose content changed) bypass this.
- Recommendations: Add `PathValidator.IsSymlinkEscape()` check before `File.ReadAllBytes()` in the stale-file and new-file loops of `IncrementalReindexer.ReindexFiles()`.

**SEC-02: SecretDetector is filename-only:**
- Risk: `SecretDetector.IsSecretFile()` (in `src/TokenSqueeze/Security/SecretDetector.cs`) only inspects filenames and extensions. A file named `config.py` containing `API_KEY = "sk-..."` would be indexed and its content stored as a symbol source fragment.
- Files: `src/TokenSqueeze/Security/SecretDetector.cs`
- Current mitigation: The tool only indexes supported language files (`.py`, `.ts`, `.js`, `.cs`, `.c`, `.cpp` etc.), so `.env` and `.pem` files are already excluded by extension filtering. The risk is limited to secrets embedded in source files.
- Recommendations: This is an inherent limitation of the tool's design. Document it. Content scanning would add significant complexity for marginal benefit since the tool stores raw source that already exists on disk.

**SEC-03: StoragePaths.TestRootOverride is a mutable static field:**
- Risk: `StoragePaths.TestRootOverride` (`src/TokenSqueeze/Storage/StoragePaths.cs` line 9) is a mutable internal static field that redirects all storage operations. While marked `EditorBrowsable(Never)`, it has no thread-safety guarantee. Concurrent tests setting different values could interfere.
- Files: `src/TokenSqueeze/Storage/StoragePaths.cs`
- Current mitigation: Field is `internal` and only used in tests. Production code never sets it.
- Recommendations: If test parallelism is ever needed, replace with an instance-based storage root (inject `StoragePaths` as a service rather than using static methods).

## Performance Bottlenecks

**PERF-01: Full search-index.json loaded into memory on every find/query:**
- Problem: `IndexStore.LoadAllSymbols()` (line 143 of `src/TokenSqueeze/Storage/IndexStore.cs`) deserializes the entire `search-index.json` into a `List<SearchSymbol>` on every `find` command. For large projects with tens of thousands of symbols, this means parsing a multi-megabyte JSON file per query.
- Files: `src/TokenSqueeze/Storage/IndexStore.cs`, `src/TokenSqueeze/Commands/FindCommand.cs`
- Cause: No in-memory caching or streaming search. Each invocation is a fresh process (CLI tool).
- Improvement path: This is largely inherent to the CLI-per-invocation model. For the current architecture, this is acceptable. If query latency becomes an issue, consider a persistent daemon or binary index format (e.g., SQLite).

**PERF-02: StalenessChecker reads entire files for hash comparison:**
- Problem: `StalenessChecker.DetectStaleFiles()` (line 44 of `src/TokenSqueeze/Indexing/StalenessChecker.cs`) calls `File.ReadAllBytes()` for every file whose mtime has changed, even before confirming the hash actually differs. The hash is computed from the full file contents.
- Files: `src/TokenSqueeze/Indexing/StalenessChecker.cs`
- Cause: Need full file bytes to compute SHA256 hash for comparison.
- Improvement path: The mtime fast-path already avoids most reads. Files whose mtime hasn't changed are skipped. The PERF-01 cap (stop after `MaxReindexPerQuery` stale files) also bounds this. Low priority.

**PERF-03: Query-time reindexing adds latency to every query command:**
- Problem: `QueryReindexer.EnsureFresh()` runs `StalenessChecker.DetectStaleFiles()` on every `find`, `outline`, and `extract` call. This walks the entire source directory to detect new files (`DetectNewFiles`) even when the user just wants to search.
- Files: `src/TokenSqueeze/Storage/QueryReindexer.cs`, `src/TokenSqueeze/Indexing/StalenessChecker.cs`
- Cause: Design choice to keep queries always-fresh without requiring explicit re-index.
- Improvement path: Add a `--no-refresh` flag to skip staleness checks when the user knows the index is current. Alternatively, cache a "last checked" timestamp and skip the full walk if checked recently (e.g., within 30 seconds).

## Fragile Areas

**FRAG-01: Symbol ID format is a string convention with no versioning:**
- Files: `src/TokenSqueeze/Models/Symbol.cs` (lines 47-62)
- Why fragile: Symbol IDs use the format `filePath::qualifiedName#kind`, parsed via string splitting on `::` and `#`. If a file path contains `::` or a qualified name contains `#`, parsing breaks. The `ParseId` method (line 50) uses `IndexOf("::")` and `LastIndexOf('#')` which could give wrong results for pathological inputs.
- Safe modification: Do not change the ID format without migrating existing indexes. Consider validating that file paths and names don't contain the delimiter characters.
- Test coverage: `src/TokenSqueeze.Tests/Models/SymbolParseIdTests.cs` covers basic cases but may not cover edge cases with delimiters in paths.

**FRAG-02: OutlineCommand file matching uses loose string comparison:**
- Files: `src/TokenSqueeze/Commands/OutlineCommand.cs` (lines 46-57)
- Why fragile: File matching in `OutlineCommand` uses three different string comparison strategies (exact match, ends-with suffix, trimmed leading slash) with `OrdinalIgnoreCase`. This means `foo/bar.cs` would match `baz/foo/bar.cs` via the `EndsWith` branch. If two files have the same suffix, only the first match is returned.
- Safe modification: Add a test for ambiguous file name matching. Consider requiring exact relative path matches and only falling back to suffix matching when no exact match is found.
- Test coverage: `src/TokenSqueeze.Tests/Commands/OutlineHierarchyTests.cs` exists but may not cover ambiguous suffix cases.

**FRAG-03: Manifest.Files dictionary is mutated in-place during staleness checking:**
- Files: `src/TokenSqueeze/Indexing/StalenessChecker.cs` (lines 51, 64-65), `src/TokenSqueeze/Indexing/IncrementalReindexer.cs`
- Why fragile: `StalenessChecker.DetectStaleFiles()` mutates the passed `Manifest.Files` dictionary to update mtime values (deferred via `mtimeUpdates`). `IncrementalReindexer.ReindexFiles()` also mutates `manifest.Files` to add/remove entries. Since `Manifest` is a `record` with `required init` properties, this mutation pattern is surprising.
- Safe modification: Be aware that any method receiving a `Manifest` may mutate its `Files` dictionary. The `Dictionary` inside the record is reference-typed and mutable despite the record semantics suggesting immutability.
- Test coverage: Covered by `src/TokenSqueeze.Tests/ReindexOnQueryTests.cs` and `src/TokenSqueeze.Tests/StalenessCheckerTests.cs`.

## Scaling Limits

**SCALE-01: MaxReindexPerQuery cap of 50 files:**
- Current capacity: Incremental reindex processes at most 50 files per query (`IncrementalReindexer.MaxReindexPerQuery`).
- Limit: If a project has more than 50 changed/new/deleted files since last index, a single query will only partially update the index. Subsequent queries will process more, but the index remains stale across multiple queries.
- Scaling path: This is a deliberate trade-off for query latency. The user can run `token-squeeze index <path>` for a full re-index. The cap value could be made configurable via a CLI flag or environment variable.

**SCALE-02: All symbols loaded into memory during indexing:**
- Current capacity: `ProjectIndexer.Index()` collects all symbols from all files into `allSymbols` list and `allFiles` dictionary before writing.
- Limit: For very large codebases (100k+ files), this could exhaust available memory.
- Scaling path: Stream symbols to storage per-file during the parallel loop instead of collecting everything first. The split-storage format already supports this -- write file fragments as they're processed.

## Dependencies at Risk

**DEP-01: TreeSitter.DotNet native interop:**
- Risk: `TreeSitter.DotNet` wraps native tree-sitter libraries. Version updates can change AST node types, breaking symbol extraction logic in `SymbolExtractor.cs`. The `LanguageRegistry` hard-codes expected node type strings (e.g., `"function_definition"`, `"class_declaration"`) that come from the native grammars.
- Impact: A tree-sitter grammar update could silently change extracted symbols or break extraction entirely for a language.
- Migration plan: Pin tree-sitter grammar versions. Add regression tests with fixture files for each language that verify exact symbol extraction output.

**DEP-02: Ignore library for gitignore parsing:**
- Risk: The `Ignore.Ignore` class (used in `DirectoryWalker` and `StalenessChecker`) is a third-party gitignore pattern matcher. Its behavior may not perfectly match git's gitignore semantics for all edge cases (negation patterns, escaped characters, etc.).
- Impact: Files could be incorrectly included or excluded from indexing.
- Migration plan: Low risk. The library is mature. Add integration tests with complex `.gitignore` patterns if issues are reported.

## Test Coverage Gaps

**GAP-01: No tests for concurrent/parallel indexing correctness:**
- What's not tested: `ProjectIndexer.Index()` uses `Parallel.ForEach` with `ConcurrentBag`. While `src/TokenSqueeze.Tests/Indexing/ParallelIndexingTests.cs` exists, verify it covers race conditions in the `ConcurrentBag` collection and deterministic ordering.
- Files: `src/TokenSqueeze/Indexing/ProjectIndexer.cs`
- Risk: Non-deterministic symbol ordering could cause different index output on different runs.
- Priority: Low -- the `OrderBy` on line 108 of `ProjectIndexer.cs` should ensure determinism, but worth verifying.

**GAP-02: No tests for AtomicWrite retry/failure paths:**
- What's not tested: `IndexStore.AtomicWrite()` (lines 326-351 of `src/TokenSqueeze/Storage/IndexStore.cs`) has a retry loop for `UnauthorizedAccessException` and a cleanup path for other exceptions. These paths are not tested.
- Files: `src/TokenSqueeze/Storage/IndexStore.cs`
- Risk: If the retry logic has a bug (e.g., infinite loop, temp file leak), it would only manifest under file contention on Windows.
- Priority: Medium -- this is production error-handling code.

**GAP-03: No tests for very large file handling:**
- What's not tested: The `MaxFileSize` (1MB) limit in `DirectoryWalker` is enforced but not tested with actual large files. The binary content detection (`IsBinaryContent`) checks only the first 8192 bytes.
- Files: `src/TokenSqueeze/Indexing/DirectoryWalker.cs`
- Risk: Low -- straightforward size check. But a file that is exactly at the boundary (1,048,576 bytes) should be verified.
- Priority: Low

**GAP-04: ExtractCommand batch mode with mixed valid/invalid IDs:**
- What's not tested: `ExtractCommand` handles batch extraction where some IDs may be valid and others invalid. Each invalid ID produces an inline error object in the results array. Verify edge cases: all-invalid batch, empty batch, duplicate IDs.
- Files: `src/TokenSqueeze/Commands/ExtractCommand.cs`
- Risk: Medium -- batch mode is a key integration point for the Claude Code plugin.
- Priority: Medium

**GAP-05: No integration test for full index-then-query-after-file-change cycle:**
- What's not tested: The end-to-end flow of: index a project, modify a source file, run a query (triggering incremental reindex), verify the query returns updated symbols.
- Files: `src/TokenSqueeze/Storage/QueryReindexer.cs`, `src/TokenSqueeze/Indexing/IncrementalReindexer.cs`
- Risk: The individual pieces are tested, but the full cycle through `QueryReindexer.EnsureFresh()` -> `StalenessChecker` -> `IncrementalReindexer` -> query is not verified end-to-end.
- Priority: Medium

---

*Concerns audit: 2026-03-09*
