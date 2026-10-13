# v1.2.8 Hide Console + Mirror Advanced + Deep Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore Hide Melon console + Advanced mirror expander onto 1.2.7 tip, deep-scan/fix blockers, ship **1.2.8** Setup/portable + update channel.

**Architecture:** Surgical port from git `91532b2` / branch `backup/wip-before-release-cleanup-20260914` onto current `master`. Core writes Melon `UserData\Loader.cfg` `[console] hide_console`; UI + `PreferHideConsole` thread through install/dual-sync/CLI. Setup PS1 never invents hide and must not strip existing keys.

**Tech Stack:** .NET 8 WPF, xUnit, FluentAssertions, Inno Setup, existing release/`latest.json` channel.

**Working branch:** `feature/v1.2.8-hide-console-mirror-deep-fix`

## Global Constraints

- Spec: `docs/dev/superpowers/specs/2026-09-16-v128-hide-console-mirror-expand-and-deep-fix-design.md`
- Version target: **1.2.8** (`csproj` + `MechabellumModManager.iss`)
- `HideMelonConsole` default **false**
- Do not blind-merge WIP branch; do not change factory mirror URL unless deep-scan requires it
- Do not stage unrelated dirty files (`packaging/installer/redist/manifest.json`, `.vscode/`, `coscli_output/`)
- Test command: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --nologo`
- Reference commit for port: `91532b2`

---

### Task 1: MelonLoaderConfigOptimizer hide_console + unit tests

**Files:**
- Modify: `src/MechabellumModManager/Services/MelonLoaderConfigOptimizer.cs`
- Modify: `tests/MechabellumModManager.Tests/MelonLoaderConfigOptimizerTests.cs`

**Interfaces:**
- Produces: `SetHideConsole(string gamePath, bool hide) -> MelonLoaderOptimizeResult`
- Produces: `ApplyRecommendedSettings(string gamePath, string? knownUnityVersion = null, bool? hideConsole = null)` — when `hideConsole` is null, leave hide keys untouched; when non-null, write/update

- [ ] **Step 1: Add failing tests** (port from `91532b2` tests): `SetHideConsole_creates_console_section_true`, `SetHideConsole_writes_false_when_key_present`, `SetHideConsole_noop_when_false_and_key_absent`, `ApplyRecommendedSettings_new_cfg_includes_hide_when_requested`, `ApplyRecommendedSettings_null_hide_does_not_add_console_on_new_cfg`, `ApplyRecommendedSettings_null_hide_leaves_existing_hide_unchanged`

- [ ] **Step 2: Run tests — expect FAIL** (missing `SetHideConsole` / overload)

```bash
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~MelonLoaderConfigOptimizerTests" --nologo
```

- [ ] **Step 3: Port optimizer implementation from `91532b2`** — `HideConsoleRegex`, `SetHideConsole`, `ApplyHideConsole`, extend `ApplyRecommendedSettings` and `MinimalLoaderCfg(bool forceOffline, bool? hideConsole)` so null hide does not add `[console]` on new cfg; non-null writes hide; existing force_quit/offline logic unchanged

- [ ] **Step 4: Run same filter — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/MelonLoaderConfigOptimizer.cs tests/MechabellumModManager.Tests/MelonLoaderConfigOptimizerTests.cs
git commit -m "feat: MelonLoader hide_console in Loader.cfg optimizer"
```

---

### Task 2: AppConfig + i18n strings + UiStrings

