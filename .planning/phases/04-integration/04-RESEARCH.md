# Phase 4: Integration - Research

**Researched:** 2026-03-09
**Domain:** MCP server, plugin skills/hooks/agents, documentation, test verification
**Confidence:** HIGH

## Summary

Phase 4 is a cleanup and alignment phase -- all the hard architectural work (storage, models, CLI) was completed in Phases 1-3. What remains is updating the MCP server JavaScript (`mcp-server.js`), deleting dead plugin files (hooks, scripts, purge skill), updating agent definitions and remaining skills, updating docs (README.md, CLAUDE.md), and verifying all 265 tests still pass.

The riskiest item is MCP-06 (CWD propagation). Research confirms there is **no standardized MCP protocol mechanism** for passing working directory. However, the plugin's `.mcp.json` spawns the server via `node ${CLAUDE_PLUGIN_ROOT}/mcp-server.js` as a stdio subprocess, and Node.js `child_process` inherits cwd from the parent by default. Claude Code launches from the project root, so the MCP server's `process.cwd()` should be the project root. The `runCli` function in `mcp-server.js` does not specify a `cwd` option to `execFileSync`, so the CLI binary also inherits this cwd. Since CLI commands use `Directory.GetCurrentDirectory()` + `.cache/` to find the index, this should work without changes. An explicit `cwd` option could be added to `execFileSync` as a safety measure.

**Primary recommendation:** This is a straightforward file-editing phase. The MCP server changes are mechanical (remove tool, drop parameters, update args arrays, add manifest check). The plugin cleanup is pure deletion. The main verification is ensuring the MCP server's tool schemas, CLI args, and error handling all align with the Phase 3 CLI signatures.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Remove `list_projects` tool entirely from TOOLS array and handleToolCall switch
- Drop `project` parameter from `read_file_outline`, `read_symbol_source`, `search_symbols` input schemas
- CLI args change: `["outline", args.file]` not `["outline", args.project, args.file]`, same pattern for extract/find
- Add `.cache/manifest.json` existence check before tool execution -- return clear error: "No index found. Run /token-squeeze:index"
- MCP server inherits cwd from Claude Code's MCP spawn -- no special cwd handling needed
- Delete `hooks/hooks.json` (SessionStart hook)
- Delete `scripts/auto-index.sh`
- Delete `scripts/auto-index.ps1` if it exists (it does NOT exist -- only `.sh` exists)
- The hooks/ and scripts/ directories can be deleted entirely if empty after
- Delete `skills/purge/` directory entirely
- Update `skills/index/SKILL.md`: remove "Project name assigned" from output section, emphasize "run this first" positioning, add `.gitignore` offer
- Update `skills/explore/SKILL.md`: remove `list_projects` validation step, replace with `.cache/` existence check or let MCP error propagate
- Update `skills/savings/SKILL.md`: review for accuracy (no project name references to remove)
- `CLAUDE.md`: verify CLI commands table is current (Phase 3 already updated it)
- `README.md`: update to emphasize index-first workflow, remove project naming, update example output
- Verify all 265 tests pass against final codebase state
- Tests use `.cache/` paths, not global `~/.token-squeeze/` paths

