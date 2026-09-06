# Critical-op guard + layered update/close Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent unsafe manager updates/closes during disk-critical work, warn during Steam settle, and force a startup continue/repair modal after crash/kill mid-write.

**Architecture:** Add `CriticalOpGuard` (durable `critical-op.json` + in-memory Running). Classify gates as Hard / Soft / Allow. Wire update + window-close through the classifier; wrap branch/Melon/orphan disk segments with Begin/Complete; show a recovery modal when Interrupted. Enhance Inno Files-in-Use copy. No durable marker for ordinary deploy (Busy only).

**Tech Stack:** .NET 8 WPF, xUnit + FluentAssertions, Inno Setup, existing `PathsService` / `JsonStore` / `MainViewModel` / `App.xaml.cs`.

**Spec:** `docs/superpowers/specs/2026-09-05-critical-op-guard-design.md`

## Global Constraints

- Layered gates: hard on disk-critical; soft confirm on settle / waiting-download; never hard-block settle-only from updating forever.
- Settle wait does **not** write `critical-op.json`.
- Deploy profile → Mods: Busy/`_blockMainClose` only; **no** durable marker in v1.
- Startup recovery is **modal** until Continue or Repair path chosen.
- Keep Steam-open settle snapshot behavior from settle-confirm polish (do not re-block snapshot on Steam running).
- i18n: add keys to `Strings.resx` + `Strings.en.resx` at minimum; ja/ru/de get English fallback if needed.
- Ship notes target **v1.1.2**.

---

## File structure

| File | Responsibility |
|------|----------------|
| `src/.../Models/CriticalOpKind.cs` | Enum kinds |
| `src/.../Models/CriticalOpMarker.cs` | JSON DTO |
| `src/.../Services/CriticalOpGuard.cs` | Begin / Complete / LoadInterrupted / Clear / `IsRunning` |
| `src/.../Services/CriticalOpGate.cs` | Pure classifier → `CriticalOpGateLevel` |
| `src/.../Services/PathsService.cs` | `CriticalOpMarkerPath` |
| `src/.../ViewModels/MainViewModel.cs` | Inject guard; gate update; recovery flags; Begin/Complete wrappers |
| `src/.../App.xaml.cs` | Close handler uses VM gate; inject guard; optional recovery dialog host |
| `src/.../Dialogs/CriticalOpRecoveryDialog.xaml(+.cs)` | Modal Continue / Repair / (optional diagnostics) |
| `src/.../Resources/Strings*.resx` | Block/soft/recovery strings |
| `installer/MechabellumModManager.iss` + `installer/ChineseSimplified.isl` (and EN) | Files-in-use / close-apps guidance |
| `tests/.../CriticalOpGuardTests.cs` | Marker lifecycle |
| `tests/.../CriticalOpGateTests.cs` | Hard/Soft/Allow matrix |
| `tests/.../MainViewModelCriticalOpTests.cs` | Update + recovery integration |

---

### Task 1: CriticalOpGuard + path (TDD)

**Files:**
- Create: `src/MechabellumModManager/Models/CriticalOpKind.cs`
- Create: `src/MechabellumModManager/Models/CriticalOpMarker.cs`
- Create: `src/MechabellumModManager/Services/CriticalOpGuard.cs`
- Modify: `src/MechabellumModManager/Services/PathsService.cs` (add property)
- Test: `tests/MechabellumModManager.Tests/CriticalOpGuardTests.cs`

**Interfaces:**
- Consumes: `PathsService.DataRoot`, `JsonStore` (or raw File IO — prefer File + System.Text.Json like journal for atomicity simplicity)
- Produces:
  - `PathsService.CriticalOpMarkerPath` → `Path.Combine(DataRoot, "critical-op.json")`
  - `enum CriticalOpKind { None, BranchDiskWrite, MelonInstall, OrphanRepair }`
  - `class CriticalOpMarker { CriticalOpKind Kind; DateTimeOffset StartedUtc; string Detail; int Pid; }`
  - `CriticalOpGuard`:
    - `bool IsRunning { get; }`
    - `CriticalOpKind RunningKind { get; }`
    - `void Begin(CriticalOpKind kind, string detail)`
    - `void Complete()`
    - `bool TryLoadInterrupted(out CriticalOpMarker? marker)` — true if file exists and process is **not** currently Running (startup); if `IsRunning`, treat as current op not Interrupted
    - `void ClearInterrupted()` — delete file only when `!IsRunning`

