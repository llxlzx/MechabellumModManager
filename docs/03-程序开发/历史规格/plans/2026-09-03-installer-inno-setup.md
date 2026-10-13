# Inno Setup Installer + Remove In-App MelonLoader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship an Inno Setup Chinese installer that installs the mod manager, optionally installs .NET 8/6 Desktop runtimes and MelonLoader (hybrid local/online), captures game path into config; remove in-app one-click MelonLoader install.

**Architecture:** Publish framework-dependent single-file manager as today. Inno Setup (`installer/*.iss`) copies app + Assets, runs PowerShell helpers for runtime detect/install and MelonLoader zip extract. Manager UI drops MelonLoader install command/progress; installer owns prereq installation. `MelonLoaderConfigOptimizer` remains in C# for tests; installer duplicates minimal Loader.cfg tweaks in PowerShell.

**Tech Stack:** Inno Setup 6, PowerShell 5+, .NET 8 WPF manager, existing Steam/Locator patterns.

## Global Constraints

- MelonLoader component default checked, optional
- Hybrid: `installer/redist/` first, else official Microsoft/GitHub URLs
- Admin elevation required
- No third-party mods in package
- Chinese wizard copy
- Remove in-app MelonLoader one-click UI completely
- Write `%AppData%\MechabellumModManager\config.json` gamePath on install

---

### Task 1: Remove in-app MelonLoader install UI

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`
- Modify: `src/MechabellumModManager/MainWindow.xaml`
- Modify: `src/MechabellumModManager/App.xaml.cs`
- Modify: `src/MechabellumModManager/Services/GameDetector.cs` (loader-missing message)
- Modify: `README.md`, `docs/分发-使用说明.txt`
- Keep: `MelonLoaderInstaller.cs` + tests (unused by UI; may be deleted later)
- Keep: `MelonLoaderConfigOptimizer` (still used by manager on status refresh)

**Interfaces:**
- Produces: Manager with no MelonLoader install entry points
- Missing-loader message mentions installer / manual install

- [ ] **Step 1:** Update GameDetector missing/partial loader messages
- [ ] **Step 2:** Remove MelonLoader install command, progress props, installer field from MainViewModel
- [ ] **Step 3:** Remove install button + progress row from MainWindow.xaml; fix grid rows
- [ ] **Step 4:** Clean App.xaml.cs; update docs
- [ ] **Step 5:** `dotnet test` PASS; commit `refactor: remove in-app MelonLoader one-click install`

---

### Task 2: Installer PowerShell helpers

**Files:**
- Create: `installer/scripts/Detect-DotNetDesktop.ps1`
- Create: `installer/scripts/Install-Prereqs.ps1`
- Create: `installer/scripts/Install-MelonLoader.ps1`
- Create: `installer/scripts/Write-ManagerConfig.ps1`
- Create: `installer/redist/README.md`

**Interfaces:**
- Detect exits 0 if Desktop runtime major present
- Install-Prereqs: local redist then download; `/install /quiet /norestart`; 0/3010 OK
- Install-MelonLoader: unzip to game root + Loader.cfg optimizes
- Write-ManagerConfig: merge gamePath into AppData config.json

- [ ] **Step 1–4:** Implement four scripts + redist README
- [ ] **Step 5:** Commit `feat: add installer PowerShell prereq helpers`

---

### Task 3: Inno Setup project

**Files:**
- Create: `installer/MechabellumModManager.iss`
- Create: `installer/build-installer.bat`
- Modify: `.gitignore`, `README.md`

- [ ] **Step 1:** `.iss` Chinese UI, admin, files from `../发布`
- [ ] **Step 2:** Custom game-path page + component checkboxes (melon default on)
- [ ] **Step 3:** Call PowerShell after file copy
- [ ] **Step 4:** build-installer.bat + gitignore large redist binaries
- [ ] **Step 5:** Commit `feat: add Inno Setup installer project`

---

### Task 4: Publish smoke + docs

- [ ] **Step 1:** Full `dotnet test`
- [ ] **Step 2:** Publish + ISCC if available; else document installing Inno Setup 6
- [ ] **Step 3:** Update 使用说明 for Setup.exe; commit if needed

---

## Spec coverage

| Spec item | Task |
|-----------|------|
| Remove in-app MelonLoader | 1 |
| Hybrid .NET / Melon | 2–3 |
| Melon default optional | 3 |
| Game path + config | 2–3 |
| Admin / Chinese | 3 |
| Empty library | 3–4 |