### Claude's Discretion
- Whether to bump MCP server version number (currently 2.0.1)
- Exact wording of the "no index found" MCP error message (as long as it mentions `/token-squeeze:index`)
- Whether to simplify the explore skill's multi-agent approach or keep it as-is minus the list_projects call

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MCP-01 | `read_file_outline` tool drops `project` parameter | Remove from TOOLS inputSchema and handleToolCall args |
| MCP-02 | `read_symbol_source` tool drops `project` parameter | Remove from TOOLS inputSchema and handleToolCall args |
| MCP-03 | `search_symbols` tool drops `project` parameter | Remove from TOOLS inputSchema and handleToolCall args |
| MCP-04 | `list_projects` tool removed entirely | Delete from TOOLS array and handleToolCall switch case |
| MCP-05 | MCP server checks `.cache/manifest.json` before proceeding | Add fs.existsSync check in handleToolCall before CLI dispatch |
| MCP-06 | MCP server passes correct cwd to CLI subprocess | Inherits from Claude Code spawn; optionally add explicit `cwd: process.cwd()` to execFileSync |
| PLUG-01 | `hooks/hooks.json` deleted | File exists, delete it and the hooks/ directory |
| PLUG-02 | `scripts/auto-index.sh` and `.ps1` deleted | `.sh` exists, `.ps1` does not; delete file and scripts/ directory |
| PLUG-03 | Index skill updated | Remove "Project name assigned" output, add index-first emphasis and .gitignore offer |
| PLUG-04 | Explore skill updated | Remove `list_projects` from Step 1, update validation approach |
| PLUG-05 | Savings skill updated | Review -- currently has no project name references (already clean) |
| PLUG-06 | Purge skill deleted | Delete `skills/purge/` directory entirely |
| DOC-01 | CLAUDE.md updated | Verify Phase 3 already updated CLI table; remove any remaining project-name refs |
| DOC-02 | README.md updated | Rewrite MCP tools table, remove list_projects/purge, update storage description |
| TEST-01 | Tests updated for new storage model | All 265 tests already pass with `.cache/` paths; verify no old refs remain |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Node.js built-ins | N/A | `child_process`, `path`, `os`, `fs` | MCP server is zero-dependency by design |

### Supporting
No additional libraries needed. All changes are edits to existing files or file deletions.

## Architecture Patterns

### Current MCP Server Structure (mcp-server.js)
```
mcp-server.js (~330 lines)
├── Platform binary detection (getBinaryPath)
├── runCli(args) -- execFileSync wrapper
├── TOOLS array -- tool definitions with inputSchema
├── handleToolCall(name, args) -- switch dispatch to CLI
├── MCP JSON-RPC protocol handler
└── stdio transport (auto-detect framing)
```

### Pattern: Tool Schema to CLI Arg Mapping
Each MCP tool maps directly to a CLI command. The pattern is:
1. TOOLS array defines the tool name, description, and inputSchema
2. handleToolCall builds a CLI args array from the tool arguments
3. runCli executes the binary with those args
4. Result passes through as text

**Current (before changes):**
```javascript
// outline: ["outline", args.project, args.file]
// extract: ["extract", args.project, ...ids]
// find:    ["find", args.project, args.query, ...]
```

**Target (after changes):**
```javascript
// outline: ["outline", args.file]
// extract: ["extract", ...ids]
// find:    ["find", args.query, ...]
```

### Pattern: Manifest Check Guard
Add a guard before tool dispatch to check that `.cache/manifest.json` exists:
```javascript
const fs = require("fs");
const manifestPath = path.join(process.cwd(), ".cache", "manifest.json");

// In handleToolCall, before the switch:
if (name !== "some_exempt_tool") {
  if (!fs.existsSync(manifestPath)) {
    return errorResult("No index found. Run /token-squeeze:index");
  }
}
```

### Anti-Patterns to Avoid
- **Don't add a cwd parameter to MCP tools:** The whole point of this refactor is eliminating project identity. The cwd IS the project.
- **Don't keep list_projects as a no-op:** Remove it entirely so calling it returns method-not-found from the MCP protocol layer (the `default` case in handleToolCall already returns "Unknown tool").
- **Don't add `require('fs')` redundantly:** `fs` is already available via Node.js built-ins, just add the require at the top of the file.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Manifest existence check | Custom file parsing | `fs.existsSync(path)` | Just need existence, not content |
| CWD resolution | Custom path logic | `process.cwd()` | Node.js already provides this |

## Common Pitfalls

### Pitfall 1: Forgetting to update agent files
**What goes wrong:** The `plugin/agents/` directory contains `codebase-explorer.md` and `symbol-locator.md` which both reference `list_projects` in their tools list and instructions.
**Why it happens:** Agents are easy to overlook -- they weren't mentioned in CONTEXT.md decisions but clearly need updating.
**How to avoid:** Update both agent files: remove `list_projects` from their `tools:` frontmatter and instruction steps.
**Warning signs:** After deployment, agents try to call a tool that no longer exists.

