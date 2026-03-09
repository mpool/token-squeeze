# Testing Patterns

**Analysis Date:** 2026-03-08

## Test Framework

**Runner:**
- xUnit 2.9.2
- Config: implicit (no xunit.runner.json detected; configured via `.csproj` package references)
- Test SDK: Microsoft.NET.Test.Sdk 17.12.0

**Assertion Library:**
- xUnit built-in assertions (`Assert.*`)
- No FluentAssertions or similar third-party assertion library

**Coverage:**
- coverlet.collector 6.0.2 (installed but no enforced thresholds)

**Run Commands:**
```bash
dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj       # Run all tests
dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --filter "FullyQualifiedName~SmokeTest"  # Filter
dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --collect:"XPlat Code Coverage"          # Coverage
```

## Test File Organization

**Location:**
- Separate project: `src/TokenSqueeze.Tests/`
- Mirror structure: test directories match source directories (`Commands/`, `Storage/`, `Parser/`, `Security/`, `Indexing/`, `Models/`)
- Some root-level test files exist for cross-cutting concerns: `SmokeTest.cs`, `RobustnessTests.cs`, `DisposalTests.cs`, `StalenessCheckerTests.cs`, `ReindexOnQueryTests.cs`

**Naming:**
- Test files: `{Feature}Tests.cs` (e.g., `SymbolExtractorTests.cs`, `PathValidatorTests.cs`)
- More specific test files: `{Feature}{Aspect}Tests.cs` (e.g., `FindCommandGlobTests.cs`, `IndexStoreDeleteTests.cs`, `DirectoryWalkerFilterTests.cs`)

**Structure:**
```
src/TokenSqueeze.Tests/
├── Commands/
│   ├── CliIntegrationTests.cs          # End-to-end CLI tests
│   ├── ExtractCommandTests.cs          # Extract command behavior
│   ├── ExtractCommandSecurityTests.cs  # Extract security edge cases
│   ├── FindCommandTests.cs             # Find scoring and filtering
│   ├── FindCommandGlobTests.cs         # Glob-to-regex conversion
│   ├── GlobToRegexTests.cs             # GlobToRegex unit tests
│   ├── IndexCommandTests.cs            # Index command behavior
│   ├── ListCommandProjectionTests.cs   # List output shape
│   └── OutlineHierarchyTests.cs        # Outline parent/child nesting
├── Fixtures/                           # Sample source files for parser tests
│   ├── sample.py, sample.js, sample.ts, sample.tsx
│   ├── sample.cs, sample-advanced.cs
│   ├── sample.c, sample.cpp, sample.h
├── Helpers/
│   ├── CliTestHarness.cs               # CLI integration test harness
│   └── TestIndexBuilder.cs             # Test data factory
├── Indexing/
│   ├── DirectoryWalkerFilterTests.cs   # File filtering logic
│   ├── DirectoryWalkerGitignoreTests.cs # Gitignore integration
│   └── ParallelIndexingTests.cs        # Parallel indexing behavior
├── Models/
│   ├── ManifestTests.cs
│   └── SymbolParseIdTests.cs           # Symbol.ParseId edge cases
├── Parser/
│   ├── SymbolExtractorTests.cs         # Per-language extraction
│   ├── SymbolExtractorEdgeCaseTests.cs # Edge cases
│   ├── CSharpExtractionTests.cs        # C#-specific parsing
│   └── LanguageRegistryTests.cs        # Language loading
├── Security/
│   ├── PathValidatorTests.cs           # Path traversal validation
│   ├── ProjectNameSanitizationTests.cs # Name sanitization
│   ├── SecretDetectorTests.cs          # Secret file detection
│   └── DirectoryWalkerSymlinkTests.cs  # Symlink escape
├── Storage/
│   ├── SplitStorageTests.cs            # Split format save/load
│   ├── AtomicWriteTests.cs             # Concurrent write safety
│   ├── IndexStoreDeleteTests.cs        # Delete validation
│   ├── IndexStoreSaveTests.cs          # Save behavior
│   ├── IndexStoreValidationTests.cs    # Validation edge cases
│   ├── LegacyMigrationTests.cs         # v1->v2 migration
│   ├── SearchIndexTests.cs             # Search index format
│   ├── SelectiveLoadTests.cs           # Selective file loading
│   └── StoragePathsTests.cs            # Path computation
├── SmokeTest.cs                        # Basic wiring smoke tests
├── RobustnessTests.cs                  # File size limits, depth limits, error isolation
├── DisposalTests.cs                    # Resource disposal verification
├── StalenessCheckerTests.cs            # Staleness detection
├── ReindexOnQueryTests.cs              # Query-time reindex
├── LanguageSpecValidationTests.cs      # Language spec consistency
└── OutlineHierarchyTests.cs            # Outline nesting (duplicate location)
```