- [ ] **Step 1: Write failing tests**

```csharp
public class CriticalOpGuardTests
{
    [Fact]
    public void Begin_writes_marker_and_sets_IsRunning()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        var guard = new CriticalOpGuard(paths);

        guard.Begin(CriticalOpKind.BranchDiskWrite, "SwitchToBeta");

        guard.IsRunning.Should().BeTrue();
        File.Exists(paths.CriticalOpMarkerPath).Should().BeTrue();
        var json = File.ReadAllText(paths.CriticalOpMarkerPath);
        json.Should().Contain("BranchDiskWrite");
        json.Should().Contain("SwitchToBeta");
    }

    [Fact]
    public void Complete_deletes_marker_and_clears_IsRunning()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        var guard = new CriticalOpGuard(paths);
        guard.Begin(CriticalOpKind.MelonInstall, "Install");
        guard.Complete();

        guard.IsRunning.Should().BeFalse();
        File.Exists(paths.CriticalOpMarkerPath).Should().BeFalse();
    }

    [Fact]
    public void TryLoadInterrupted_true_when_leftover_file_and_not_running()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        Directory.CreateDirectory(paths.DataRoot);
        File.WriteAllText(paths.CriticalOpMarkerPath, """
            {"kind":"BranchDiskWrite","startedUtc":"2026-09-05T12:00:00+00:00","detail":"x","pid":1}
            """);

        var guard = new CriticalOpGuard(paths);
        guard.TryLoadInterrupted(out var marker).Should().BeTrue();
        marker!.Kind.Should().Be(CriticalOpKind.BranchDiskWrite);
    }

    [Fact]
    public void ClearInterrupted_removes_stale_file()
    {
        using var tmp = new TempDir();
        var paths = new PathsService(tmp.Path);
        var guard = new CriticalOpGuard(paths);
        guard.Begin(CriticalOpKind.OrphanRepair, "repair");
        guard.Complete(); // ensure clean
        File.WriteAllText(paths.CriticalOpMarkerPath, """{"kind":"OrphanRepair","startedUtc":"2026-09-05T12:00:00+00:00","detail":"stale","pid":1}""");

        guard.ClearInterrupted();
        File.Exists(paths.CriticalOpMarkerPath).Should().BeFalse();
    }
}
```

Use whatever temp helper the test project already uses (copy pattern from `SteamAcfSnapshotSanitizerTests` / Fixture `IDisposable` temp root). If no shared `TempDir`, inline `Path.Combine(Path.GetTempPath(), Guid...)` + delete on dispose.

- [ ] **Step 2: Run tests — expect FAIL**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~CriticalOpGuardTests"
```

Expected: compile fail or missing type.

- [ ] **Step 3: Implement minimal Guard + path**

`PathsService`:

```csharp
public string CriticalOpMarkerPath => Path.Combine(DataRoot, "critical-op.json");
```

`CriticalOpGuard.Begin`: set in-memory Running; ensure DataRoot; serialize marker with `Environment.ProcessId`, `DateTimeOffset.UtcNow`, kind, detail. On IO failure: still set in-memory Running (fail-closed for gates); log via optional `Action<string>? log` or swallow for pure service (VM logs).

`Complete`: clear in-memory; best-effort delete file.

`TryLoadInterrupted`: if `IsRunning` return false; else if file exists deserialize (tolerant) return true; corrupt file → treat as Interrupted with `Kind=BranchDiskWrite`, `Detail="corrupt"`.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit** (only if user asked for commits in session; otherwise skip)

```bash
git add src/MechabellumModManager/Models/CriticalOpKind.cs src/MechabellumModManager/Models/CriticalOpMarker.cs src/MechabellumModManager/Services/CriticalOpGuard.cs src/MechabellumModManager/Services/PathsService.cs tests/MechabellumModManager.Tests/CriticalOpGuardTests.cs
git commit -m "feat: add CriticalOpGuard durable marker"
```

---

### Task 2: Gate classifier (TDD)

**Files:**
- Create: `src/MechabellumModManager/Services/CriticalOpGate.cs`
- Test: `tests/MechabellumModManager.Tests/CriticalOpGateTests.cs`

**Interfaces:**
- Consumes: booleans from VM/App
- Produces:

```csharp
public enum CriticalOpGateLevel { Allow, SoftConfirm, HardBlock }

