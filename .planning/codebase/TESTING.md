# Testing Patterns

**Analysis Date:** 2026-03-08

## Test Framework

**Runner:**
- None configured. No test project exists in the solution.

**Assertion Library:**
- Not applicable

**Run Commands:**
```bash
# No test commands available
# The solution file (`src/token-squeeze.sln`) contains only the main project
```

## Current State

**No automated tests exist.** The codebase has zero test projects, zero test files, and no test framework dependencies.

### What Exists Instead

**Manual test fixtures:**
- `tests/` directory contains sample source files for manual `parse-test` command validation
- Files: `sample.py`, `sample.ts`, `sample.js`, `sample.cs`, `sample.c`, `sample.cpp`, `sample.h`, `sample.tsx`
- These are input files for `token-squeeze parse-test <file>`, not automated tests

**Hidden debug command:**
- `ParseTestCommand` at `src/TokenSqueeze/Commands/ParseTestCommand.cs`
- Parses a single file and dumps extracted symbols as JSON
- Used for manual verification of parser output

**Startup smoke test:**
- `src/TokenSqueeze/Program.cs` lines 7-19: tree-sitter native library load check on every startup
- Catches `DllNotFoundException` to fail fast if native libs are missing

## Recommended Test Setup

If adding tests to this project, follow these conventions:

**Framework:** Use xUnit (standard for .NET 9 projects) with FluentAssertions.

**Project structure:**
```
src/
├── TokenSqueeze/              # Existing main project
└── TokenSqueeze.Tests/        # New test project
    ├── TokenSqueeze.Tests.csproj
    ├── Parser/
    │   └── SymbolExtractorTests.cs
    ├── Storage/
    │   └── IndexStoreTests.cs
    ├── Security/
    │   └── PathValidatorTests.cs
    ├── Indexing/
    │   └── DirectoryWalkerTests.cs
    └── Fixtures/
        ├── sample.py
        ├── sample.ts
        └── ...
```

**Test project .csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="7.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TokenSqueeze\TokenSqueeze.csproj" />
  </ItemGroup>
</Project>
```

**Visibility note:** Most classes are `internal sealed`. To test them, add to the main project's `.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="TokenSqueeze.Tests" />
</ItemGroup>
```

## Testable Units

**High-value targets (most logic, least I/O):**

| Component | File | What to Test |
|-----------|------|-------------|
| `SymbolExtractor` | `src/TokenSqueeze/Parser/SymbolExtractor.cs` | Symbol extraction per language, signature building, docstring extraction, constant detection |
| `LanguageRegistry` | `src/TokenSqueeze/Parser/LanguageRegistry.cs` | Extension mapping, parser creation, disposal |
| `PathValidator` | `src/TokenSqueeze/Security/PathValidator.cs` | Path traversal detection, symlink escape detection |
| `SecretDetector` | `src/TokenSqueeze/Security/SecretDetector.cs` | Secret file identification by name and extension |
| `DirectoryWalker` | `src/TokenSqueeze/Indexing/DirectoryWalker.cs` | Skip directories, gitignore, binary detection |
| `FindCommand` scoring | `src/TokenSqueeze/Commands/FindCommand.cs` | Search scoring logic, glob-to-regex conversion |

**Lower priority (mostly I/O wiring):**

| Component | File | Why Lower |
|-----------|------|-----------|
| `IndexStore` | `src/TokenSqueeze/Storage/IndexStore.cs` | Thin JSON serialization wrapper over filesystem |
| `ProjectIndexer` | `src/TokenSqueeze/Indexing/ProjectIndexer.cs` | Orchestrator -- testable only as integration test |
| CLI commands | `src/TokenSqueeze/Commands/*.cs` | Spectre.Console provides its own test harness, but these are thin wiring |

## Recommended Test Patterns

**Symbol extraction test (unit):**
```csharp
public class SymbolExtractorTests
{
    [Fact]
    public void ExtractSymbols_PythonFunction_ReturnsCorrectSignature()
    {
        using var registry = new LanguageRegistry();
        var extractor = new SymbolExtractor(registry);
        var spec = registry.GetSpecForExtension(".py")!;
        var source = "def greet(name: str) -> str:\n    pass"u8.ToArray();

        var symbols = extractor.ExtractSymbols("test.py", source, spec);

        symbols.Should().ContainSingle();
        symbols[0].Name.Should().Be("greet");
        symbols[0].Kind.Should().Be(SymbolKind.Function);
        symbols[0].Signature.Should().Be("def greet(name: str) -> str");
    }
}
```

**Security test (unit):**
```csharp
public class SecretDetectorTests
{
    [Theory]
    [InlineData(".env", true)]
    [InlineData("credentials.json", true)]
    [InlineData("app.config", false)]
    [InlineData("private.key", true)]
    public void IsSecretFile_DetectsCorrectly(string fileName, bool expected)
    {
        SecretDetector.IsSecretFile(fileName).Should().Be(expected);
    }
}
```

**Binary detection test (unit):**
```csharp
public class DirectoryWalkerTests
{
    [Fact]
    public void IsBinaryFile_TextFile_ReturnsFalse()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "hello world");

        DirectoryWalker.IsBinaryFile(tempFile).Should().BeFalse();

        File.Delete(tempFile);
    }
}
```

## Mocking

**Framework:** Not yet established. Recommended: NSubstitute or Moq.

**What to mock:**
- Filesystem access in `IndexStore` and `DirectoryWalker` tests
- Currently no interfaces exist, so mocking requires either:
  - Extracting interfaces (`IIndexStore`, `ILanguageRegistry`)
  - Using filesystem abstractions (`System.IO.Abstractions`)
  - Testing against temp directories (integration-style)

**What NOT to mock:**
- `SymbolExtractor` -- test with real tree-sitter parsing (the native lib must load)
- `LanguageRegistry` -- lightweight, manages native resources that are fast to create
- `PathValidator` / `SecretDetector` -- pure logic, no dependencies

## Coverage

**Requirements:** None enforced
**Current coverage:** 0%

## Test Types

**Unit Tests:**
- Focus on `Parser/`, `Security/`, and search scoring logic
- These contain the core algorithmic complexity

**Integration Tests:**
- Index a temp directory with known files, verify round-trip through `ProjectIndexer` -> `IndexStore` -> query commands
- Requires tree-sitter native libraries available at test runtime

**E2E Tests:**
- Not used. Could invoke the CLI binary and assert JSON stdout, but this is fragile and slow.

---

*Testing analysis: 2026-03-08*
