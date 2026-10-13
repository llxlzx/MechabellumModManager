# Dual-Branch Busy Progress Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show an indeterminate modal busy window with stage messages during dual-branch folder moves and branch switches so testers do not think the app froze.

**Architecture:** `BusyProgressDialog` is shown via `Show()` (not `ShowDialog`) with the main window disabled. `App` injects `beginBusy` / `setBusyMessage` / `endBusy` into `MainViewModel`. Busy spans are **segmented** so mid-wizard Confirm dialogs and post-switch settle UI never open while the busy window is up. Disk ops run under `Task.Run` so the progress bar can animate.

**Tech Stack:** WPF (.NET 8), CommunityToolkit.Mvvm, xUnit

**Spec:** `docs/superpowers/specs/2026-09-05-branch-busy-progress-design.md`

## Global Constraints

- Interaction-modal via `Show()` + disable Owner; never `ShowDialog` for the busy window
- No cancel button; no byte percentage
- Scope B only: wizard archive/move segments + `SwitchToBranchAsync` move segment — not teardown / Melon / diagnostics / settle wait
- `endBusy` before any Confirm/Notify that needs user input
- Do not change archive/swap success conditions
- i18n via `Strings*.resx` (zh/en/de/ja/ru); no hardcoded Chinese stage strings in VM
- Pair every `beginBusy` with `finally { endBusy }`

---

### Task 1: BusyProgressDialog + App busy host

**Files:**
- Create: `src/MechabellumModManager/Dialogs/BusyProgressDialog.xaml`
- Create: `src/MechabellumModManager/Dialogs/BusyProgressDialog.xaml.cs`
- Modify: `src/MechabellumModManager/App.xaml.cs` (busy host + pass callbacks into `MainViewModel`)
- Modify: `src/MechabellumModManager/MainWindow.xaml.cs` (optional: expose Closing cancel hook, or handle in App)
- Modify: `src/MechabellumModManager/Resources/Strings.resx` (+ en/de/ja/ru)
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs` (only if UI binds title via Ui; otherwise dialog can call `LocalizationService.T` like ConfirmDialog)

**Interfaces:**
- Produces (injected into VM):
  - `Action<string, string>? beginBusy` → `(title, message)`
  - `Action<string>? setBusyMessage` → `(message)`
  - `Action? endBusy`
- Dialog: `void SetMessage(string message)`, Closing canceled while `_allowClose == false`

- [ ] **Step 1: Add localization keys** to all five resx files (before `</root>`):

| Key | zh (canonical) |
|-----|----------------|
| `BusyProgressTitle` | 请稍候 |
| `BusyWaitingSteam` | 正在请求退出 Steam / 游戏，请勿关闭管理器… |
| `BusyMovingGameFolder` | 正在移动游戏目录，请勿关闭管理器… |
| `BusyCreatingJunction` | 正在创建目录联接，请勿关闭管理器… |
| `BusySwitchingSteamBranch` | 正在准备 Steam 分支信息，请勿关闭管理器… |

en/de/ja/ru: equivalent meaning (keep “do not close the manager”).

- [ ] **Step 2: Create `BusyProgressDialog.xaml`** — similar chrome to `ConfirmDialog`, content:

```xml
<!-- Width ~420, no buttons, ProgressBar IsIndeterminate=True, TitleText + MessageText -->
```

Code-behind outline:

```csharp
public partial class BusyProgressDialog : Window
{
    bool _allowClose;

    public BusyProgressDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("BusyProgressTitle");
        TitleText.Text = LocalizationService.T("BusyProgressTitle");
        Closing += (_, e) => { if (!_allowClose) e.Cancel = true; };
    }

    public void SetMessage(string message) => MessageText.Text = message ?? "";

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}
```

- [ ] **Step 3: Implement App busy host** in `App.xaml.cs` (fields on the method that builds the VM, or instance fields on `App`):

```csharp
BusyProgressDialog? _busyDialog;
Window? _busyOwner;
bool _busyOwnerWasEnabled = true;
bool _blockMainClose;

void BeginBusy(Window owner, string title, string message)
{
    if (_busyDialog is not null)
    {
        _busyDialog.Title = title;
        _busyDialog.SetMessage(message);
        return;
    }

    _busyOwner = owner;
    _busyOwnerWasEnabled = owner.IsEnabled;
    owner.IsEnabled = false;
    _blockMainClose = true;

    _busyDialog = new BusyProgressDialog { Owner = owner };
    _busyDialog.Title = title;
    _busyDialog.SetMessage(message);
    _busyDialog.Show();
}

void SetBusyMessage(string message) => _busyDialog?.SetMessage(message);

