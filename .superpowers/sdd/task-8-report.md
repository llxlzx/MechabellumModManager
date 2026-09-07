# Task 8 Report — Verification + release notes

Date: 2026-09-05  
Base commit: `4379bf9` (installer: prefer graceful close over force-kill on files-in-use)  
Tasks 1–7: committed through 4379bf9.

## Automated verification

| Step | Result |
|------|--------|
| `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj -c Release` | **PASS** — 429 passed, 0 failed, 0 skipped (~4s) |
| Stash required? | **No** — dirty WIP in working tree did not break build or tests |

### Critical-op test coverage (automated)

| Suite | Tests |
|-------|-------|
| `CriticalOpGuardTests` | 5 |
| `CriticalOpGateTests` | 11 |
| `MainViewModelCriticalOpTests` | 2 |
| **Subtotal** | **17** |

Related installer/i18n coverage from Tasks 1–7 is included in the 429 total.

## Manual checklist

| # | Scenario | Status | Notes |
|---|----------|--------|-------|
| 1 | Start switch → Busy/write → 检查更新 → blocked notify | **needs human** | Hard gate: no URL, localized notify |
| 2 | Settle wait → 检查更新 → confirm cancel/accept | **needs human** | Soft gate: confirm before opening Setup URL |
| 3 | Settle wait → close → confirm | **needs human** | Soft gate on window close |
| 4 | Leftover `critical-op.json` in AppData → startup modal | **needs human** | Simulate kill mid disk write |
| 5 | Repair/continue clears marker | **needs human** | Verify `Complete()` after success path |
| 6 | Setup while manager open → close-apps guidance | **needs human** | Task 7 copy in Inno Files-in-Use |

## Release notes

Updated `release/v1.1.2/RELEASE_NOTES.md` — new section **关键操作保护** (4 bullets).

## Pack / upload

Skipped per brief (only on user request).

## Commit

- `release/v1.1.2/RELEASE_NOTES.md` only (this report under `.superpowers/sdd/` is not committed).

---

## Final fix — ApplyRecoveryRepair gate clear (2026-09-05)

### Problem

`ApplyRecoveryRepair` left `IsRecoveryGateActive` true when `IsAwaitingSteamSettle || IsBranchWizardInProgress`, causing **HardBlock** on update/close for the entire Steam wait. `ApplyRecoveryContinue` already cleared via `ClearRecoveryGate()` in that case.

### Change

- **MainViewModel.ApplyRecoveryRepair**: after reload/navigate and optional orphan repair, clear gate when `IsAwaitingSteamSettle || IsBranchWizardInProgress || IsStaleReadyRecovery()` (matches Continue semantics; orphan repair attempt unchanged).
- **MainViewModelCriticalOpTests**: added `ApplyRecoveryRepair_clears_gate_when_settle_or_wizard` (mirrors `ApplyRecoveryContinue_clears_gate_routes_settle_ui`).
- **DeployBlockedReason**: already uses `CriticalOpRecoveryBody` when `IsRecoveryGateActive` — no change needed.

### Verification

```
dotnet test --filter FullyQualifiedName~CriticalOp
```

| Result | Count |
|--------|-------|
| Passed | 24 |
| Failed | 0 |
| Skipped | 0 |

### Commit

- SHA: `c05ab796ca532e7cd519d24ade60a6736ff65a92`
- Files: `MainViewModel.cs`, `MainViewModelCriticalOpTests.cs` only (other WIP not staged)

### Status

**Ready for merge** — recovery repair no longer hard-blocks update/close during Steam settle; settle/wizard still session-locks deploy via `IsAwaitingSteamSettle` / `IsBranchWizardInProgress` with **SoftConfirm** close/update gates.
