# Official / Beta Dual-Folder Switch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a settings-embedded, one-click official/test dual-folder switch (Junction + silent Steam BetaKey when Steam is quit, manual fallback) with per-branch deploy manifests, without changing behavior when the feature is disabled.

**Architecture:** Isolate all Steam/filesystem branch logic in `BranchSwitchService` + `SteamBetaKeyEditor` + extended `ProcessProbe`/`PathsService`. `DeployService` only reads branch-aware manifest paths from `PathsService`. `MainViewModel` orchestrates UI gates (`AwaitingSteamSettle` / wizard) and never lets Deploy/Launcher call Steam editors.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, System.Text.Json, xUnit, FluentAssertions; Win32 `CreateSymbolicLink`/`DeviceIoControl` or `mklink /J` via P/Invoke for junctions; minimal ACF text edit (no full VDF library).

**Spec:** `docs/superpowers/specs/2026-09-04-official-beta-branch-switch-design.md`

## Global Constraints

- Windows x64 only; NTFS same-volume junctions; Steam AppID `669330` only
- Never edit Steam config or swap junctions while `steam` or `Mechabellum` processes are running
- Never recursively delete junction targets when removing a link
- `DeployService` / `GameLauncher` / `ModLibraryService` must not call `SteamBetaKeyEditor`
- Failed or missing `branch-switch.json` must not crash `App` startup; treat as feature disabled
- When feature disabled: existing `deploy-manifest.json` behavior unchanged
- Silent Beta write: backup ACF first; on failure degrade to manual Beta UI path
- Working tree: `<repo-root>` = MechabellumModManager repo

---

## File Structure

```
src/MechabellumModManager/
  Models/
    BranchSwitchConfig.cs          # NEW: config + enums
    DeployManifest.cs              # optional StorePath field later if needed
  Services/
    PathsService.cs                # MOD: branch manifest paths + BranchSwitchPath
    ProcessProbe.cs                # MOD: IsSteamRunning
    JunctionService.cs             # NEW: create/remove/resolve junctions
    SteamBetaKeyEditor.cs          # NEW: backup/read/write BetaKey in appmanifest_669330.acf
    BranchSwitchService.cs         # NEW: wizard + daily switch + journal
    DeployService.cs               # MOD: use PathsService current-branch paths
    DeployPlanner.cs               # unchanged logic; callers pass right manifest
  ViewModels/
    MainViewModel.cs               # MOD: commands, gates, settings bindings
    UiStrings.cs                   # MOD: i18n keys
  Resources/ (or existing loc files) # MOD: zh/en/... strings
  MainWindow.xaml                  # MOD: settings block UI
  App.xaml.cs                      # MOD: compose new services

tests/MechabellumModManager.Tests/
  ProcessProbeTests.cs             # NEW or extend
  PathsServiceBranchTests.cs       # NEW
  JunctionServiceTests.cs          # NEW (temp dirs on NTFS)
  SteamBetaKeyEditorTests.cs       # NEW (fixture ACF text)
  BranchSwitchServiceTests.cs      # NEW
  DeployServiceBranchManifestTests.cs  # NEW
  MainViewModelBranchSwitchTests.cs    # NEW (gates)
```

---

### Task 1: Branch models + PathsService branch manifests

**Files:**
- Create: `src/MechabellumModManager/Models/BranchSwitchConfig.cs`
- Modify: `src/MechabellumModManager/Services/PathsService.cs`
- Test: `tests/MechabellumModManager.Tests/PathsServiceBranchTests.cs`

**Interfaces:**
- Produces:
  - `enum GameBranch { Official, Beta }`
  - `enum BranchWizardStep { None, Declared, ArchivedA, WaitingDownloadB, ArchivedB, Linked, AwaitingSteamSettle, Ready }`
  - `sealed class BranchSwitchConfig` with properties from spec §5.1
  - `PathsService.BranchSwitchConfigPath`
  - `PathsService.GetDeployManifestPath(GameBranch? branch, bool enabled)`
  - `PathsService.GetDeployManifestPrevPath(GameBranch? branch, bool enabled)`

- [ ] **Step 1: Write failing tests**