public static class CriticalOpGate
{
    public static CriticalOpGateLevel Classify(
        bool guardRunning,
        bool busyDialogOpen,      // App _blockMainClose / busy
        bool branchSwitchBusy,
        bool recoveryModalPending,
        bool awaitingSteamSettle,
        bool wizardWaitingSteam)  // WaitingDownloadB (and similar wait-only)
}
```

Rules (exact):

1. `recoveryModalPending` → `HardBlock`
2. `guardRunning || busyDialogOpen || branchSwitchBusy` → `HardBlock`
3. `awaitingSteamSettle || wizardWaitingSteam` → `SoftConfirm`
4. else → `Allow`

- [ ] **Step 1: Write matrix tests** covering each row above (at least 6 facts).

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement `Classify`**

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit** (if requested)

---

### Task 3: i18n strings

**Files:**
- Modify: `src/MechabellumModManager/Resources/Strings.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.en.resx`
- Modify: `Strings.ja.resx`, `Strings.ru.resx`, `Strings.de.resx` (EN fallback OK)
- Modify: `ViewModels/UiStrings.cs` only if UI binds new properties; recovery dialog can use `LocalizationService.T` directly

**Keys (exact names):**

| Key | zh intent |
|-----|-----------|
| `CriticalOpBlockUpdate` | 正在进行写盘/关键操作，请完成后再检查更新。 |
| `CriticalOpBlockClose` | 正在进行写盘/关键操作，请勿关闭管理器。 |
| `CriticalOpSoftUpdateConfirm` | 当前仍在等待 Steam 切换/下载（{0}）。更新安装程序可能中断流程；确认仍要打开下载链接？ |
| `CriticalOpSoftCloseConfirm` | 当前仍在等待 Steam（{0}）。关闭后可稍后继续结算/向导；确认关闭？ |
| `CriticalOpRecoveryTitle` | 检测到未完成的关键操作 |
| `CriticalOpRecoveryBody` | 上次切换模式/安装 Melon/修复可能被中断。请先继续未完成步骤，或运行修复。 |
| `CriticalOpRecoveryContinue` | 继续未完成操作 |
| `CriticalOpRecoveryRepair` | 运行修复 / 安全清理 |
| `CriticalOpRecoveryDiagnostics` | 打开诊断（不解除限制） |

- [ ] **Step 1: Add keys** via existing XmlDocument/resx edit pattern used in this repo.

- [ ] **Step 2: Build** `dotnet build src/MechabellumModManager/MechabellumModManager.csproj` — PASS

---

### Task 4: Wire update gate + close gate in VM/App

**Files:**
- Modify: `App.xaml.cs` — construct `CriticalOpGuard`, pass into `MainViewModel`; replace close handler
- Modify: `MainViewModel.cs` — ctor param `CriticalOpGuard? criticalOp = null`; properties + methods below
- Test: `tests/.../MainViewModelCriticalOpTests.cs`

**Interfaces produced on VM:**

```csharp
public bool IsCriticalOpRunning => _criticalOp.IsRunning;
public bool IsRecoveryGateActive { get; private set; }  // until continue/repair resolves
public bool IsWizardWaitingSteam =>
    BranchWizardStep is BranchWizardStep.WaitingDownloadB;

public CriticalOpGateLevel EvaluateCloseOrUpdateGate(bool busyDialogOpen)
{
    return CriticalOpGate.Classify(
        _criticalOp.IsRunning,
        busyDialogOpen,
        IsBranchSwitchBusy,
        IsRecoveryGateActive,
        IsAwaitingSteamSettle,
        IsWizardWaitingSteam);
}

