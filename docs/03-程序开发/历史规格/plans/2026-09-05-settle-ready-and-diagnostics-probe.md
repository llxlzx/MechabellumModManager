# Settle Ready Gate + Diagnostics Probe — Implementation Plan

> **For agentic workers:** Implement task-by-task with TDD. Checkboxes track progress.

**Goal:** Block false Ready on Steam settle when deploy/ACF fail; tighten “already on branch”; strengthen diagnostics for Track A/B; reinforce Melon launch hint.

**Architecture:** Change `DeployBoundProfileAndClearSettle` to clear settle only after Apply + settled ACF snapshot succeed. Gate switch early-out on `GameDetector` Ready. Enrich diagnostics `environment.json` with a structured `probe` + `findings` for Melon log freshness and dual-folder/ACF state.

**Tech Stack:** WPF/.NET 8, xUnit, FluentAssertions, existing BranchSwitch / DiagnosticsExport services.

## Global Constraints

- Approach 1 only (no SteamThenExe timeout fallback).
- Unsettled ACF blocks Ready.
- Do not commit unless user asks.

---

### Task 1: Settle clear only on success

**Files:** `MainViewModel.cs`, `MainViewModelBranchSwitchTests.cs`, `Strings*.resx`

- [ ] RED: Confirm when GameMissing → stays awaiting, no manifest
- [ ] RED: Confirm when unsettled ACF → stays awaiting  
- [ ] Update existing success test to write settled ACF
- [ ] GREEN: `DeployBoundProfileAndClearSettle` returns early without `ClearSteamSettle` on failure

### Task 2: Aligned but incomplete

- [ ] RED: IsAlignedWith + GameMissing → not NotifyAlreadyOnGameBranch
- [ ] GREEN: gate in `SwitchToBranchAsync`

### Task 3: Launch Melon hint

- [ ] Add `LogMelonLaunchVerifyHint` after ApplyAndLaunch success (all locales)

### Task 4: Diagnostics probe

**Files:** `DiagnosticsExportService.cs` (+ helper), request fields, `MainViewModel.ExportDiagnostics`, tests

- [ ] RED: environment contains probe.melonLatestLog / findings when log stale
- [ ] RED: probe.branch includes junction, store roots, acfSettled
- [ ] GREEN: implement probe + findings list
- [ ] Pass launchMode, game/steam running, awaitingSettle from VM

### Task 5: Verify

- [ ] `dotnet test` on affected projects