```csharp
public class PathsServiceBranchTests
{
    [Fact]
    public void When_disabled_uses_legacy_manifest_names()
    {
        var paths = new PathsService(Path.Combine(Path.GetTempPath(), "mmm-p-" + Guid.NewGuid().ToString("N")));
        paths.GetDeployManifestPath(GameBranch.Official, enabled: false)
            .Should().EndWith("deploy-manifest.json");
        paths.GetDeployManifestPrevPath(GameBranch.Beta, enabled: false)
            .Should().EndWith("deploy-manifest.prev.json");
    }

    [Fact]
    public void When_enabled_uses_per_branch_manifest_names()
    {
        var paths = new PathsService(Path.Combine(Path.GetTempPath(), "mmm-p-" + Guid.NewGuid().ToString("N")));
        paths.GetDeployManifestPath(GameBranch.Official, enabled: true)
            .Should().EndWith("deploy-manifest.official.json");
        paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)
            .Should().EndWith("deploy-manifest.beta.json");
        paths.GetDeployManifestPrevPath(GameBranch.Beta, enabled: true)
            .Should().EndWith("deploy-manifest.beta.prev.json");
    }

    [Fact]
    public void BranchSwitchConfigPath_is_under_DataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-p-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(root);
        paths.BranchSwitchConfigPath.Should().Be(Path.Combine(root, "branch-switch.json"));
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL** (methods missing)

```bash
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter PathsServiceBranchTests
```

- [ ] **Step 3: Implement models + PathsService methods**

```csharp
// Models/BranchSwitchConfig.cs
namespace MechabellumModManager.Models;

public enum GameBranch { Official, Beta }

public enum BranchWizardStep
{
    None, Declared, ArchivedA, WaitingDownloadB, ArchivedB, Linked, AwaitingSteamSettle, Ready
}

public sealed class BranchSwitchConfig
{
    public bool Enabled { get; set; }
    public BranchWizardStep WizardStep { get; set; } = BranchWizardStep.None;
    public string SteamLinkPath { get; set; } = "";
    public string OfficialStorePath { get; set; } = "";
    public string BetaStorePath { get; set; } = "";
    public GameBranch ActiveBranch { get; set; } = GameBranch.Official;
    public string OfficialProfileId { get; set; } = "default";
    public string BetaProfileId { get; set; } = "default";
    public string BetaBranchName { get; set; } = "";
    public string? ManifestBackupPath { get; set; }
}
```

```csharp
// PathsService additions
public string BranchSwitchConfigPath => Path.Combine(DataRoot, "branch-switch.json");
public string BranchSwitchJournalPath => Path.Combine(DataRoot, "branch-switch-journal.json");

public string GetDeployManifestPath(GameBranch? branch, bool enabled)
{
    if (!enabled || branch is null)
        return DeployManifestPath;
    return branch == GameBranch.Official
        ? Path.Combine(DataRoot, "deploy-manifest.official.json")
        : Path.Combine(DataRoot, "deploy-manifest.beta.json");
}

public string GetDeployManifestPrevPath(GameBranch? branch, bool enabled)
{
    if (!enabled || branch is null)
        return DeployManifestPrevPath;
    return branch == GameBranch.Official
        ? Path.Combine(DataRoot, "deploy-manifest.official.prev.json")
        : Path.Combine(DataRoot, "deploy-manifest.beta.prev.json");
}
```

Keep existing `DeployManifestPath` / `DeployManifestPrevPath` properties for backward compatibility.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Models/BranchSwitchConfig.cs src/MechabellumModManager/Services/PathsService.cs tests/MechabellumModManager.Tests/PathsServiceBranchTests.cs
git commit -m "feat: add branch-switch config model and per-branch manifest paths"
```

---

### Task 2: ProcessProbe Steam detection

**Files:**
- Modify: `src/MechabellumModManager/Services/ProcessProbe.cs`
- Test: `tests/MechabellumModManager.Tests/ProcessProbeTests.cs`

**Interfaces:**
- Produces: `bool IsSteamRunning()`, `bool IsGameOrSteamRunning()`

- [ ] **Step 1: Write failing test**