## Test Structure

**Suite Organization:**
```csharp
// Standard pattern: sealed class, IDisposable for cleanup
public sealed class PathValidatorTests : IDisposable
{
    private readonly string _tempRoot;

    public PathValidatorTests()
    {
        // Constructor = setup: create temp directory
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ts-pathval-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void MethodUnderTest_Scenario_ExpectedResult()
    {
        // Arrange
        var maliciousPath = Path.Combine(_tempRoot, "../../../etc/passwd");

        // Act & Assert
        Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateWithinRoot(maliciousPath, _tempRoot));
    }

    public void Dispose()
    {
        // Cleanup temp directories
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
```

**Test Method Naming:**
- Pattern: `MethodOrFeature_Scenario_ExpectedBehavior`
- Examples: `ValidateWithinRoot_RejectsTraversalPaths`, `Save_CreatesManifestWithFormatVersion2`, `ExactMatchScore_Returns325`
- Some use shorter descriptive names: `Python_ExtractsExpectedSymbols`, `ListEmpty_ReturnsEmptyProjectsArray`

**Attributes Used:**
- `[Fact]` for single-case tests (majority)
- `[Theory]` with `[InlineData]` for parameterized tests (security tests, parser tests)
- `[Collection("CLI")]` for tests that share Console.Out (prevents parallel conflicts)

## Test Isolation

**Storage Isolation:**
- `StoragePaths.TestRootOverride` static field redirects storage to temp directories
- Every test class that touches storage sets this in constructor and clears in `Dispose()`
- Pattern:

```csharp
public SomeTests()
{
    _tempDir = Path.Combine(Path.GetTempPath(), "ts-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_tempDir);
    StoragePaths.TestRootOverride = _tempDir;
}

public void Dispose()
{
    StoragePaths.TestRootOverride = null;
    if (Directory.Exists(_tempDir))
        Directory.Delete(_tempDir, recursive: true);
}
```

**Console Output Isolation:**
- `CliTestHarness` captures `Console.Out` via `Console.SetOut(new StringWriter())` and restores in `finally`
- Tests using `CliTestHarness` must be in `[Collection("CLI")]` to prevent parallel Console.Out conflicts

## CLI Integration Test Harness

**Location:** `src/TokenSqueeze.Tests/Helpers/CliTestHarness.cs`

**Purpose:** Full CLI integration testing -- boots the entire Spectre.Console.Cli command pipeline with real DI.

```csharp
// Usage pattern
[Collection("CLI")]
public sealed class MyCommandTests : IDisposable
{
    private readonly CliTestHarness _harness = new();

    [Fact]
    public void SomeCommand_SomeScenario()
    {
        // Create source files
        var sourceDir = _harness.CreateSourceDir("project-name", new Dictionary<string, string>
        {
            ["hello.py"] = "def greet(name):\n    pass\n"
        });

        // Run CLI command and capture JSON output
        var (exitCode, output) = _harness.Run("index", sourceDir);
        Assert.Equal(0, exitCode);

        // Parse and assert JSON
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Equal("project-name", root.GetProperty("projectName").GetString());
    }

    public void Dispose() => _harness.Dispose();
}
```