### Pitfall 2: MCP extract command args format
**What goes wrong:** The extract handler has special logic for single vs batch IDs. After removing `args.project`, the args array construction changes.
**Current code:**
```javascript
const cliArgs = ["extract", args.project];
if (ids.length === 1) cliArgs.push(ids[0]);
else for (const id of ids) cliArgs.push("--batch", id);
```
**Target code:**
```javascript
const cliArgs = ["extract"];
if (ids.length === 1) cliArgs.push(ids[0]);
else for (const id of ids) cliArgs.push("--batch", id);
```
**How to avoid:** Carefully remove only `args.project` from the initial array, keep the batch logic intact.

### Pitfall 3: README.md tool names are wrong
**What goes wrong:** README.md currently lists tools as `token_squeeze_list`, `token_squeeze_outline`, etc. (underscore-separated). The actual MCP tool names are `list_projects`, `read_file_outline`, `read_symbol_source`, `search_symbols`.
**How to avoid:** Use the correct tool names from the TOOLS array when updating README.

### Pitfall 4: plugin.json version vs mcp-server.js version
**What goes wrong:** Two places define version: `plugin/.claude-plugin/plugin.json` (version: "2.0.1") and `mcp-server.js` SERVER_INFO (version: "2.0.1"). If bumping, both need updating.
**How to avoid:** If version is bumped, update both files.

### Pitfall 5: fs module not yet required
**What goes wrong:** `mcp-server.js` currently only requires `child_process`, `path`, and `os`. Adding the manifest check requires `fs`.
**How to avoid:** Add `const fs = require("fs");` at the top alongside existing requires.

## Code Examples

### MCP Server: Updated TOOLS array (outline example)
```javascript
// Source: current mcp-server.js lines 59-78, modified
{
  name: "read_file_outline",
  description:
    "Get all symbols (functions, classes, methods, types) in a file with their signatures. Pass file path (e.g. 'src/main.py').",
  inputSchema: {
    type: "object",
    properties: {
      file: {
        type: "string",
        description:
          "File path relative to project root (e.g. 'src/Parser/SymbolExtractor.cs')",
      },
    },
    required: ["file"],
  },
},
```

### MCP Server: Manifest guard
```javascript
const fs = require("fs");

function handleToolCall(name, args) {
  // Check for index before any tool execution
  const manifestPath = path.join(process.cwd(), ".cache", "manifest.json");
  if (!fs.existsSync(manifestPath)) {
    return errorResult("No index found. Run /token-squeeze:index");
  }

  switch (name) {
    // ... (no list_projects case)
  }
}
```

### Updated index skill output section
```markdown
## Output

The command outputs JSON. Present a summary to the user showing:
- Number of files indexed
- Languages detected
- Cache location

Then offer: "Want me to add `.cache/` to your `.gitignore`?"
```