```csharp
public class ProcessProbeTests
{
    [Fact]
    public void IsGameOrSteamRunning_matches_individual_flags()
    {
        var probe = new ProcessProbe();
        // Cannot assert true without running Steam; assert API exists and is consistent:
        (probe.IsGameRunning() || probe.IsSteamRunning()).Should().Be(probe.IsGameOrSteamRunning());
    }
}
```

- [ ] **Step 2: Run — FAIL until methods exist**

- [ ] **Step 3: Implement**

```csharp
public bool IsSteamRunning()
{
    // Steam main process name is "steam" on Windows
    var processes = Process.GetProcessesByName("steam");
    try { return processes.Length > 0; }
    finally { foreach (var p in processes) p.Dispose(); }
}

public bool IsGameOrSteamRunning() => IsGameRunning() || IsSteamRunning();
```

- [ ] **Step 4: PASS + Commit**

```bash
git commit -m "feat: detect Steam process for branch-switch gate"
```

---

### Task 3: JunctionService

**Files:**
- Create: `src/MechabellumModManager/Services/JunctionService.cs`
- Test: `tests/MechabellumModManager.Tests/JunctionServiceTests.cs`

**Interfaces:**
- Produces:
  - `bool IsJunction(string path)`
  - `string? ResolveTarget(string path)` — null if not junction
  - `void CreateJunction(string linkPath, string targetPath)` — linkPath must not exist
  - `void DeleteJunction(string linkPath)` — removes link only, never deletes target contents

- [ ] **Step 1: Failing integration test on temp NTFS dir**

```csharp
public class JunctionServiceTests
{
    [Fact]
    public void Create_resolve_delete_preserves_target()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-j-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "Mechabellum.exe"), "x");
        var link = Path.Combine(root, "Mechabellum");
        var sut = new JunctionService();

        sut.CreateJunction(link, target);
        sut.IsJunction(link).Should().BeTrue();
        sut.ResolveTarget(link).Should().Be(Path.GetFullPath(target));
        File.Exists(Path.Combine(link, "Mechabellum.exe")).Should().BeTrue();

        sut.DeleteJunction(link);
        Directory.Exists(link).Should().BeFalse();
        File.Exists(Path.Combine(target, "Mechabellum.exe")).Should().BeTrue();
    }
}
```

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Implement via P/Invoke `CreateSymbolicLinkW` with `SYMBOLIC_LINK_FLAG_DIRECTORY` **or** documented fallback: run `cmd /c mklink /J` with escaped paths (prefer P/Invoke in `JunctionService`). Detect reparse with `File.GetAttributes` + `ReparsePoint` and `DeviceIoControl`/`Marshal` as needed. On non-NTFS, throw clear `InvalidOperationException`.

- [ ] **Step 4: PASS + Commit**

```bash
git commit -m "feat: add JunctionService for dual-folder game link"
```

---

### Task 4: SteamBetaKeyEditor (isolated)

**Files:**
- Create: `src/MechabellumModManager/Services/SteamBetaKeyEditor.cs`
- Test: `tests/MechabellumModManager.Tests/SteamBetaKeyEditorTests.cs`
- Create fixture text under test project or inline strings

**Interfaces:**
- Consumes: `ProcessProbe` (refuse if Steam running)
- Produces:
  - `string FindAppManifestPath(string steamLinkGamePath)` → `..\..\appmanifest_669330.acf` from `...\common\Mechabellum`
  - `SteamBetaEditResult BackupAndSetBetaKey(string acfPath, string? betaKey, string backupDir)`  
    - `betaKey` null/empty = official (remove or empty BetaKey in UserConfig)
  - `string? ReadBetaKey(string acfPath)`
  - Never touches non-669330 filenames

- [ ] **Step 1: Failing tests with sample ACF**