**Key methods:**
- `Run(params string[] args)` -> `(int exitCode, string output)`: Runs CLI command, returns exit code and stdout
- `CreateSourceDir(string name, Dictionary<string, string> files)` -> `string`: Creates temp directory with files, returns path

## Test Data Factory

**Location:** `src/TokenSqueeze.Tests/Helpers/TestIndexBuilder.cs`

**Purpose:** Builds `CodeIndex` and `Symbol` instances for unit tests without running the full indexer.

```csharp
// Create a test index with symbols
var index = TestIndexBuilder.Create("myproject", "/tmp/myproject",
    TestIndexBuilder.MakeSymbol("greet", file: "hello.py", kind: SymbolKind.Function),
    TestIndexBuilder.MakeSymbol("Calculator", file: "calc.py", kind: SymbolKind.Class)
);

// Create a single test symbol with defaults
var sym = TestIndexBuilder.MakeSymbol("foo");
// Defaults: file="test.py", kind=Function, language="Python", signature="foo()"
```

## Fixtures

**Location:** `src/TokenSqueeze.Tests/Fixtures/`

**Purpose:** Real source files in each supported language for parser extraction tests.

**Files:**
- `sample.py`, `sample.js`, `sample.ts`, `sample.tsx` -- web/scripting languages
- `sample.cs`, `sample-advanced.cs` -- C# samples (basic and advanced features)
- `sample.c`, `sample.cpp`, `sample.h` -- C/C++ samples

**Access pattern:**
```csharp
private static readonly string FixtureDir = Path.Combine(
    Path.GetDirectoryName(typeof(SymbolExtractorTests).Assembly.Location)!,
    "..", "..", "..", "Fixtures");

private List<Symbol> ExtractFromFixture(string filename)
{
    var filePath = Path.Combine(FixtureDir, filename);
    var sourceBytes = File.ReadAllBytes(filePath);
    var ext = Path.GetExtension(filename);
    var spec = _registry.GetSpecForExtension(ext)!;
    return _extractor.ExtractSymbols(filePath, sourceBytes, spec);
}
```

## Mocking

**Framework:** None. No mocking library is used.

**Approach:** The codebase uses real implementations throughout tests:
- Real `IndexStore` with `StoragePaths.TestRootOverride` for storage isolation
- Real `LanguageRegistry` and `SymbolExtractor` for parser tests
- Real `CliTestHarness` boots full DI container for integration tests
- Real filesystem with temp directories for file-based tests

**What to mock (if needed):** Nothing currently. The architecture uses concrete classes without interfaces. To add mocking, interfaces would need to be extracted for `IndexStore`, `LanguageRegistry`, etc.

**What NOT to mock:**
- `LanguageRegistry` -- wraps native tree-sitter handles, must be real
- `SymbolExtractor` -- core logic under test
- Filesystem operations -- tests use real temp directories

## Assertion Patterns

**JSON Output Assertions** (most common pattern in this codebase):
```csharp
var (exitCode, output) = _harness.Run("find", projectName, "greet");
Assert.Equal(0, exitCode);
using var doc = JsonDocument.Parse(output);
var results = doc.RootElement.GetProperty("results");
Assert.True(results.GetArrayLength() >= 1);
Assert.Equal("greet", results[0].GetProperty("name").GetString());
```

**Symbol Extraction Assertions:**
```csharp
// Custom assertion helpers in SymbolExtractorTests
private static void AssertContainsSymbol(List<Symbol> symbols, string name, SymbolKind kind)
{
    Assert.Contains(symbols, s => s.Name == name && s.Kind == kind);
}

private static void AssertDoesNotContainKind(List<Symbol> symbols, string name, SymbolKind kind)
{
    Assert.DoesNotContain(symbols, s => s.Name == name && s.Kind == kind);
}
```

**Exception Assertions:**
```csharp
Assert.Throws<SecurityException>(() =>
    PathValidator.ValidateWithinRoot(maliciousPath, _tempRoot));

Assert.Throws<ObjectDisposedException>(() =>
    registry.GetOrCreateParser("Python"));
```

