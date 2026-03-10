---
phase: 3
slug: cli-commands
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| **Quick run command** | `dotnet test src/TokenSqueeze.Tests --no-build -x` |
| **Full suite command** | `dotnet test src/TokenSqueeze.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/token-squeeze.sln && dotnet test src/TokenSqueeze.Tests --no-build`
- **After every plan wave:** Run `dotnet test src/TokenSqueeze.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-01-01 | 01 | 1 | CLI-01 | Already covered | N/A (no change) | N/A | ⬜ pending |
| 3-01-02 | 01 | 1 | CLI-02 | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | ❌ W0 | ⬜ pending |
| 3-01-03 | 01 | 1 | CLI-03 | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | ❌ W0 | ⬜ pending |
| 3-01-04 | 01 | 1 | CLI-04 | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | ❌ W0 | ⬜ pending |
| 3-01-05 | 01 | 1 | CLI-05 | manual-only | Verify `ListCommand.cs` file doesn't exist | N/A | ⬜ pending |
| 3-01-06 | 01 | 1 | CLI-06 | manual-only | Verify `PurgeCommand.cs` file doesn't exist | N/A | ⬜ pending |
| 3-01-07 | 01 | 1 | CLI-07 | unit | `dotnet test src/TokenSqueeze.Tests --filter "CliCommandTests" --no-build` | ❌ W0 | ⬜ pending |
| 3-01-08 | 01 | 1 | CLI-08 | manual-only | Inspect Program.cs | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `src/TokenSqueeze.Tests/CliCommandTests.cs` — stubs for CLI-02, CLI-03, CLI-04, CLI-07
  - Note: Test cache guard logic directly; command-level integration tests deferred to Phase 4 (TEST-01)

*Existing infrastructure covers CLI-01, CLI-05, CLI-06, CLI-08 via inspection/deletion.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| list command deleted | CLI-05 | File deletion check | Verify `ListCommand.cs` doesn't exist |
| purge command deleted | CLI-06 | File deletion check | Verify `PurgeCommand.cs` doesn't exist |
| Program.cs registrations | CLI-08 | Static inspection | Verify only index, outline, extract, find registered |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