**Files:**
- Modify: `src/MechabellumModManager/Models/AppConfig.cs` — add `public bool HideMelonConsole { get; set; }`
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs` — add properties for new keys
- Modify: `src/MechabellumModManager/Resources/Strings.resx` (+ `.en` `.de` `.ja` `.ru`) — keys from WIP:
  - `HideMelonConsole`, `HideMelonConsoleTip`, `LogHideMelonConsoleRestartHint`, `LogMelonConsoleHiddenHint`, `LogMelonLaunchVerifyHiddenHint`
  - `SettingsAdvanced`, `MirrorSummaryOnDefault`, `MirrorSummaryCustom`, `MirrorSummaryOff`
- Verify: `tests/MechabellumModManager.Tests/ResourceKeyParityTests.cs` still passes

**Interfaces:**
- Produces: `AppConfig.HideMelonConsole` (default false)
- Produces: string keys listed above in all five resx files

- [ ] **Step 1: Add `HideMelonConsole` to AppConfig** (XML doc from WIP)

- [ ] **Step 2: Add all string keys to five resx + UiStrings accessors** — copy values from `git show 91532b2:.../Strings*.resx` (fix encoding)

- [ ] **Step 3: Run ResourceKeyParityTests**

```bash
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ResourceKeyParity" --nologo
```

Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/MechabellumModManager/Models/AppConfig.cs src/MechabellumModManager/ViewModels/UiStrings.cs src/MechabellumModManager/Resources/Strings*.resx
git commit -m "feat: HideMelonConsole config and i18n strings"
```

---

### Task 3: PreferHideConsole on install / dual-sync / CLI

**Files:**
- Modify: `src/MechabellumModManager/Services/MelonLoaderInstaller.cs` — `public bool? PreferHideConsole { get; set; }` pass into `ApplyRecommendedSettings`
- Modify: `src/MechabellumModManager/Services/MelonLoaderDualStoreSync.cs` — same property; all three optimize call sites
- Modify: `src/MechabellumModManager/Services/InstallMelonLoaderCli.cs` — `LoadHideConsolePreference()` reading `%AppData%\MechabellumModManager\config.json` → `HideMelonConsole`; pass to optimize

**Interfaces:**
- Consumes: Task 1 `ApplyRecommendedSettings(..., hideConsole:)`
- Consumes: Task 2 `AppConfig.HideMelonConsole`
- Produces: `PreferHideConsole` on installer + dual-sync

- [ ] **Step 1: Port PreferHideConsole property + call sites from `91532b2` onto HEAD** (keep HEAD seed/Cpp2IL logic)

- [ ] **Step 2: Port CLI `LoadHideConsolePreference`**

- [ ] **Step 3: Build + run Melon-related tests**

```bash
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~MelonLoader" --nologo
```

Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/MechabellumModManager/Services/MelonLoaderInstaller.cs src/MechabellumModManager/Services/MelonLoaderDualStoreSync.cs src/MechabellumModManager/Services/InstallMelonLoaderCli.cs
git commit -m "feat: thread PreferHideConsole through Melon install paths"
```

---

### Task 4: Settings UI + MainViewModel wiring

**Files:**
- Modify: `src/MechabellumModManager/MainWindow.xaml` — checkbox under portable/check-updates; replace mirror DockPanel (Grid.Row 3) with Expander `IsExpanded="False"` (WIP layout)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`:
  - Load `_hideMelonConsole` / set `_melonDualSync.PreferHideConsole` from config
  - `[ObservableProperty] bool _hideMelonConsole`
  - `OnHideMelonConsoleChanged` → save + `ApplyHideMelonConsolePreference`
  - `EnumerateHideConsoleTargets` (GamePath + dual stores)
  - `MirrorSummaryText` + `RefreshMirrorSummary` on mirror apply / language refresh
  - Pass `HideMelonConsole` into all `ApplyRecommendedSettings` / installer / dual-sync sites
  - Launch logs: if hide → `LogMelonConsoleHiddenHint` + `LogMelonLaunchVerifyHiddenHint`; else existing hints

**Interfaces:**
- Consumes: Tasks 1–3
- Produces: Settings UX per spec sections A–B

- [ ] **Step 1: Port XAML checkbox + Expander from `91532b2` onto HEAD settings grid** (preserve HEAD buttons: ExportDiagnostics, PureGameCleanup, language combo)

- [ ] **Step 2: Port MainViewModel hide + mirror summary + launch hint branching**

- [ ] **Step 3: Build app + run MainViewModel / smoke tests**

```bash
dotnet build src/MechabellumModManager/MechabellumModManager.csproj -c Release --nologo
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel|FullyQualifiedName~MainWindowSmoke|FullyQualifiedName~Mirror" --nologo
```

Expected: build 0 errors; tests PASS

- [ ] **Step 4: Commit**