void EndBusy()
{
    _blockMainClose = false;
    if (_busyDialog is not null)
    {
        _busyDialog.ForceClose();
        _busyDialog = null;
    }
    if (_busyOwner is not null)
    {
        _busyOwner.IsEnabled = _busyOwnerWasEnabled;
        _busyOwner = null;
    }
}
```

Wire MainWindow `Closing`:

```csharp
window.Closing += (_, e) => { if (_blockMainClose) e.Cancel = true; };
```

Pass into `MainViewModel` ctor (new optional params at end):

```csharp
beginBusy: (title, msg) => BeginBusy(window, title, msg),
setBusyMessage: SetBusyMessage,
endBusy: EndBusy,
```

- [ ] **Step 4: Build**

Run: `dotnet build src/MechabellumModManager/MechabellumModManager.csproj -c Release --nologo`  
Expected: success (VM may not consume callbacks yet — add stub fields in Task 2 if compile requires ctor params only as optional).

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Dialogs/BusyProgressDialog.xaml \
  src/MechabellumModManager/Dialogs/BusyProgressDialog.xaml.cs \
  src/MechabellumModManager/App.xaml.cs \
  src/MechabellumModManager/Resources/Strings*.resx
git commit -m "feat: add BusyProgressDialog and App busy host callbacks"
```

---

### Task 2: VM busy helpers + SwitchToBranchAsync

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (ctor fields + helpers + `SwitchToBranchAsync`)
- Modify: `tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs` (`CreateVm` accepts busy spies; new test)

**Interfaces:**
- Consumes: `Action<string, string>? beginBusy`, `Action<string>? setBusyMessage`, `Action? endBusy`
- Produces helpers:
  - `void BusyBegin(string message)` → `beginBusy(LocalizationService.T("BusyProgressTitle"), message)`
  - `void BusyMessage(string message)` → `setBusyMessage?.Invoke(message)`
  - `void BusyEnd()` → `endBusy?.Invoke()`
  - `async Task RunBusyAsync(string initialMessage, Func<Task> action)` with try/finally BusyEnd

- [ ] **Step 1: Write failing test** — extend `CreateVm` with optional busy callbacks; add test:

```csharp
[Fact]
public async Task SwitchToBranch_shows_busy_around_swap_and_ends_even_if_steam_wait_fails()
{
    // Arrange dual-folder ready config + probe returns steam running forever (or timeout)
    var begins = 0; var ends = 0;
    var vm = fx.CreateVm(
        confirm: _ => true,
        beginBusy: (_, _) => begins++,
        setBusyMessage: _ => { },
        endBusy: () => ends++);
    // Act: SwitchToBetaCommand / SwitchToOfficialCommand toward other branch
    // Assert: begins >= 1 && ends == begins (no leak)
}
```

Use existing fixture patterns in `MainViewModelBranchSwitchTests` for an enabled dual-folder layout. If steam wait fails quickly with probe always running + short timeout, swap never runs but Busy must still end.

- [ ] **Step 2: Run test — expect FAIL** (callbacks not wired)

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj -c Release --filter "SwitchToBranch_shows_busy" --nologo`

- [ ] **Step 3: Implement VM wiring**

Ctor: store `_beginBusy`, `_setBusyMessage`, `_endBusy`.

```csharp
void BusyBegin(string message) =>
    _beginBusy?.Invoke(LocalizationService.T("BusyProgressTitle"), message);

void BusyMessage(string message) => _setBusyMessage?.Invoke(message);

void BusyEnd() => _endBusy?.Invoke();

async Task RunBusyAsync(string initialMessage, Func<Task> work)
{
    BusyBegin(initialMessage);
    try { await work().ConfigureAwait(true); }
    finally { BusyEnd(); }
}
```

Rewrite `SwitchToBranchAsync` body after Confirm:

```csharp
IsBranchSwitchBusy = true;
try
{
    await RunBusyAsync(LocalizationService.T("BusyWaitingSteam"), async () =>
    {
        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
            return;

        BusyMessage(LocalizationService.T("BusyMovingGameFolder"));
        // snapshot ACF (fast) then:
        var swap = await Task.Run(() => _branchSwitch.TrySwapJunction(target)).ConfigureAwait(true);
        if (!swap.Success) { /* AppendLog; return */ }

        // update ActiveGameBranch / profile (UI thread)
        BusyMessage(LocalizationService.T("BusySwitchingSteamBranch"));
        EnsureMelonLoaderForDualStores(...); // keep existing
        var silent = _branchSwitch.TryPrepareSteamBranchMetadata(target);
        // log restored-acf as today
        // DO NOT call SettleAfterSilentBetaAsync inside RunBusyAsync
        _pendingSilentAfterBusy = silent; // or local var outside
    }).ConfigureAwait(true);

    // After busy closed:
    await SettleAfterSilentBetaAsync(silent).ConfigureAwait(true);
}
finally { IsBranchSwitchBusy = false; ... }
```

Prefer a local `BranchOperationResult? silentResult` assigned inside the busy lambda (capture) then settle after `RunBusyAsync` returns — **Busy must be ended before Settle/Notify**.

- [ ] **Step 4: Run test — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/ViewModels/MainViewModel.cs \
  tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs
git commit -m "feat: show busy progress during dual-branch switch move"
```

---

### Task 3: Wizard archive segments + CreateLink

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`RunWizardFromStartAsync`, `ContinueWizardAfterArchiveAAsync`, `FinishWizardAfterStoresReadyAsync` / resume paths)
- Modify: `tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs`