// For App close:
public bool TryHandleWindowClosing(bool busyDialogOpen, out bool cancel)
```

`TryHandleWindowClosing` logic:

- Hard → `_notify(CriticalOpBlockClose)`; `cancel=true`; return true (handled)
- Soft → if `!_confirm(CriticalOpSoftCloseConfirm formatted)` then `cancel=true`; else `cancel=false`
- Allow → `cancel=false`

`CheckForUpdatesAsync` — **before** opening URL (and ideally before check network if Hard, to avoid useless work):

```csharp
var level = EvaluateCloseOrUpdateGate(busyDialogOpen: false);
// Note: Busy dialog already implies user can't click update easily; still classify branch busy/guard.
if (level == CriticalOpGateLevel.HardBlock)
{
    _notify(LocalizationService.T("CriticalOpBlockUpdate"));
    return;
}
if (level == CriticalOpGateLevel.SoftConfirm)
{
    var state = /* ActiveBranchDisplayName or settle/wizard label */;
    if (!_confirm(string.Format(LocalizationService.T("CriticalOpSoftUpdateConfirm"), state)))
        return;
    AppendLog("User accepted soft update risk during settle/wait.");
}
// existing check flow...
```

App close:

```csharp
window.Closing += (_, e) =>
{
    if (window.DataContext is MainViewModel vm)
    {
        if (vm.TryHandleWindowClosing(_blockMainClose || _busyDialog is not null, out var cancel) && cancel)
            e.Cancel = true;
        else if (_blockMainClose)
            e.Cancel = true;
    }
    else if (_blockMainClose)
        e.Cancel = true;
};
```

Prefer single path: Busy sets `_blockMainClose` → pass `busyDialogOpen: _blockMainClose` into VM so Hard covers Busy without double MessageBox. When Hard from Busy, `TryHandleWindowClosing` notifies once.

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void CheckForUpdates_hard_blocks_when_guard_running()
{
    // Arrange: CreateVm with fake UpdateChecker that would succeed; Begin guard
    // Act: CheckForUpdatesCommand.Execute
    // Assert: confirm/open URL not called; notify received CriticalOpBlockUpdate
}

[Fact]
public void CheckForUpdates_soft_requires_confirm_when_awaiting_settle()
{
    // IsAwaitingSteamSettle=true; confirm returns false → no URL
    // confirm returns true → URL open attempted (track via injectable open or UpdateChecker + confirm spy)
}
```

If `TryOpenUrl` is hard to spy, assert `_confirm` was invoked with soft text and early-return when false; when true, assert update checker ran.

- [ ] **Step 2: Implement wiring**

- [ ] **Step 3: Tests PASS**

- [ ] **Step 4: Commit** (if requested)

---

### Task 5: Begin/Complete on disk paths

**Files:**
- Modify: `MainViewModel.cs` only (wrap existing async methods) — prefer not pushing Guard deep into `BranchSwitchService` in v1 unless a method has no VM wrapper.

**Wrap with try/finally:**

| Method / segment | Kind | Detail example |
|------------------|------|----------------|
| Branch switch Official↔Beta disk segment after Steam exit, before mutate | `BranchDiskWrite` | `SwitchToBeta` / `SwitchToOfficial` |
| Wizard archive/link move+junction segments | `BranchDiskWrite` | `WizardArchiveB` etc. |
| `RepairOrphanDualLayout` | `OrphanRepair` | `RepairOrphan` |
| Teardown dual-folder | `BranchDiskWrite` | `Teardown` |
| `InstallMelonLoaderAsync` write/extract | `MelonInstall` | `MelonInstall` |

Pattern:

```csharp
_criticalOp.Begin(CriticalOpKind.BranchDiskWrite, "SwitchToBeta");
try
{
    // existing mutate
}
finally
{
    _criticalOp.Complete();
}
```

**Do not** Begin for:

- Pure `WaitForSteamAndGameExitAsync` wait loops before mutate (optional: Begin only immediately before mutate after wait returns true)
- `IsAwaitingSteamSettle` sticky wait
- `ConfirmManualBeta` / deploy+snapshot (Busy only)

- [ ] **Step 1: Add one integration-style test** that calls a VM path with a fake branch service if available; otherwise unit-test a small internal helper `RunCriticalAsync(kind, detail, Func<Task>)` extracted for testability:

```csharp
internal async Task RunWithCriticalOpAsync(CriticalOpKind kind, string detail, Func<Task> action)
{
    _criticalOp.Begin(kind, detail);
    try { await action().ConfigureAwait(true); }
    finally { _criticalOp.Complete(); }
}
```

Test: action throws → Complete still runs; marker gone; `IsRunning` false.

- [ ] **Step 2: Refactor call sites to use helper**

- [ ] **Step 3: `dotnet test --filter CriticalOp` PASS**

---

### Task 6: Startup recovery modal

**Files:**
- Create: `Dialogs/CriticalOpRecoveryDialog.xaml`
- Create: `Dialogs/CriticalOpRecoveryDialog.xaml.cs`
- Modify: `App.xaml.cs` — after `window.Show()`, if guard interrupted or (optional) dirty inconsistent layout, show dialog before enabling writes
- Modify: `MainViewModel.cs` — `IsRecoveryGateActive`, `ApplyRecoveryContinue()`, `ApplyRecoveryRepair()`

**Trigger (App after DataContext set, before or right after Show):**

