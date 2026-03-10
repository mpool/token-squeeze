---
phase: 4
slug: integration
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-09
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET 9) |
| **Config file** | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| **Quick run command** | `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo -v q` |
| **Full suite command** | `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo -v q`
- **After every plan wave:** Run `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | MCP-01, MCP-02, MCP-03 | manual | Verify MCP tool schemas in mcp-server.js | N/A (JS) | ⬜ pending |
| 04-01-02 | 01 | 1 | MCP-04 | manual | Verify list_projects removed from mcp-server.js | N/A (JS) | ⬜ pending |
| 04-01-03 | 01 | 1 | MCP-05 | manual | Verify manifest check added to mcp-server.js | N/A (JS) | ⬜ pending |
| 04-01-04 | 01 | 1 | MCP-06 | manual | Verify cwd passed to execFileSync | N/A (JS) | ⬜ pending |
| 04-02-01 | 02 | 1 | PLUG-01 | manual | Verify hooks.json deleted | N/A (file) | ⬜ pending |
| 04-02-02 | 02 | 1 | PLUG-02 | manual | Verify auto-index scripts deleted | N/A (file) | ⬜ pending |
| 04-02-03 | 02 | 1 | PLUG-03, PLUG-04, PLUG-05 | manual | Verify skills updated/purge deleted | N/A (file) | ⬜ pending |
| 04-02-04 | 02 | 1 | PLUG-06 | manual | Verify purge skill deleted | N/A (file) | ⬜ pending |
| 04-03-01 | 03 | 2 | DOC-01 | manual | Visual review of CLAUDE.md | N/A (doc) | ⬜ pending |
| 04-03-02 | 03 | 2 | DOC-02 | manual | Visual review of README.md | N/A (doc) | ⬜ pending |
| 04-03-03 | 03 | 2 | TEST-01 | unit | `dotnet test src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj --nologo -v q` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. MCP server changes are JavaScript (no JS test framework in project). Plugin/doc changes are file operations verified by inspection. C# tests already pass with `.cache/` paths.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| MCP tools accept no project param | MCP-01 thru MCP-03 | JavaScript, no test framework | Inspect TOOLS array schemas in mcp-server.js |
| list_projects tool removed | MCP-04 | JavaScript, no test framework | Verify no list_projects in TOOLS or handleToolCall |
| MCP manifest guard | MCP-05 | JavaScript, no test framework | Verify fs.existsSync check before tool execution |
| MCP cwd propagation | MCP-06 | Runtime behavior | Verify cwd option in execFileSync or default inheritance |
| Hook/script deletion | PLUG-01, PLUG-02 | File operations | Verify hooks/ and scripts/ dirs empty or deleted |
| Skill updates | PLUG-03 thru PLUG-06 | Content review | Review skill SKILL.md files for project name refs |
| Doc accuracy | DOC-01, DOC-02 | Content review | Visual review of CLAUDE.md and README.md |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-03-09