### Updated explore skill Step 1
```markdown
## Step 1 -- Validate Index

Use `search_symbols` with a broad query (e.g. the user's topic) to confirm an index exists. If the MCP server returns "No index found", tell the user to run `/token-squeeze:index` first and stop.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Global `~/.token-squeeze/` storage | Local `.cache/` per project | This milestone (Phases 1-3) | MCP server args, tool schemas, plugin hooks all change |
| Project name identity | Directory-is-identity (cwd) | This milestone (Phases 1-3) | `project` param removed from all tools |
| SessionStart auto-index hook | Manual `/token-squeeze:index` | This phase | Hooks and scripts deleted |
| `list` and `purge` CLI commands | Deleted | Phase 3 | `list_projects` MCP tool and purge skill deleted |

## Files to Modify/Delete

### Delete
| File | Reason |
|------|--------|
| `plugin/hooks/hooks.json` | SessionStart hook removed |
| `plugin/scripts/auto-index.sh` | Auto-index script removed |
| `plugin/hooks/` (directory) | Empty after deletion |
| `plugin/scripts/` (directory) | Empty after deletion |
| `plugin/skills/purge/SKILL.md` | Purge command no longer exists |
| `plugin/skills/purge/` (directory) | Empty after deletion |

### Modify
| File | Changes |
|------|---------|
| `plugin/mcp-server.js` | Remove list_projects, drop project params, add manifest guard, add fs require |
| `plugin/agents/codebase-explorer.md` | Remove list_projects from tools and instructions |
| `plugin/agents/symbol-locator.md` | Remove list_projects from tools and instructions |
| `plugin/skills/index/SKILL.md` | Remove project name output, add index-first emphasis |
| `plugin/skills/explore/SKILL.md` | Replace list_projects validation with search_symbols or MCP error |
| `plugin/skills/savings/SKILL.md` | Review only -- currently clean of project refs |
| `README.md` | Rewrite MCP tools table, storage description, remove auto-index mention |
| `CLAUDE.md` | Verify CLI table (likely already done in Phase 3) |

### Verify Only
| File | Check |
|------|-------|
| All test files | Confirm 265 tests pass, no old `~/.token-squeeze` refs |
| `plugin/.claude-plugin/plugin.json` | Version number if bumping |

## Open Questions

1. **MCP CWD propagation -- empirical verification**
   - What we know: Node.js child_process inherits parent cwd. Claude Code launches from project root. The `.mcp.json` config does not specify a `cwd` field.
   - What's unclear: Whether Claude Code guarantees project-root cwd for plugin MCP servers, or whether it could be different in edge cases (workspaces, remote, etc.).
   - Recommendation: Proceed with the assumption it works (CONTEXT.md decision). Add `cwd: process.cwd()` explicitly to `execFileSync` as documentation/safety. Flag for manual testing after implementation.

2. **Version bump**
   - Claude's discretion. Recommendation: Bump to 3.0.0 since removing tools and changing schemas is a breaking change for any consumer of the MCP interface. Update both `plugin.json` and `SERVER_INFO` in `mcp-server.js`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (latest with .NET 9) |
| Config file | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| Quick run command | `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo -v q` |
| Full suite command | `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MCP-01 thru MCP-06 | MCP server changes | manual | Manual test: run MCP server, send JSON-RPC | N/A (JS, not C#) |
| PLUG-01 thru PLUG-06 | Plugin file changes | manual | Verify files deleted/updated | N/A (file ops) |
| DOC-01, DOC-02 | Documentation accuracy | manual | Visual review | N/A |
| TEST-01 | Tests use .cache/ paths | unit | `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo -v q` | Existing 265 tests |

### Sampling Rate
- **Per task commit:** `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo -v q`
- **Per wave merge:** Full suite
- **Phase gate:** Full suite green + manual MCP server smoke test

### Wave 0 Gaps
None -- existing test infrastructure covers all C# requirements. MCP server changes are JavaScript (no test framework in project for JS). Plugin/doc changes are file operations verified by inspection.

## Sources

### Primary (HIGH confidence)
- Direct file inspection of `plugin/mcp-server.js` (330 lines), all skill/agent/hook files
- Direct file inspection of CLI commands showing `Directory.GetCurrentDirectory()` + `.cache/` pattern
- Test run confirming 265/265 pass on current branch
- [Claude Code MCP docs](https://code.claude.com/docs/en/mcp) -- plugin MCP server configuration

### Secondary (MEDIUM confidence)
- [MCP Python SDK issue #1520](https://github.com/modelcontextprotocol/python-sdk/issues/1520) -- confirms no standard CWD protocol mechanism
- Web search results on Claude Code MCP server CWD behavior

### Tertiary (LOW confidence)
- CWD inheritance assumption (Node.js child_process default behavior applied to Claude Code plugin spawn context)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new libraries, just editing existing JS/MD files
- Architecture: HIGH - straightforward parameter removal and file deletion
- Pitfalls: HIGH - identified from direct code inspection (agents, extract batch logic, README tool names)
- CWD propagation: MEDIUM - Node.js default behavior is well-understood, but Claude Code's specific spawn behavior is not officially documented

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable -- no fast-moving dependencies)