```csharp
var interrupted = criticalOp.TryLoadInterrupted(out _);
var needsRecovery = interrupted || vm.ShouldOfferCriticalRecovery();
if (needsRecovery)
{
    vm.IsRecoveryGateActive = true;
    // show CriticalOpRecoveryDialog
    // Continue => vm.ApplyRecoveryContinue(); clear gate when continue routed
    // Repair => await/fire RepairOrphan or focus repair; on success ClearInterrupted + clear gate
}
```

`ShouldOfferCriticalRecovery()` (VM): true if `TryLoadInterrupted` already handled in App; additionally if `InspectOrphanDualLayout.IsOrphan` **and** wizard mid-flight — keep v1 trigger primarily **marker-based** to avoid nagging; secondary: marker OR (`IsOrphan && WizardStep not None/Ready`). Spec allows both — implement:

```csharp
public bool ShouldOfferCriticalRecovery(CriticalOpGuard guard)
{
    if (guard.TryLoadInterrupted(out _)) return true;
    try
    {
        return _branchSwitch.InspectOrphanDualLayout(GamePath).IsOrphan
            && IsBranchWizardInProgress;
    }
    catch { return false; }
}
```

**Continue path:**

1. Refresh branch UI from config (`ReloadBranchState` / existing).
2. If `IsAwaitingSteamSettle` → ensure settle button visible; navigate to settings/branch page if page enum exists.
3. If wizard mid → existing continue wizard entry.
4. If marker stale + Ready + not orphan → `ClearInterrupted()`; `IsRecoveryGateActive=false`.
5. Else keep gate until user finishes settle/repair; clearing gate when: settle cleared successfully, repair succeeded, or stale-clean path above.

**Session lock:** `IsRecoveryGateActive` must make `IsSessionLocked` true (add to property).

```csharp
public bool IsSessionLocked =>
    IsRecoveryGateActive
    || IsAwaitingSteamSettle
    || ...
```

Hard update/close already via Classify.

- [ ] **Step 1: Test** `IsRecoveryGateActive` locks deploy; Continue on stale Ready clears.

- [ ] **Step 2: Implement dialog + App hook**

- [ ] **Step 3: Manual checklist** (document in plan completion notes)

---

### Task 7: Installer copy

**Files:**
- Modify: `installer/MechabellumModManager.iss` — custom `AppMutex` already? If not, ensure CloseApplications enabled; add `[Messages]` override or `ChineseSimplified.isl` note
- Modify: `installer/ChineseSimplified.isl` — `ErrorCloseApplications` / related string append guidance
- Modify: English default messages if project has `Default.isl` override section in `.iss`

Suggested zh append:

> 若钢铁指挥官 Mod 管理器正在切换正式服/测试服或安装 MelonLoader，请先在管理器内完成或取消该任务并正常退出，再继续安装。不建议强制结束进程。

- [ ] **Step 1: Edit strings**
- [ ] **Step 2: Compile Setup** (`.\installer\build-installer.ps1`) — SUCCESS

---

### Task 8: Verification + release notes

- [ ] **Step 1: Full test**

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj -c Release
```

Expected: 0 failed.

- [ ] **Step 2: Manual checklist**

1. Start switch → Busy/write → 检查更新 → blocked notify  
2. Settle wait → 检查更新 → confirm cancel/accept  
3. Settle wait → close → confirm  
4. Simulate leftover `critical-op.json` in AppData → startup modal  
5. Repair/continue clears marker  
6. Run Setup while manager open → see new close-apps guidance  

- [ ] **Step 3: Update** `release/v1.1.2/RELEASE_NOTES.md` bullet for this feature  
- [ ] **Step 4: Pack/upload only if user requests**

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| CriticalOpGuard marker Begin/Complete | Task 1 |
| Hard vs Soft vs Allow matrix | Task 2 |
| Check update gated | Task 4 |
| Close gated | Task 4 |
| Begin on branch/Melon/orphan | Task 5 |
| No marker on settle-only / deploy | Task 5 notes |
| Startup modal Continue/Repair | Task 6 |
| IsSessionLocked includes recovery | Task 6 |
| Setup Files-in-Use copy | Task 7 |
| Tests from spec §6 | Tasks 1–2, 4–6, 8 |
| i18n | Task 3 |

**Placeholders:** none intentional.  
**Type names:** `CriticalOpGateLevel`, `CriticalOpGuard`, `CriticalOpKind` consistent across tasks.
