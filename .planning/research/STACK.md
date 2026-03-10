# Technology Stack: Local Storage Simplification

**Project:** TokenSqueeze - Storage Migration (Global to Local)
**Researched:** 2026-03-09

## Recommended Stack

No new dependencies required. This is a storage layer refactor using existing .NET 9 APIs.

### Core (unchanged)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET 9 | net9.0 | Runtime | Already in use; `Directory.GetCurrentDirectory()` and `Path.GetFullPath()` are the only APIs needed for local storage |
| System.Text.Json | (framework) | Index serialization | Already configured in `JsonDefaults.cs`; no changes needed |
| System.IO | (framework) | Directory creation, path resolution | `Directory.GetCurrentDirectory()`, `Path.Combine()`, `Directory.CreateDirectory()` |

### No New Dependencies

The storage migration needs zero new NuGet packages. Everything is handled by `System.IO` which is already in use.

## Key Technical Decisions

### 1. Working Directory Resolution

**Use `Directory.GetCurrentDirectory()` -- not a CLI argument.**

| Approach | Verdict | Rationale |
|----------|---------|-----------|
| `Directory.GetCurrentDirectory()` | **Use this** | Returns the process working directory. When user runs `token-squeeze index` from their project root, this gives you the project root. Matches how `dotnet`, `cargo`, `git`, and every other project-scoped CLI works. |
| `Environment.CurrentDirectory` | Equivalent | Identical to `Directory.GetCurrentDirectory()` -- same underlying call. Use either. |
| `AppDomain.CurrentDomain.BaseDirectory` | Do NOT use | Returns the *executable's* location, not the user's working directory. Useless for a self-contained binary that lives in `~/.claude/plugins/`. |
| CLI `<path>` argument (current design) | Drop it | The `index <path>` command currently takes an explicit path. For local storage, the index command should still accept an optional path for indexing a non-cwd directory, but storage always goes to cwd's `.cache/`. |

**Confidence: HIGH** -- This is standard .NET behavior, documented by Microsoft, and matches every major CLI tool's pattern.

**Important caveat for MCP server:** The MCP server (`mcp-server.js`) spawns the CLI via `execFileSync`. The child process inherits the MCP server's working directory, which is typically set by Claude Code to the project root. Verify this assumption during implementation -- if the MCP server's cwd isn't the project root, you'll need to pass `cwd` explicitly in `execFileSync` options.

### 2. Cache Directory Naming: `.cache/` vs `.token-squeeze/`

**Use `.token-squeeze/` -- not `.cache/`.**

This contradicts the PROJECT.md's initial preference for `.cache/`. Here's why:

| Name | Pros | Cons |
|------|------|------|
| `.cache/` | Shorter; matches JS ecosystem convention (`node_modules/.cache/`, ESLint `--cache-location .cache/eslint/`) | **Collision risk**: other tools use `.cache/` too. Deleting `.cache/` nukes all tools' caches. Generic name provides no attribution. Not a convention in the .NET ecosystem at all. |
| `.token-squeeze/` | Zero collision risk. Self-documenting -- user sees the directory and knows what made it. Matches tool identity. Easy to gitignore specifically. | Slightly longer. |

The `.cache/` convention exists primarily in the JavaScript/Node.js ecosystem where tools share `node_modules/.cache/` as a namespace. In .NET and Rust ecosystems, tools use their own named directories: Cargo uses `target/`, .NET uses `obj/` and `bin/`, Rider uses `.idea/`. A `.cache/` directory at project root with no subdirectory namespacing is asking for collisions.

**If you insist on `.cache/`**, at minimum use `.cache/token-squeeze/` to namespace within it. But `.token-squeeze/` is cleaner for a standalone tool.

**Confidence: MEDIUM** -- This is a naming convention choice, not a technical constraint. The reasoning is sound but the PROJECT.md may have other factors driving the `.cache/` preference.

### 3. Directory Structure Inside Cache

```
<project-root>/
  .token-squeeze/           # or .cache/token-squeeze/
    manifest.json           # file list, hashes, timestamps
    search-index.json       # symbol search index
    files/                  # per-file symbol fragments
      src-main-py.json
      src-parser-extractor-cs.json
```

This mirrors the current per-project structure under `~/.token-squeeze/projects/<name>/` -- just relocated. The `StoragePaths` class changes from resolving against `~/.token-squeeze/projects/<name>` to resolving against `<cwd>/.token-squeeze/`.

**Confidence: HIGH** -- Direct migration of existing structure.