**Structural Verification** (verifying source code properties):
```csharp
// Some tests read source code to verify structural invariants
var source = File.ReadAllText(sourcePath);
Assert.True(afterRun.Contains("Dispose", StringComparison.Ordinal),
    "Program.cs must call Dispose after app.Run");
```

## Test Types

**Smoke Tests:**
- `src/TokenSqueeze.Tests/SmokeTest.cs` -- verifies basic wiring (types resolve, native libs load)
- 2 tests, runs fast

**Unit Tests:**
- Parser tests (`SymbolExtractorTests.cs`, `CSharpExtractionTests.cs`, `SymbolExtractorEdgeCaseTests.cs`)
- Model tests (`SymbolParseIdTests.cs`, `ManifestTests.cs`)
- Security tests (`PathValidatorTests.cs`, `SecretDetectorTests.cs`, `ProjectNameSanitizationTests.cs`)
- Storage path tests (`StoragePathsTests.cs`, `GlobToRegexTests.cs`)
- Disposal tests (`DisposalTests.cs`)

**Integration Tests:**
- CLI integration (`CliIntegrationTests.cs`) -- full command pipeline via `CliTestHarness`
- Command-specific integration (`FindCommandTests.cs`, `ExtractCommandTests.cs`, `IndexCommandTests.cs`)
- Storage integration (`SplitStorageTests.cs`, `LegacyMigrationTests.cs`, `SearchIndexTests.cs`)
- Indexing integration (`DirectoryWalkerFilterTests.cs`, `DirectoryWalkerGitignoreTests.cs`, `ParallelIndexingTests.cs`)
- Robustness (`RobustnessTests.cs` -- file size limits, depth limits, error isolation)
- Reindex on query (`ReindexOnQueryTests.cs`, `StalenessCheckerTests.cs`)

**E2E Tests:**
- Not present as a separate category; `CliIntegrationTests.cs` is the closest equivalent (index -> find -> extract -> purge lifecycle)

## Coverage

**Requirements:** None enforced. coverlet.collector is installed but no minimum threshold is configured.

**View Coverage:**
```bash
dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --collect:"XPlat Code Coverage"
# Results in TestResults/*/coverage.cobertura.xml
```

## Test Collection Serialization

**The `[Collection("CLI")]` pattern is critical:**
- Tests that use `CliTestHarness` or modify `StoragePaths.TestRootOverride` MUST be in `[Collection("CLI")]`
- This prevents xUnit from running them in parallel (they share global state: `Console.Out` and `StoragePaths.TestRootOverride`)
- Defined in `src/TokenSqueeze.Tests/Helpers/CliTestHarness.cs`:

```csharp
[CollectionDefinition("CLI")]
public sealed class CliCollection : ICollectionFixture<CliTestHarness> { }
```

**Tests NOT in `[Collection("CLI")]`** run in parallel and must not touch global state:
- `SmokeTest.cs` -- only reads, no writes
- `SymbolExtractorTests.cs` -- uses own `LanguageRegistry` instance
- `SecretDetectorTests.cs` -- pure static method tests
- `PathValidatorTests.cs` -- uses own temp directory

## Adding New Tests

**New unit test for an existing feature:**
1. Add test methods to the existing `*Tests.cs` file in the matching directory
2. Use `[Fact]` for single cases, `[Theory]` + `[InlineData]` for parameterized
3. Follow naming: `MethodOrFeature_Scenario_ExpectedBehavior`

**New test class for a new feature:**
1. Create `src/TokenSqueeze.Tests/{Directory}/{Feature}Tests.cs` mirroring the source directory
2. If the test touches storage or Console.Out, add `[Collection("CLI")]` and implement `IDisposable`
3. Use `StoragePaths.TestRootOverride` for storage isolation
4. If testing CLI commands, use `CliTestHarness` from `Helpers/`

**New parser fixture:**
1. Add sample source file to `src/TokenSqueeze.Tests/Fixtures/sample.{ext}`
2. Write extraction test in `src/TokenSqueeze.Tests/Parser/` using `ExtractFromFixture()`

---

*Testing analysis: 2026-03-08*
