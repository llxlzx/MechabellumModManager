# Melon UI freeze + CriticalOp follow-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop UI-thread Melon `GetResult` freezes (Win32 1816 zombie), and close CriticalOp Continue SoftConfirm / stuck-recovery / startup prompt races without reopening the CriticalOp core.

**Architecture:** Make dual-store Melon ensure fully async under a second `RunBusyAsync` segment before settle; strip Melon generation from sync `ApplyProfile`; widen SoftConfirm to all mid-wizard steps; align Continue clear with Repair visibility and clear gate on teardown; skip assembly Idle prompt while recovery is active.

**Tech Stack:** .NET 8 WPF, xUnit + FluentAssertions, existing `MainViewModel` / `CriticalOpGate` / `RunBusyAsync` / Melon assembly generator.

**Spec:** `docs/superpowers/specs/2026-09-05-melon-ui-freeze-criticalop-followup-design.md`

## Global Constraints

- No new `CriticalOpKind` for Il2Cpp assembly generation.
- Keep Win32 1816 `Handled=true` in `App.xaml.cs`.
- Melon await **before** `SettleAfterSilentBetaAsync`; Busy must **end before** settle SoftConfirm / notify.
- `IsBranchSwitchBusy` stays true across Melon busy segment.
- SoftConfirm for **all** `IsBranchWizardInProgress` (not only `WaitingDownloadB`).
- Do not reopen CriticalOp marker lifecycle / Classify Hard priority / disk Begin/Complete wrappers.
- i18n: add `BusyGeneratingMelonAssemblies` to `Strings.resx` + `Strings.en.resx` at minimum; ja/ru/de English fallback OK.
- Sync `ApplyProfile` must **not** generate assemblies (Ready / Missing fail path only).

## File structure

| File | Responsibility |
|------|----------------|
| `src/MechabellumModManager/Services/CriticalOpGate.cs` | Soft wizard param = mid-wizard in progress |
| `src/MechabellumModManager/ViewModels/MainViewModel.cs` | Async Melon dual-store; switch/wizard/Apply; Continue/Teardown; prompt skip |
| `src/MechabellumModManager/Resources/Strings.resx` (+ `.en`) | `BusyGeneratingMelonAssemblies` |
| `src/MechabellumModManager/Services/MelonLoaderAssemblyGenerator.cs` | Delete unused sync `EnsureAssemblies` if no callers |
| `tests/MechabellumModManager.Tests/CriticalOpGateTests.cs` | Param rename |
| `tests/MechabellumModManager.Tests/MainViewModelCriticalOpTests.cs` | Continue SoftConfirm; gate clear; prompt skip |

### Task 1: SoftConfirm for all mid-wizard (Bug A) — TDD

**Files:** CriticalOpGate.cs, MainViewModel.EvaluateCloseOrUpdateGate (~363-373), CriticalOpGateTests, MainViewModelCriticalOpTests

**Interfaces:**
- Consumes: `CriticalOpGate.Classify`
- Produces: Soft when `awaitingSteamSettle || wizardInProgress`; VM passes `IsBranchWizardInProgress`

- [ ] **Step 1: Write failing test** `ApplyRecoveryContinue_mid_wizard_Linked_is_SoftConfirm_not_Allow`
  - WriteInterruptedMarker; BranchSwitchConfig Enabled=true, WizardStep=Linked; IsRecoveryGateActive=true; ApplyRecoveryContinue(); expect IsRecoveryGateActive false, IsBranchWizardInProgress true, EvaluateCloseOrUpdateGate SoftConfirm (not Allow).

- [ ] **Step 2: Run** `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ApplyRecoveryContinue_mid_wizard_Linked"` — expect FAIL (Allow today).

- [ ] **Step 3: Implement** Rename Classify param `wizardWaitingSteam` → `wizardInProgress`. EvaluateCloseOrUpdateGate pass `IsBranchWizardInProgress` instead of `IsWizardWaitingSteam`. Update CriticalOpGateTests param name.

- [ ] **Step 4: Run** `--filter FullyQualifiedName~CriticalOp` — PASS.

- [ ] **Step 5: Commit** `fix: SoftConfirm for all mid-wizard steps after recovery Continue`

### Task 2: Recovery stickiness + teardown clear (Bug B) — TDD

**Files:** MainViewModel ApplyRecoveryContinue / IsStaleReadyRecovery / RunTeardownCoreAsync; MainViewModelCriticalOpTests

- [ ] **Step 1: Write failing tests**
  - `ApplyRecoveryContinue_Enabled_Ready_clears_gate_when_repair_not_offered` (Enabled+Ready, ShowRepairOrphanDualLayout false, Continue clears gate+marker).
  - `Teardown_success_clears_recovery_gate` using existing teardown test setup patterns.

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement ApplyRecoveryContinue**
```
ActiveContentPage = Settings;
if (IsAwaitingSteamSettle || IsBranchWizardInProgress) { ClearRecoveryGate(); return; }
if (!ShowRepairOrphanDualLayout) ClearRecoveryGate();
```
  Align IsStaleReadyRecovery with `!ShowRepairOrphanDualLayout` instead of `!TryInspectOrphanDualLayout()`.
  On RunTeardownCoreAsync success (`ok = true`): `ClearRecoveryGate()`.

