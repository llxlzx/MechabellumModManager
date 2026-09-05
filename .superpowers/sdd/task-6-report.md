# Task 6 Report: Startup recovery modal

**Status:** DONE (adjudication fix `619d965`)  
**Commits:**
- `01c2384` feat: show startup critical-op recovery modal (VM APIs, tests, first dialog/App hook)
- `31ac1e2` fix: pass CriticalOpGuard into startup recovery dialog (dialog + App only)
- `619d965` fix: clear recovery gate when Continue routes to settle UI

Unrelated WIP left unstaged (MainViewModel session-lock/orphan, ACF CLI, installer, resx, etc.).

## What landed

- `IsRecoveryGateActive` `internal set` + `NotifyBranchGates`
- Folded into `IsSessionLocked` (locks deploy / Apply / Apply-and-launch)
- `ShouldOfferCriticalRecovery(guard)` — marker-primary; optional orphan+wizard via reflection (`InspectOrphanDualLayout` not required on clean HEAD)
- `ApplyRecoveryContinue()` — reload branch UI; settle/wizard → Settings + `ClearRecoveryGate()` (settle/wizard lock remains); stale Ready + not orphan → clear gate
- `ApplyRecoveryRepair()` — Settings; optional `RepairOrphanDualLayoutCommand` if present; stale Ready clears
- `ClearRecoveryGate()` also called from `ClearSteamSettle()`
- `CriticalOpRecoveryDialog` — Continue / Repair / Diagnostics (does not lift gate); close cancelled until Continue or Repair
- `App` after `Show()`: if `ShouldOfferCriticalRecovery` → set gate + modal; Continue/Repair routed

## Tests

TDD: tests written first → compile fail CS0200 (readonly gate) + CS1061 (missing methods). Then implement.

```
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~CriticalOp --nologo
→ 失败: 0，通过: 23 (720 ms)
```

Required:
- `IsRecoveryGateActive_locks_deploy` — session lock, CanDeployOrLaunch false, HardBlock
- `ApplyRecoveryContinue_clears_stale_Ready_marker` — marker gone, gate off

Also: offer true/false, Continue clears gate but keeps settle lock, Repair clears stale Ready.

## Manual checklist

1. Drop leftover `critical-op.json` in AppData → launch → modal before writes
2. Continue on Ready/clean → gate + marker clear; deploy works
3. Continue while AwaitingSteamSettle → Settings + settle button; writes stay locked
4. Repair on stale Ready → same clear as Continue
5. Diagnostics from modal → export; modal/gate remain
6. X/Alt+F4 on modal → stays open until Continue or Repair

## Self-review

- Marker-primary trigger avoids nagging; orphan is best-effort reflection.
- Session lock + Classify(recoveryModalPending) already hard-block update/close.
- Dialog matches ConfirmDialog chrome (steel/amber).
- `01c2384` VM recovery bits already on HEAD; this session did not re-commit fat MainViewModel WIP.

## Concerns

1. **`DeployBlockedReason` recovery copy** (`CriticalOpRecoveryBody`) is only in unstaged VM WIP; committed tree still locks deploy but may show generic busy text.
2. **Repair is fire-and-forget** (`_ = ApplyRecoveryRepair()`); orphan command only runs if WIP `RepairOrphanDualLayoutCommand` exists and CanExecute.
3. **Committed `IsSessionLocked`** uses `IsBranchWizardBlocking` (enabled+mid-wizard), not session-lock WIP’s `IsBranchWizardInProgress`. Recovery bit is present either way.
4. Modal has **no Cancel** — intentional; leftover marker + X would otherwise skip the gate.

## Adjudication fix (Continue + settle/wizard)

**Issue:** Continue routed to Settings/settle UI but left IsRecoveryGateActive and the critical-op.json marker, so deploy stayed hard-blocked with recovery semantics even though real work was settle/wizard (SoftConfirm).

**Fix:** In ApplyRecoveryContinue(), when IsAwaitingSteamSettle || IsBranchWizardInProgress, call ClearRecoveryGate() after switching to Settings (marker cleared via gate helper; session lock remains via settle/wizard flags).

**Test:** Renamed/updated ApplyRecoveryContinue_clears_gate_routes_settle_ui — expects gate off, marker gone, IsAwaitingSteamSettle true, CanDeployOrLaunch false, EvaluateCloseOrUpdateGate → SoftConfirm.

```
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~CriticalOp --nologo
已通过! - 失败: 0，通过: 23，已跳过: 0，总计: 23，持续时间: 623 ms
```

**Manual checklist item 3 (updated):** Continue while AwaitingSteamSettle → Settings + settle button; recovery gate/marker cleared; writes still locked via settle state.

