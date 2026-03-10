---
phase: 2
slug: storage-layer
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing) |
| **Config file** | `src/TokenSqueeze.Tests/TokenSqueeze.Tests.csproj` |
| **Quick run command** | `dotnet test src/TokenSqueeze.Tests --no-build -x` |
| **Full suite command** | `dotnet test src/TokenSqueeze.Tests` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/token-squeeze.sln && dotnet test src/TokenSqueeze.Tests --no-build`
- **After every plan wave:** Run `dotnet test src/TokenSqueeze.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | STOR-03 | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | Existing (needs update) | ⬜ pending |
| 02-01-02 | 01 | 1 | STOR-04 | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | STOR-05 | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | ❌ W0 | ⬜ pending |
| 02-01-04 | 01 | 1 | STOR-06 | unit | `dotnet test src/TokenSqueeze.Tests --filter "FullyQualifiedName~SplitStorageTests" --no-build` | ❌ W0 | ⬜ pending |
| 02-01-05 | 01 | 1 | TEST-02 | structural | Visual inspection — all `new IndexStore()` calls take a cacheDir arg | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] New test: `IndexStore_Constructor_DoesNotCreateDirectory` — covers STOR-04
- [ ] New test: `Save_WritesCachedirTag` — covers STOR-05
- [ ] New test: `Save_WritesGitignore` — covers STOR-06
- [ ] New test: `Save_DoesNotOverwriteExistingMarkers` — covers idempotency

*Existing infrastructure covers STOR-03 and TEST-02.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| TEST-02 structural | TEST-02 | Pattern compliance, not behavior | Verify all `new IndexStore()` calls pass cacheDir constructor arg |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