- [ ] **Step 4: CriticalOp tests PASS**

- [ ] **Step 5: Commit** `fix: clear recovery gate when Repair unavailable or teardown succeeds`

### Task 3: Skip assembly prompt during recovery (Bug D) — TDD

**Files:** ScheduleAssemblyGeneratePrompt (~933-974); tests

- [ ] **Step 1:** Assert early-return when `IsRecoveryGateActive` (optional public `CanOfferAssemblyGeneratePrompt => !IsRecoveryGateActive && Missing`).

- [ ] **Step 2: FAIL baseline**

- [ ] **Step 3:** At start of ScheduleAssemblyGeneratePrompt and inside Idle RunPrompt before Confirm: `if (IsRecoveryGateActive) return;`

- [ ] **Step 4: PASS**

- [ ] **Step 5: Commit** `fix: do not prompt Melon assembly generate while recovery gate is active`

### Task 4: i18n BusyGeneratingMelonAssemblies

- [ ] **Step 1:** Add to Strings.resx / Strings.en.resx (ja/ru/de EN fallback OK).
  - zh: 正在生成 MelonLoader 程序集，请勿关闭管理器…
  - en: Generating MelonLoader assemblies. Do not close the manager…

- [ ] **Step 2: Commit** `i18n: add BusyGeneratingMelonAssemblies string`

### Task 5: Delete sync Melon GetResult + async dual-store (P0 freeze)

**Current call sites (must migrate):**
- EnsureMelonAssembliesForStore GetResult (~3579-3582) called from ApplyProfile (~1270) and EnsureMelonLoaderForDualStores (~3569)
- EnsureMelonLoaderForDualStores called from SwitchToBranchAsync (~3217) and ApplyLinkedWizardState (~3540)
- ApplyAndLaunch sync ApplyProfile (~1351-1355)
- Generator sync EnsureAssemblies (~97-107) — delete if unused after

- [ ] **Step 1:** Test `ApplyProfile_sync_does_not_generate_when_assemblies_missing` (Missing fixture; returns quickly without Unity).

- [ ] **Step 2: Baseline**

- [ ] **Step 3: Implement**
  1. Delete EnsureMelonAssembliesForStore.
  2. Replace EnsureMelonLoaderForDualStores with EnsureMelonLoaderForDualStoresAsync (await EnsureMelonAssembliesForStoreAsync).
  3. SwitchToBranchAsync after disk busy:
     `await RunBusyAsync(T("BusyGeneratingMelonAssemblies"), () => EnsureMelonLoaderForDualStoresAsync(preferTarget: target));`
     then `await SettleAfterSilentBetaAsync(silentResult);`
     Keep IsBranchSwitchBusy true until finally after Melon+settle entry.
  4. Wizard: after junction busy / ApplyLinked config without Melon, second RunBusyAsync for Melon async (preferred over GetResult inside junction busy).
  5. Sync ApplyProfile(bool): remove Melon ensure block; Missing fails Ready gate.
  6. ApplyAndLaunch → async ApplyAndLaunchAsync via await ApplyProfileAsync(); verify XAML ApplyAndLaunchCommand binding.
  7. Delete MelonLoaderAssemblyGenerator.EnsureAssemblies sync if no callers.

- [ ] **Step 4:** `dotnet test ... --filter "FullyQualifiedName~CriticalOp|FullyQualifiedName~BranchSwitch|FullyQualifiedName~Melon|FullyQualifiedName~ApplyProfile"`

- [ ] **Step 5: Commit** `fix: await Melon assembly generation off the UI GetResult path`

### Task 6: Verification + smoke

- [ ] **Step 1:** Filtered regression CriticalOp|BranchSwitch|MelonLoader|Orphan — all PASS
- [ ] **Step 2: Manual smoke**
  1. Dual-folder switch → Melon Busy then settle; no zombie UI
  2. Interrupted + WizardStep=Linked → Continue → SoftConfirm on close
  3. Enabled Ready + marker → Continue clears gate
  4. Teardown with recovery gate → cleared
  5. Missing assemblies + recovery modal → no assembly Confirm until Continue/Repair
- [ ] **Step 3:** Optional release-notes bullet

## Self-review vs spec

| Spec requirement | Task |
|------------------|------|
| Delete GetResult / async dual-store / Busy before settle | Task 5 |
| Soft all mid-wizard | Task 1 |
| Continue/Teardown gate clear | Task 2 |
| Skip assembly prompt under recovery | Task 3 |
| Busy i18n string | Task 4 |
| Keep 1816 handler / no Melon CriticalOp kind | Global Constraints |
