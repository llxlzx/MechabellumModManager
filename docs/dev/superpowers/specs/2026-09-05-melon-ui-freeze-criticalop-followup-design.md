# Melon UI freeze + CriticalOp follow-up — Design

Date: 2026-09-05  
Status: approved for planning (user 认可整合包)  
Related: `2026-09-05-critical-op-guard-design.md`, diagnostics `MechabellumModManager-diagnostics-20260905-165323`

## Problem

Two issues share the branch-switch / Melon path:

1. **UI freeze (zombie process)** — After disk swap, `EnsureMelonAssembliesForStore` still calls `GetAwaiter().GetResult()` on the UI thread. Dispatcher starvation leads to `Win32Exception 1816` (配额不足). App marks 1816 `Handled=true`, so the process stays alive with a dead UI; Task Manager kill is required. Evidence: `crash.log` 21:59 / 22:08, manager log Melon wait gap ~21:05–21:13, code comment at `EnsureMelonAssembliesForStoreAsync`.

2. **CriticalOp recovery gate holes** (after guard feature shipped):
   - **A:** `ApplyRecoveryContinue` clears `IsRecoveryGateActive` for any mid-wizard step, but SoftConfirm only uses `WaitingDownloadB` → other wizard steps become **Allow** for update/close.
   - **B:** Continue can leave `IsRecoveryGateActive` HardBlock when dual-folder is Enabled+Ready and disk “looks orphan”, while Repair UI is hidden (`BranchSwitchEnabled`); Teardown never calls `ClearRecoveryGate` → stuck session.
   - **D:** Startup recovery modal can race Idle `ScheduleAssemblyGeneratePrompt`.

CriticalOp core (marker lifecycle, Classify priority, disk Begin/Complete, installer copy) is solid and must not be reopened.

## Goals

1. Eliminate UI-thread Melon assembly `GetResult` on all production paths (switch, wizard link, Apply/Launch).
2. Keep Melon generation **before** settle; hold Hard-relevant busy/session state until await completes.
3. Fix Continue SoftConfirm coverage for all `IsBranchWizardInProgress` steps.
4. Ensure recovery gate cannot stick without an actionable path; clear gate on successful teardown.
5. Suppress assembly-generate Confirm while recovery gate is active.
6. Add regression tests for the above (unit tests currently pass but do not cover these holes).

## Non-goals

- New `CriticalOpKind` for Il2Cpp assembly generation (long wait; crash mid-gen remains without durable marker — accepted for this package).
- Removing Win32 1816 `Handled=true` (keep as belt; revisit after freeze paths are gone).
- Reworking CriticalOp recovery dialog UX / Repair reflection cleanup (optional later).
- In-process self-update, Steam download progress, journal replay expansion.

## Decisions (locked)

| Item | Choice |
|------|--------|
| Package shape | Single follow-up: Melon freeze + CriticalOp gate holes + startup prompt guard |
| Melon UX during generation | **Busy progress dialog** (same pattern as branch swap busy), then end busy **before** settle/notify |
| Melon vs CriticalOp marker | **No** durable marker for assembly gen; keep `IsBranchSwitchBusy` / Busy Hard until await done |
| Soft after Continue | SoftConfirm for **all** `IsBranchWizardInProgress`, not only `WaitingDownloadB` |
| Stuck recovery | Align stale/orphan clear with Repair visibility; always `ClearRecoveryGate` on successful teardown |
| ApplyProfile sync | Sync path must **not** generate assemblies; only async / Ready gate |
| 1816 handler | Keep |

---

## 1. Melon async migration

### Current (broken)

```text
SwitchToBranchAsync
  RunBusyAsync(disk swap + CriticalOp BranchDiskWrite)
  EnsureMelonLoaderForDualStores → EnsureMelonAssembliesForStore → GetResult()  // UI block
  SettleAfterSilentBetaAsync
  finally IsBranchSwitchBusy = false

ApplyProfile (sync) → EnsureMelonAssembliesForStore → GetResult()  // ApplyAndLaunch / settle deploy path
```

### Target

```text
SwitchToBranchAsync
  RunBusyAsync(disk swap + CriticalOp)
  RunBusyAsync("生成 MelonLoader 程序集…", await EnsureMelonLoaderForDualStoresAsync)
  SettleAfterSilentBetaAsync   // only after Melon await + RefreshStatusCore
  finally IsBranchSwitchBusy = false   // clear only after Melon + settle entry

Wizard link (ApplyLinkedWizardState / TryCreateLinkAndApplyAsync)
  await EnsureMelonLoaderForDualStoresAsync inside or as sequential RunBusyAsync segment
  never GetResult on UI thread

ApplyProfile (sync)
  Ready check only — no Melon generation
ApplyProfileAsync / ApplyAndLaunch
  await EnsureMelonAssembliesForStoreAsync when Missing, then deploy
```

