# Phase 4: Integration - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Update the MCP server, plugin skills/hooks, tests, and documentation to reflect the simplified local-storage model completed in Phases 1-3. After this phase, the entire stack — CLI, MCP, plugin, docs, tests — operates against `<cwd>/.cache/` with no project name concept.

</domain>

<decisions>
## Implementation Decisions

### MCP server rewiring
- Remove `list_projects` tool entirely from TOOLS array and handleToolCall switch
- Drop `project` parameter from `read_file_outline`, `read_symbol_source`, `search_symbols` input schemas
- CLI args change: `["outline", args.file]` not `["outline", args.project, args.file]`, same pattern for extract/find
- Add `.cache/manifest.json` existence check before tool execution — return clear error: "No index found. Run /token-squeeze:index"
- MCP server inherits cwd from Claude Code's MCP spawn — no special cwd handling needed (Claude Code sets it to project root)

### Plugin cleanup — hooks and scripts
- Delete `hooks/hooks.json` (SessionStart hook)
- Delete `scripts/auto-index.sh`
- Delete `scripts/auto-index.ps1` if it exists (requirement says both .sh and .ps1)
- The hooks/ and scripts/ directories can be deleted entirely if empty after

### Plugin cleanup — skills
- Delete `skills/purge/` directory entirely (purge command no longer exists)
- Update `skills/index/SKILL.md`: remove "Project name assigned" from output section, emphasize "run this first" positioning, add `.gitignore` offer after successful index
- Update `skills/explore/SKILL.md`: remove `list_projects` validation step, replace with `.cache/` existence check or just let MCP error propagate
- Update `skills/savings/SKILL.md`: no project name references to remove (already generic), but review for accuracy

### Documentation updates
- `CLAUDE.md`: update CLI commands table (already done in Phase 3 commit — verify), remove any remaining project-name references
- `README.md`: update to emphasize index-first workflow, remove project naming, update example output

### Test updates
- Verify all 265+ tests still pass against final codebase state
- Any tests referencing old MCP tool signatures or project names need updating (these are likely in integration/smoke tests if they exist)
- TEST-01 requirement: tests use `.cache/` paths, not global `~/.token-squeeze/` paths

### Claude's Discretion
- Whether to bump MCP server version number (currently 2.0.1)
- Exact wording of the "no index found" MCP error message (as long as it mentions `/token-squeeze:index`)
- Whether to simplify the explore skill's multi-agent approach or keep it as-is minus the list_projects call

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `mcp-server.js`: Self-contained MCP server (~330 lines), zero dependencies — straightforward to edit
- `runCli()` function: Already handles binary detection and error propagation — just change the args arrays
- `plugin/.mcp.json`: Simple config, no changes needed (just points to mcp-server.js)

### Established Patterns
- MCP tools follow consistent pattern: validate args → build CLI args → runCli() → return result
- Skills use `${CLAUDE_PLUGIN_ROOT}` for binary path resolution
- All CLI output is JSON via `JsonOutput.Write()` — MCP server passes through as text

### Integration Points
- `mcp-server.js` TOOLS array defines what Claude Code sees as available tools
- `mcp-server.js` handleToolCall() maps tool names to CLI invocations
- `hooks/hooks.json` is auto-discovered by Claude Code plugin system
- Skills in `skills/*/SKILL.md` are auto-discovered by Claude Code plugin system
- `.claude-plugin/` manifest may reference hooks — check if it needs updating

</code_context>

<specifics>
## Specific Ideas

- STATE.md notes a blocker: "MCP CWD propagation: Need empirical verification during Phase 4 of how Claude Code sets working directory for MCP servers." — The researcher should verify this.
- The `.mcp.json` already passes `CLAUDE_PLUGIN_ROOT` env var; cwd behavior depends on Claude Code's MCP spawn implementation
- Phase 1 context established: `.cache/` directory name is locked, no migration from old format

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-integration*
*Context gathered: 2026-03-09*