```csharp
const string SampleAcf = """
"AppState"
{
	"appid"		"669330"
	"UserConfig"
	{
		"language"		"english"
	}
}
""";

[Fact]
public void SetBetaKey_inserts_under_UserConfig()
{
    var dir = Path.Combine(Path.GetTempPath(), "mmm-acf-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var acf = Path.Combine(dir, "appmanifest_669330.acf");
    File.WriteAllText(acf, SampleAcf);
    var probe = new FakeProbe { SteamRunning = false };
    var editor = new SteamBetaKeyEditor(probe);

    var result = editor.BackupAndSetBetaKey(acf, "publicbeta", Path.Combine(dir, "bak"));
    result.Success.Should().BeTrue();
    editor.ReadBetaKey(acf).Should().Be("publicbeta");
    Directory.GetFiles(Path.Combine(dir, "bak")).Should().NotBeEmpty();
}

[Fact]
public void Refuses_when_steam_running()
{
    var editor = new SteamBetaKeyEditor(new FakeProbe { SteamRunning = true });
    var result = editor.BackupAndSetBetaKey("x", "y", "z");
    result.Success.Should().BeFalse();
}

[Fact]
public void FindAppManifest_rejects_wrong_appid_name()
{
    var game = @"D:\SteamLibrary\steamapps\common\Mechabellum";
    var path = SteamBetaKeyEditor.FindAppManifestPath(game);
    Path.GetFileName(path).Should().Be("appmanifest_669330.acf");
}
```

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Minimal ACF editor** — locate `"UserConfig"` block; set `"BetaKey" "name"` or remove BetaKey line for official; preserve other keys; UTF-8; throw if appid line is not 669330 when present.

- [ ] **Step 4: PASS + Commit**

```bash
git commit -m "feat: add SteamBetaKeyEditor with backup for app 669330"
```

---

### Task 5: BranchSwitchService (core FS + journal)

**Files:**
- Create: `src/MechabellumModManager/Services/BranchSwitchService.cs`
- Create: `src/MechabellumModManager/Models/BranchSwitchJournal.cs` (optional nested type)
- Test: `tests/MechabellumModManager.Tests/BranchSwitchServiceTests.cs`

**Interfaces:**
- Consumes: `PathsService`, `JsonStore`, `ProcessProbe`, `JunctionService`, `SteamBetaKeyEditor`
- Produces:
  - `BranchSwitchConfig LoadConfig()` / `SaveConfig`
  - `BranchOperationResult TrySwapJunction(GameBranch target)` — Steam must be quit; no Beta write
  - `BranchOperationResult TrySilentSetBeta(GameBranch target)` — uses `BetaBranchName`
  - `BranchOperationResult TryRepairFromJournal()`
  - Wizard step helpers: `ArchiveCurrentAs(GameBranch)`, `ArchiveDownloadedAs(GameBranch)`, `CreateLinkTo(GameBranch)` matching spec §2.1 order
  - `MigrateLegacyManifestIfNeeded(GameBranch current)` copies `deploy-manifest.json` → branch file once

```csharp
public sealed class BranchOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool DegradeToManualBeta { get; init; }
}
```

- [ ] **Step 1: Tests** — fake probe + temp dirs:
  - Swap junction official↔beta preserves both store folders
  - Refuse swap when steam running
  - DeleteJunction does not delete store
  - Journal written between unlink and relink; `TryRepairFromJournal` restores link

- [ ] **Step 2–4: Implement + PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add BranchSwitchService junction swap and journal repair"
```

---

### Task 6: DeployService uses branch-aware paths

**Files:**
- Modify: `src/MechabellumModManager/Services/DeployService.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (dirty check paths) — minimal in this task if possible; else Task 7
- Test: `tests/MechabellumModManager.Tests/DeployServiceBranchManifestTests.cs`
- Modify existing tests if they break (keep legacy path when disabled)

**Interfaces:**
- Change `DeployService` to accept optional `Func<(GameBranch? branch, bool enabled)> branchState` **or** pass explicit manifest paths into `Apply` overload:

```csharp
public DeployResult Apply(
    Profile profile,
    IReadOnlyDictionary<string, ModPackage> packages,
    string gamePath,
    bool allowOverwriteUnmanaged,
    string? manifestPath = null,
    string? manifestPrevPath = null)
```

When null, use legacy `PathsService.DeployManifestPath`. When branch enabled, caller passes `GetDeployManifestPath(...)`.

- [ ] **Step 1: Test** — enable branch mode; Apply to official path writes `deploy-manifest.official.json` not legacy file; second Apply on beta uses beta file and does not delete official store files (use two temp game roots simulating stores via junctions or plain folders).

