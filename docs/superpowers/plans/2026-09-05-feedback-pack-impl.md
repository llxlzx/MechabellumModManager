# Feedback Pack + Bug Preprocess Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Phase 0–3 of the approved design: Melon assemblies auto-generate (B), wizard Access Denied + UX, installer EULA, then main UI multi-select/collapse/Apply, then release 1.0.8.

**Architecture:** Tighten `GameDetector` so Ready requires Il2Cpp assemblies; dual-store sync still copies framework only (skip Latest.log); new `MelonLoaderAssemblyGenerator` starts game exe once and polls for `Assembly-CSharp.dll`. Wizard/Confirm UX and installer License are independent layers on top.

**Tech Stack:** .NET 8 WPF, xUnit/FluentAssertions, Inno Setup 6

## Global Constraints

- Order: Phase 0 → 1 → 2 → 3; do not merge Phase 2 before Phase 0 green.
- Melon scheme B: manager triggers/waits generation; do not copy Il2CppAssemblies across stores.
- Confirm buttons: **Yes left, No right**.
- Release default: **1.0.8**.
- TDD for behavior changes; no silent UAC elevation.

---

### Task 1: Tighten GameDetector Ready

**Files:**
- Modify: `src/MechabellumModManager/Models/GameStatus.cs`
- Modify: `src/MechabellumModManager/Services/GameDetector.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (status text)
- Test: `tests/MechabellumModManager.Tests/GameDetectorTests.cs`
- Update callers expecting Ready without assemblies: `MelonLoaderDualStoreSyncTests`, `MelonLoaderInstallerTests`, fixtures

**Interfaces:**
- Produces: `GameStatusKind.LoaderPresentAssembliesMissing`
- Ready iff Melon dir + proxy + `MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll`

- [ ] Step 1: Failing test — framework-only → AssembliesMissing; with Assembly-CSharp.dll → Ready
- [ ] Step 2: Implement enum + detector
- [ ] Step 3: Fix dependent tests/fixtures to seed assemblies or expect AssembliesMissing
- [ ] Step 4: `dotnet test --filter GameDetector`

---

### Task 2: Dual-store sync skip Latest.log + accept AssembliesMissing as install OK

**Files:**
- Modify: `MelonLoaderDualStoreSync.cs`
- Test: `MelonLoaderDualStoreSyncTests.cs`

- [ ] Skip copying files named `Latest.log`
- [ ] Post-install success = Ready **or** LoaderPresentAssembliesMissing
- [ ] Tests green

---

### Task 3: MelonLoaderAssemblyGenerator (scheme B)

**Files:**
- Create: `Services/MelonLoaderAssemblyGenerator.cs`
- Modify: `MainViewModel.cs` (switch/launch/sync hooks)
- Test: generator polling/timeout with fake clock/fs (no real game in unit test)

- [ ] Start Mechabellum.exe from store; poll Assembly-CSharp.dll stable; timeout 180s; kill only started PID
- [ ] Wire after dual sync / before ApplyAndLaunch when AssembliesMissing
- [ ] Session: at most one auto-attempt per store path

---

### Task 4: Wizard Access Denied mapping + writable probe

**Files:**
- Modify: `BranchSwitchService.cs` (`ArchiveCurrentAs` / `ArchiveDownloadedAs`)
- Test: unit test mapping helper / probe failure message

---

### Task 5: Confirm Yes|No order + branch pick dialog

**Files:**
- Modify: `ConfirmDialog.xaml` (Yes left, No right)
- Create or extend dialog for Official|Beta pick (do not overload Mod TypePickDialog)
- Modify: `MainViewModel.RunWizardFromStartAsync` + App confirm injection if needed
- Resx: prompt strings zh/en (+ other locales best-effort)

---

### Task 6: Installer EULA (Phase 1)

**Files:**
- Create: `installer/EULA.zh-CN.txt`
- Modify: `installer/MechabellumModManager.iss` (`LicenseFile=`)

---

### Task 7: Main UI (Phase 2)

**Files:**
- `MainWindow.xaml`, `MainViewModel.cs`, resx

- Multi-select Extended; catalog collapse chevron + border; move Apply; gray→accent when applicable

---

### Task 8: Regression + release 1.0.8 (Phase 3)

- Bump version csproj + iss; build-installer; upload Release; tests green

---
