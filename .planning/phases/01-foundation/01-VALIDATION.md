---
phase: 1
slug: foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| **Quick run command** | `dotnet test src/TokenSqueeze.Tests/ --no-build --filter "Category!=Integration"` |
| **Full suite command** | `dotnet test src/TokenSqueeze.Tests/` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/TokenSqueeze/`
- **After every plan wave:** Run `dotnet test src/TokenSqueeze.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | MODL-01 | build | `dotnet build src/TokenSqueeze/` | ✅ | ⬜ pending |
| 1-01-02 | 01 | 1 | MODL-02 | build | `dotnet build src/TokenSqueeze/` | ✅ | ⬜ pending |
| 1-01-03 | 01 | 1 | MODL-03 | build | `dotnet build src/TokenSqueeze/` | ✅ | ⬜ pending |
| 1-02-01 | 02 | 1 | STOR-01, STOR-02 | unit | `dotnet test src/TokenSqueeze.Tests/` | ✅ | ⬜ pending |
| 1-02-02 | 02 | 1 | SEC-01 | unit | `dotnet test src/TokenSqueeze.Tests/` | ✅ | ⬜ pending |
| 1-02-03 | 02 | 1 | SEC-02 | unit | `dotnet test src/TokenSqueeze.Tests/` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements — xUnit test project already exists with build verification.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| JSON serialization round-trips without ProjectName | MODL-01, MODL-02 | Verify serialized JSON shape | Serialize/deserialize CodeIndex and Manifest, confirm no projectName field |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
