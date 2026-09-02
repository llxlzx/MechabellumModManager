# Task 8 Report: MainViewModel wiring

**Status:** DONE  
**Branch:** `feature/mod-manager`  
**Baseline:** `4feb684`  
**Commit:** `f7933a0` 鈥?`feat: main ViewModel for library, profiles, apply flow`  
**Author (commit-local):** MechabellumModManager `<dev@local>`  
**Date:** 2026-09-02

---

## Summary

Constructor-injectable `MainViewModel` (+ `ModItemViewModel`, `ProfileItemViewModel`) wires library/profiles/deploy/launch/risk with CommunityToolkit.Mvvm commands and settings-bound `GamePath` / `LaunchMode` / `UsePortableDataRoot`. Smoke test: enable marks dirty; successful deploy clears dirty.

---

## Deliverables

| Path | Notes |
|------|--------|
| `src/.../ViewModels/MainViewModel.cs` | Commands + properties per brief; RiskGate on enable; dirty vs manifest |
| `src/.../ViewModels/ModItemViewModel.cs` | Checkbox 鈫?`OnModEnabledChanged` |
| `src/.../ViewModels/ProfileItemViewModel.cs` | Profile list item |
| `tests/.../MainViewModelTests.cs` | Temp `PathsService` + Ready game stub |

---

## TDD Evidence

### RED

Wrote `MainViewModelTests.cs` only.

**Result:** FAIL (compile) 鈥?`ViewModels` namespace missing.

### GREEN

Implemented three ViewModels.

**Command:**
```powershell
dotnet test tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests"
```

**Result:** PASS 鈥?1/1

Full suite: 38/38 passed.

---

## Behavior (as implemented)

1. **Enable checkbox:** `RiskGate.CanEnable` then `ProfileService.SetEnabled`; `IsDirty = true`.
2. **ApplyProfile:** `DeployService.Apply`; `IsDirty` recomputed from manifest `profileId`+files vs desired enabled packages.
3. **ApplyAndLaunch:** Apply then `GameLauncher.Launch` if not dirty.
4. **LoaderVersionWarning:** non-empty when any mod `RequiredMelonLoaderVersion` mismatches detected loader version.
5. **RiskBanner:** `RiskGate.BannerText`.
6. **UsePortableDataRoot:** persists `config.DataRoot = Path.Combine(AppContext.BaseDirectory, "data")` (restart to remount `PathsService`).
7. Dialogs (`BrowseGamePath` / import / create name) injected as optional `Func`s for testability.

---

## Concerns

- Portable data root change is persisted only; live `PathsService` root does not swap mid-session.
- Dirty comparison duplicates DeployPlanner path mapping (keep in sync if mapping changes).

---

## Commit

```
feat: main ViewModel for library, profiles, apply flow
```

## Review fix (Important findings)

**Date:** 2026-09-02  
**Commit:** `11a5ec6`

### Changes

1. **ApplyAndLaunch gate:** `ApplyProfile()` now returns `bool`; launch only when apply succeeded **and** `GameStatus.Kind == Ready`. No longer uses `!IsDirty` as launch permission. (`ApplyProfileCommand` is a manual `RelayCommand` wrapper because MVVM Toolkit cannot generate commands from bool-returning methods.)
2. **Shared path mapping:** Removed duplicated `MapRelativeGamePath` from MainViewModel; dirty desired-files use `DeployPlanner.MapRelativeGamePath` (includes UserData `Loader.cfg` reject 鈥?skipped in desired set).
3. **confirmHighRisk default deny:** `confirmHighRisk ?? (_ => false)` so UI must wire a dialog; accidental omit no longer auto-approves high-risk.

### Tests added/adjusted

| Test | Asserts |
|------|---------|
| `ApplyAndLaunch_does_not_launch_when_Apply_fails` | unmanaged collision 鈫?no `IProcessStarter` start |
| `ApplyAndLaunch_does_not_launch_when_game_not_Ready` | bad GamePath 鈫?no launch |
| `HighRisk_enable_cancelled_when_confirm_returns_false` | RiskGate cancel path |
| `Default_confirmHighRisk_denies_high_risk_enable` | null confirm 鈫?deny |

**Command:**
```powershell
dotnet test tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj
```

**Result:** PASS 鈥?42/42 (MainViewModelTests 5/5)