### 4. Gitignore Convention

The tool should NOT auto-modify `.gitignore`. Instead:

- The index skill's SKILL.md should instruct Claude to suggest adding `.token-squeeze/` to `.gitignore` after first index.
- The CLI can print a stderr hint: `Hint: add .token-squeeze/ to .gitignore`

**Gitignore pattern:**
```gitignore
# TokenSqueeze index cache
.token-squeeze/
```

A trailing `/` is important -- it matches only directories, not files. This is standard gitignore syntax per the git documentation.

**Confidence: HIGH** -- Standard gitignore behavior.

### 5. StoragePaths Refactor

Current `StoragePaths.cs` resolves everything from a global root:
```csharp
public static string RootDir => TestRootOverride ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".token-squeeze", "projects");
```

New approach:
```csharp
public static string RootDir => TestRootOverride
    ?? Path.Combine(Directory.GetCurrentDirectory(), ".token-squeeze");
```

All downstream methods (`GetManifestPath`, `GetFilesDir`, etc.) currently take a `projectName` parameter. These drop the parameter entirely -- there's only one project per `.token-squeeze/` directory:

```csharp
// Before
public static string GetManifestPath(string projectName)
    => Path.Combine(GetProjectDir(projectName), "manifest.json");

// After
public static string GetManifestPath()
    => Path.Combine(RootDir, "manifest.json");
```

**Confidence: HIGH** -- Direct simplification of existing code.

### 6. MCP Server Path Propagation

The MCP server needs to know the project root to set cwd when spawning the CLI. Two options:

| Approach | Verdict |
|----------|---------|
| Rely on inherited cwd from Claude Code | **Try this first** -- Claude Code typically sets cwd to the project root when launching MCP servers |
| Pass cwd via MCP initialize params or environment variable | Fallback if inherited cwd is wrong |

In `mcp-server.js`, the `runCli` function should explicitly pass `cwd`:
```javascript
const stdout = execFileSync(BINARY, args, {
    encoding: "utf-8",
    timeout: 30000,
    cwd: process.cwd(),  // explicit, even if redundant
    stdio: ["pipe", "pipe", "pipe"],
});
```

**Confidence: MEDIUM** -- Need to verify Claude Code's cwd behavior for MCP servers during implementation.

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Cache dir name | `.token-squeeze/` | `.cache/` | Collision risk; not a .NET convention; generic |
| Cache dir name | `.token-squeeze/` | `.cache/token-squeeze/` | Extra nesting for no benefit vs `.token-squeeze/` |
| Path resolution | `Directory.GetCurrentDirectory()` | Explicit `--root` flag | Adds friction; every other CLI tool uses cwd |
| Storage format | Keep JSON fragments | SQLite | Overkill; adds a dependency; current format works fine |
| Auto-gitignore | Skill-based suggestion | Auto-modify `.gitignore` | Modifying user files without explicit consent is hostile |

## What Does NOT Change

- **NuGet dependencies**: Zero additions or removals
- **JSON serialization config**: `JsonDefaults.cs` stays identical
- **File fragment format**: Same `StoragePaths.PathToStorageKey()` logic
- **Atomic writes**: Same temp-file-then-rename pattern
- **Security**: Path traversal and symlink checks still apply (now relative to cwd instead of global root)
- **Build/publish**: Same `dotnet publish` commands and targets

## Installation

No new packages. The only "installation" change is removing the `catalog.json` global file and the `~/.token-squeeze/` directory structure.

## Sources

- [Microsoft: Directory.GetCurrentDirectory()](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.getcurrentdirectory?view=net-10.0)
- [Microsoft: File path formats on Windows](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats)
- [git-scm: gitignore documentation](https://git-scm.com/docs/gitignore)
- [ESLint cache location discussion](https://github.com/eslint/eslint/issues/13897) -- illustrates `.cache/` collision concerns
- [Prettier cache location](https://prettier.io/docs/cli) -- shows `node_modules/.cache/prettier/` namespacing pattern
- [Cargo build cache](https://doc.rust-lang.org/cargo/reference/build-cache.html) -- Rust uses `target/`, not `.cache/`
- [Thomas Queste: Caching in Jest, Prettier, ESLint](https://www.tomsquest.com/blog/2024/06/cache-jest-eslint-prettier-typescript-ci/) -- `.cache/` subdirectory convention in JS ecosystem
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/) -- `$XDG_CACHE_HOME` is for user-level cache, not project-level

---

*Stack analysis: 2026-03-09*