**Interfaces:**
- Consumes: same busy helpers from Task 2
- Segment rules (spec §4.2):
  - Segment A: WaitSteam + `ArchiveCurrentAs` (Task.Run)
  - **endBusy before** `ConfirmDownloadOtherBranchContinue` / user download wait UI
  - Segment B+C: after confirm, WaitSteam + `ArchiveDownloadedAs` + `CreateLinkTo` (Task.Run each) in one Busy, then end before any settle notify

- [ ] **Step 1: Failing test for ordering**

```csharp
[Fact]
public async Task Wizard_ends_busy_before_download_continue_confirm()
{
    var events = new List<string>();
    var vm = fx.CreateVm(
        confirm: msg =>
        {
            events.Add("confirm:" + msg.GetHashCode()); // or contains key substring
            // return true for start; for download-continue also true
            return true;
        },
        beginBusy: (_, m) => events.Add("begin:" + m),
        endBusy: () => events.Add("end"),
        pickCurrentBranch: () => GameBranch.Official,
        ...);
    // Drive wizard far enough that ConfirmDownloadOtherBranchContinue is asked
    // Assert: last "end" index < index of that confirm
}
```

Adapt to how confirms are distinguished in existing tests (message text / call count). If full wizard is heavy, assert at least: after ArchiveCurrent path when other store missing, an `end` occurs before the download confirm callback fires.

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement segments**

In `RunWizardFromStartAsync`, after Declared config saved:

```csharp
await RunBusyAsync(LocalizationService.T("BusyWaitingSteam"), async () =>
{
    if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
        return;
    BusyMessage(LocalizationService.T("BusyMovingGameFolder"));
    var archiveA = await Task.Run(() => _branchSwitch.ArchiveCurrentAs(current)).ConfigureAwait(true);
    if (!archiveA.Success) { AbortWizardAfterFailure(...); return; }
}).ConfigureAwait(true);
// if aborted/failed, return (Busy already ended)
SetWizardStep(WaitingDownloadB);
await ContinueWizardAfterArchiveAAsync(current);
```

In `ContinueWizardAfterArchiveAAsync`:

- Path “other store already ready” → `FinishWizardAfterStoresReadyAsync` (which opens its own Busy for link)
- Path waiting download: **no Busy** around `ConfirmDownloadOtherBranchContinue` / TryStartSteam
- After confirm true:

```csharp
await RunBusyAsync(LocalizationService.T("BusyWaitingSteam"), async () =>
{
    if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
        return;
    BusyMessage(LocalizationService.T("BusyMovingGameFolder"));
    var archiveB = await Task.Run(() => _branchSwitch.ArchiveDownloadedAs(other)).ConfigureAwait(true);
    if (!archiveB.Success) { FailWizard(...); return; }
    BusyMessage(LocalizationService.T("BusyCreatingJunction"));
    var link = await Task.Run(() => _branchSwitch.CreateLinkTo(current)).ConfigureAwait(true);
    // then apply Linked state on UI thread (existing FinishWizard body without second Wait)
}).ConfigureAwait(true);
```

Avoid double-running CreateLink: either fold Finish into this lambda or make `FinishWizardAfterStoresReadyAsync` call Busy only when invoked standalone (resume at ArchivedB/Linked). Recommended: extract `TryCreateLinkAndEnableAsync` used by both, with Busy at call sites.

`ResumeWizardAsync`: same Busy wrapping for whichever segment still runs.

- [ ] **Step 4: Run new + existing branch tests**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj -c Release --filter "FullyQualifiedName~BranchSwitch" --nologo`  
Expected: all pass

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/ViewModels/MainViewModel.cs \
  tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs
git commit -m "feat: segmented busy progress for dual-branch wizard archives"
```

---

### Task 4: Full suite + spec checklist

**Files:** none expected unless fixes

- [ ] **Step 1:** `dotnet test -c Release` — all green

- [ ] **Step 2: Spec checklist**

| Spec item | Verify |
|-----------|--------|
| Show not ShowDialog | App host |
| Segmented Busy around ConfirmDownload | Task 3 test |
| Settle after Busy on switch | SwitchToBranchAsync order |
| Task.Run on Archive/Swap/Link | call sites |
| MainWindow Closing blocked while busy | App `_blockMainClose` |
| No teardown Busy | grep RunTeardownCoreAsync |
| i18n keys all 5 resx | count keys |

- [ ] **Step 3: Commit any fixes**

```bash
git commit -m "fix: busy progress edge cases from checklist"
```

---

## Plan self-review

1. **Spec coverage:** Dialog+host §3 → T1; Switch §4.1 → T2; Wizard segments §4.2 → T3; tests/i18n/closing → T1–T4. Teardown explicitly excluded.  
2. **Placeholders:** none — concrete keys, helpers, segment rules.  
3. **Type consistency:** `beginBusy(string title, string message)`, `setBusyMessage(string)`, `endBusy()` used uniformly; `RunBusyAsync` wraps pairs.