- [ ] **Step 2–4: Implement overload + update call sites carefully so default tests still pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: deploy manifests per official/beta branch"
```

---

### Task 7: MainViewModel orchestration + gates

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`
- Modify: `src/MechabellumModManager/App.xaml.cs` (DI compose)
- Test: `tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs`

**Interfaces:**
- New commands: `SwitchToOfficialCommand`, `SwitchToBetaCommand`, `StartBranchWizardCommand`, `TeardownBranchSwitchCommand`, `ConfirmManualBetaCommand`
- Properties: `BranchSwitchEnabled`, `ActiveGameBranch`, `BetaBranchName`, `OfficialProfileId`, `BetaProfileId`, `BranchStatusText`, `IsBranchSwitchBusy`, `IsAwaitingSteamSettle`
- `CanDeployOrLaunch` => `IsReady && !IsAwaitingSteamSettle && !IsBranchWizardBlocking`
- On `SelectedProfile` change while enabled: write binding for `ActiveBranch`
- Load `branch-switch.json` in ctor via try/catch — never throw

Daily switch flow in command (spec §2.2):

1. Confirm user OK to require Steam exit  
2. If steam running → try notify / `steam://exit` wait loop (max ~30s) or abort  
3. `TrySwapJunction`  
4. `TrySilentSetBeta` → if fail set `DegradeToManualBeta` UI  
5. Start Steam (`steam://open/games` or shell Steam)  
6. Set `AwaitingSteamSettle`  
7. On success path / user confirm: Deploy bound profile with branch manifest paths; clear settle  

- [ ] **Step 1: Tests** with fakes — settle state disables ApplyAndLaunch; disabled feature does not change existing Ready behavior

- [ ] **Step 2–4: Wire + PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: orchestrate branch switch commands and launch gates"
```

---

### Task 8: Settings UI + i18n

**Files:**
- Modify: `src/MechabellumModManager/MainWindow.xaml` (inside settings panel after game path / near portable checkbox ~line 262)
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs` + existing localization dictionaries used by `LocalizationService.T`

**UI block (bindings only):**
- Status text, BetaBranchName TextBox, two Profile ComboBoxes (ItemsSource=Profiles), Switch Official/Beta buttons, Wizard / Teardown buttons, manual-confirm button visible when degraded

- [ ] **Step 1: Add all string keys** (zh-CN + en minimum; mirror other langs with English fallback if project pattern allows)

- [ ] **Step 2: XAML section** — follow existing settings styles (no new design system)

- [ ] **Step 3: Manual smoke** — launch app, expand settings, see block; with feature disabled switching stays disabled

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add official/beta switch settings UI and strings"
```

---

### Task 9: Wizard UX completion + migration + docs touchpoint

**Files:**
- Modify: `BranchSwitchService` / `MainViewModel` wizard step machine
- Modify: `docs/分发-使用说明.md` — short subsection only if user-facing install docs already describe settings (keep minimal)
- Test: wizard archive order test (empty Steam link path after archive A)

- [ ] **Step 1: Implement wizard steps 0–8** per spec with message-box prompts between Steam download waits (`WaitingDownloadB`)

- [ ] **Step 2: `MigrateLegacyManifestIfNeeded`** on first enable

- [ ] **Step 3: Regression** — `dotnet test` full suite green; feature-off paths unchanged

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: complete dual-folder wizard and legacy manifest migration"
```

---

## Spec coverage checklist (self-review)

| Spec item | Task |
|-----------|------|
| §0 isolation / no startup crash | 4, 5, 7 |
| Silent Beta + backup + degrade | 4, 7 |
| Steam quit gate | 2, 4, 5, 7 |
| Junction dual folders / no recursive delete | 3, 5 |
| Wizard empty path before download B | 5, 9 |
| Per-branch manifest + prev | 1, 6 |
| Profile binding + Deploy after settle | 7 |
| Settings UI + i18n | 8 |
| AwaitingSteamSettle disables launch | 7 |
| Teardown / rebuild | 5, 9 |
| AppID 669330 only | 4 |

**Placeholder scan:** none intentional.  
**Type consistency:** `GameBranch`, `BranchWizardStep`, `BranchOperationResult`, `GetDeployManifestPath(branch, enabled)` used throughout.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-04-official-beta-branch-switch.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — execute tasks in this session with executing-plans checkpoints  

Which approach?