### API changes

| API | Action |
|-----|--------|
| `EnsureMelonAssembliesForStore` (GetResult wrapper) | **Delete**; migrate all callers to async |
| `EnsureMelonLoaderForDualStores` | Replace with `EnsureMelonLoaderForDualStoresAsync` only |
| `MelonLoaderAssemblyGenerator.EnsureAssemblies` sync | **Delete** if no remaining callers after VM migration; otherwise leave unused (not called from UI) |

### Ordering rules (must not break)

1. Melon await **before** `SettleAfterSilentBetaAsync`.
2. Busy dialog **ends before** settle SoftConfirm / notify (existing branch-busy plan rule).
3. `IsBranchSwitchBusy` remains true across Melon busy segment (HardBlock update/close).
4. After Melon, `RefreshStatusCore(offerAssemblyGeneratePrompt: false)` before settle.
5. Do not nest a second CriticalOp Begin around Melon gen.

---

## 2. CriticalOp SoftConfirm hole (Bug A)

### Change

Pass `IsBranchWizardInProgress` into `CriticalOpGate.Classify` as the Soft wizard flag (replace today’s `IsWizardWaitingSteam` / `WaitingDownloadB`-only argument). Rename the parameter to `wizardInProgress` (or equivalent) for clarity.

`WaitingDownloadB` remains covered because it is a subset of `IsBranchWizardInProgress`. Broadening Soft to all mid-wizard steps is intentional.

### Preserve

- Settle SoftConfirm unchanged.
- Hard still wins when Running / Busy / `IsBranchSwitchBusy` / `IsRecoveryGateActive`.

---

## 3. Recovery stickiness (Bug B)

### Continue

Locked algorithm after Settings navigation:

1. If settle or wizard-in-progress → clear gate (existing Soft path); return.
2. Else if Ready and **repair is not offered** (`ShowRepairOrphanDualLayout == false`) → clear gate (healthy enabled dual-folder must not stick Hard).
3. Else if Ready and repair **is** offered → keep gate until Repair succeeds (user can use Repair).
4. Else → clear gate and stay on Settings (fail-open; never permanent Hard with no escape).

`IsStaleReadyRecovery` / orphan inspect used for Continue must use the same eligibility as `ShowRepairOrphanDualLayout` (not raw “junction exists while Enabled”).

### Teardown

On successful `RunTeardownCoreAsync`: always `ClearRecoveryGate()`. Also `ClearInterrupted()` when `!_criticalOp.IsRunning` so the marker does not re-open the modal next launch after teardown cleaned the layout.

---

## 4. Startup assembly prompt race (Bug D)

In `ScheduleAssemblyGeneratePrompt` (or its Idle callback): if `IsRecoveryGateActive` or `ShouldOfferCriticalRecovery()` would still apply, **skip** scheduling Confirm.

Recovery modal remains exclusive until Continue/Repair resolves.

---

## 5. Testing

| Test | Expect |
|------|--------|
| Continue + `WizardStep=Linked` (or Declared) | SoftConfirm on update/close — **not** Allow |
| Continue + Enabled + Ready + junction present | Gate clears (or Soft), not permanent HardBlock without Repair |
| Successful teardown | `IsRecoveryGateActive == false` |
| Switch with Missing assemblies (fake generator) | Awaits async path; no sync GetResult; busy begin/end around Melon; settle only after |
| Recovery active | `ScheduleAssemblyGeneratePrompt` does not show Confirm |

Keep existing CriticalOp settle SoftConfirm tests green.

---

## 6. Risk / cascade matrix

| Risk | Mitigation |
|------|------------|
| Settle before assemblies ready | Melon before settle; RefreshStatus after Melon |
| Second busy overlapping settle notify | End Melon busy before `SettleAfterSilentBetaAsync` |
| SoftConfirm wider → more close confirms | Intentional |
| Orphan false-positive clears marker too eagerly | Align with Repair visibility |
| 1816 still fires from other causes | Keep handler; log only |
| Parallel CriticalOp reopen | Out of scope |

---

## 7. Implementation order

1. Melon async + Busy segment + delete GetResult callers (P0).
2. Bug B Continue/Teardown gate clear (P0).
3. Bug A SoftConfirm widen (P1).
4. Startup prompt suppress (P1).
5. Regression tests for all four.
6. Manual smoke: switch branch with Missing assemblies (or forced gen), Continue mid-wizard SoftConfirm, teardown clears gate.

---

## Success criteria

- No production UI path calls `.GetAwaiter().GetResult()` for Melon assembly generation.
- Reproducing post-switch Melon wait no longer freezes the main window (dispatcher remains responsive under Busy).
- Continue mid-wizard → SoftConfirm; no Allow while wizard in progress.
- No stuck HardBlock recovery without Repair/Teardown escape.
- CriticalOp unit tests still pass; new regression tests listed above pass.