```bash
git add src/MechabellumModManager/MainWindow.xaml src/MechabellumModManager/ViewModels/MainViewModel.cs
git commit -m "feat: settings HideMelonConsole and Advanced mirror expander"
```

---

### Task 5: Setup PS1 must not strip hide_console

**Files:**
- Modify: `packaging/installer/scripts/Install-MelonLoader.ps1` — in `Apply-LoaderCfgOptimizations`, only touch `force_quit` / `force_offline_generation`; add comment that `[console] hide_console` is manager-owned and must be preserved (current regex already leaves it alone — verify with a quick manual snippet or document-only if already safe)

- [ ] **Step 1: Confirm existing function does not match/remove `hide_console`**; if new-cfg template is written when file missing, do **not** add hide (correct). Add explicit comment in function.

- [ ] **Step 2: Commit** if any change; else note N/A in commit message of Task 6

```bash
git add packaging/installer/scripts/Install-MelonLoader.ps1
git commit -m "docs: Setup Loader.cfg leaves hide_console to the manager"
```

---

### Task 6: Bump version to 1.2.8

**Files:**
- Modify: `src/MechabellumModManager/MechabellumModManager.csproj` — `<Version>1.2.8</Version>`
- Modify: `packaging/installer/MechabellumModManager.iss` — `#define MyAppVersion "1.2.8"`

- [ ] **Step 1: Bump both files**

- [ ] **Step 2: Commit**

```bash
git add src/MechabellumModManager/MechabellumModManager.csproj packaging/installer/MechabellumModManager.iss
git commit -m "chore: bump version to 1.2.8"
```

---

### Task 7: Deep scan + must-fix defects

**Files:** depends on findings (prefer small targeted fixes + tests)

**Coverage checklist (static + tests):**
1. Startup / detect / Melon ready / portable data
2. Catalog / library / update / stale duplicate update
3. Apply & launch / Steam stale-running / hide vs console hints
4. Dual-branch wizard / settle / emergency recover
5. Mirror HTTPS / default COS / fallback
6. Melon install & redist half-install / Loader.cfg coexistence

- [ ] **Step 1: Run full test suite; note failures**

```bash
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --nologo
```

- [ ] **Step 2: Static pass on MainViewModel launch/update/mirror/Melon paths vs recent regressions**

- [ ] **Step 3: Fix must-fix items (crash / wrong result / main-path no-op / data loss / security) with TDD; skip report-only**

- [ ] **Step 4: Write `docs/dev/superpowers/specs/2026-09-16-v128-deep-scan-report.md`** (severity + fixed?)

- [ ] **Step 5: Commit fixes + report**

```bash
git add -A docs/dev/superpowers/specs/2026-09-16-v128-deep-scan-report.md
# plus any fix files
git commit -m "fix: deep-scan must-fix items for v1.2.8"
```

---

### Task 8: Full verification + release artifacts + latest.json

**Files:**
- Create/update: `release/v1.2.8/*`, `release/latest.json`
- Build via: `packaging/installer/build-installer.ps1` (and portable zip per existing 1.2.7 process)
- GitHub Release `v1.2.8` with Setup + portable + SHA256
- Sync COS update channel same as 1.2.7

- [ ] **Step 1: Full `dotnet test` green**

- [ ] **Step 2: Build Setup + portable; record SHA256**

- [ ] **Step 3: Publish GitHub release assets**

- [ ] **Step 4: Update `release/latest.json` notes covering hide console, Advanced mirror, and landed fixes; commit**

```bash
git add release/latest.json release/v1.2.8/
git commit -m "release: point update channel at v1.2.8 Setup and portable"
```

- [ ] **Step 5: Sync COS mirror channel (same tooling as 1.2.7)**

---

## Spec coverage self-check

| Spec item | Task |
|-----------|------|
| Hide Melon console checkbox + Loader.cfg | 1, 2, 4 |
| PreferHide on install/dual/CLI | 3 |
| Launch hint when hidden | 4 |
| Mirror Advanced expander + summary | 2, 4 |
| Setup does not invent/strip hide | 5 |
| Version 1.2.8 | 6 |
| Deep scan + must-fix | 7 |
| Full release channel | 8 |
