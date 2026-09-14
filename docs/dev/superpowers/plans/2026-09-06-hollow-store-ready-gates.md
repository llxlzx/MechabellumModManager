# Hollow-Store Ready Hard Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent false `BranchWizardStep.Ready` / settle-clear when the active store is hollow or ACF is not snapshot-safe; auto-degrade hollow Ready with an escape hatch to the healthy other store.

**Architecture:** Add pure `BranchStoreHealth` + `SteamBetaKeyEditor.LooksAcfHardFault` (HardFault ⇒ not settled). Wire `MainViewModel` settle/clear/degrade and relax switch/teardown gates during settle so escape stays possible. Keep content check at exe + `GameAssembly.dll` only.

**Tech Stack:** .NET 8, xUnit, FluentAssertions, existing `MainViewModelFixture` / branch fixtures.

**Spec:** `docs/superpowers/specs/2026-09-06-hollow-store-ready-gates-design.md`

## Global Constraints

- Independent hotfix — do not implement C1 Phase1 Policy/Orchestrator.
- Game root = `Mechabellum.exe` + `GameAssembly.dll` only (no level packs).
- Auto-degrade only when `Enabled && WizardStep==Ready` and active store/link is not a game root — never degrade solely for transient ACF `FilesMissing` while root is intact.
- After degrade to settle: must still allow switch to a complete other store, teardown/repair, diagnostics.
- Never `ClearSteamSettle` when snapshot fails (remove deferred clear-to-Ready).
- Do not delete store directories.
- Prefer Chinese user-facing strings via `Strings.resx` (+ `.en.resx` minimum).

## Logic self-check findings (must not regress)

| ID | Finding | Required implementation |
|----|---------|-------------------------|
| L1 | Today `CanSwitchGameBranch` / `CanTeardownBranchSwitch` / `SwitchToBranchAsync` all hard-block on `IsAwaitingSteamSettle` | After hollow degrade, **must** allow switch to complete other store and teardown; otherwise escape is dead |
| L2 | `CanDeployOrLaunch` already false when `GameMissing` | Degrade still needed for false **wizard** Ready / sticky settle UX |
| L3 | `ArchiveDownloadedAs` already requires `LooksLikeGameRoot(link)` | Still gate wizard “continue” before archive; `ArchiveCurrentAs` must refuse non-root source |
| L4 | `LooksSettledForSnapshot` special-cases `"1190"` but not all Corrupt/Missing masks | `LooksAcfHardFault` early in settled check |
| L5 | Deferred clear at `DeployBoundProfileAndClearSettle` after non-settled snapshot failures other than known cases | Delete clear-on-deferred; keep settle |
| L6 | Active path to probe for degrade | Prefer **active branch store path**; also treat Steam link as hollow if it is the live path and fails `LooksLikeGameRoot` (junction → resolve target) |

## File map

| File | Responsibility |
|------|----------------|
| Create `Services/BranchStoreHealth.cs` | Evaluate root + ACF promote/snapshot eligibility |
| Modify `Services/SteamBetaKeyEditor.cs` | `LooksAcfHardFault`; settled rejects HardFault |
| Modify `ViewModels/MainViewModel.cs` | Degrade, settle clear, escape gates, wizard continue |
| Modify `Services/BranchSwitchService.cs` | ArchiveCurrentAs non-root fail if missing |
| Modify `Resources/Strings*.resx` | Notify/log strings |
| Create `tests/.../BranchStoreHealthTests.cs` | Pure health matrix |
| Modify `tests/.../SteamBetaKeyEditorTests.cs` | HardFault / settled |
| Modify `tests/.../MainViewModelBranchSwitchTests.cs` | Degrade, no deferred clear, escape switch |

---

### Task 1: `LooksAcfHardFault` + settled coupling

**Files:**
- Modify: `src/MechabellumModManager/Services/SteamBetaKeyEditor.cs`
- Modify: `tests/MechabellumModManager.Tests/SteamBetaKeyEditorTests.cs`

**Interfaces:**
- Produces: `public static bool LooksAcfHardFault(string acfText)` — true if StateFlags has bit 32 or 128 or 1024
- Produces: `LooksSettledForSnapshot` returns false when `LooksAcfHardFault` is true (call near top, after empty check / with unhealthy)

- [ ] **Step 1: Write failing tests**

Add to `SteamBetaKeyEditorTests.cs`:

```csharp
[Fact]
public void LooksAcfHardFault_true_for_1190()
{
    var acf = """
        "AppState"
        {
        	"StateFlags"		"1190"
        	"buildid"		"1"
        	"TargetBuildID"		"1"
        	"BytesToDownload"		"0"
        	"BytesDownloaded"		"0"
        }
        """;
    SteamBetaKeyEditor.LooksAcfHardFault(acf).Should().BeTrue();
    SteamBetaKeyEditor.LooksSettledForSnapshot(acf).Should().BeFalse();
}

[Fact]
public void LooksSettledForSnapshot_still_true_for_fully_installed_4()
{
    var acf = """
        "AppState"
        {
        	"StateFlags"		"4"
        	"buildid"		"100"
        	"TargetBuildID"		"100"
        	"BytesToDownload"		"0"
        	"BytesDownloaded"		"0"
        }
        """;
    SteamBetaKeyEditor.LooksAcfHardFault(acf).Should().BeFalse();
    SteamBetaKeyEditor.LooksSettledForSnapshot(acf).Should().BeTrue();
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~SteamBetaKeyEditorTests.LooksAcfHardFault" -v n`

- [ ] **Step 3: Implement**

In `SteamBetaKeyEditor.cs`:

```csharp
public static bool LooksAcfHardFault(string acfText)
{
    if (string.IsNullOrWhiteSpace(acfText))
        return false;
    if (!TryParseStateFlags(acfText, out var flags))
        return false;
    const int filesMissing = 32;
    const int filesCorrupt = 128;
    const int uninstalling = 1024;
    return (flags & (filesMissing | filesCorrupt | uninstalling)) != 0;
}
```

At start of `LooksSettledForSnapshot` body (after empty check):

```csharp
if (LooksAcfHardFault(acfText))
    return false;
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/SteamBetaKeyEditor.cs tests/MechabellumModManager.Tests/SteamBetaKeyEditorTests.cs
git commit -m "fix: treat ACF FilesMissing/Corrupt/Uninstalling as unsettleable"
```

---

### Task 2: `BranchStoreHealth` pure evaluator

**Files:**
- Create: `src/MechabellumModManager/Services/BranchStoreHealth.cs`
- Create: `tests/MechabellumModManager.Tests/BranchStoreHealthTests.cs`

**Interfaces:**
- Produces:

```csharp
public readonly record struct BranchStoreHealthResult(
    bool IsGameRoot,
    bool AcfHardFault,
    bool AcfAllowsSnapshot,
    bool CanPromoteReady,
    string? PrimaryReason);

public static class BranchStoreHealth
{
    public static BranchStoreHealthResult Evaluate(string? storePath, string? acfText);
}
```

- [ ] **Step 1: Write failing tests**

```csharp
public class BranchStoreHealthTests
{
    [Fact]
    public void Hollow_store_cannot_promote()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "x");
            var r = BranchStoreHealth.Evaluate(dir, settledAcf());
            r.IsGameRoot.Should().BeFalse();
            r.CanPromoteReady.Should().BeFalse();
            r.PrimaryReason.Should().Be("store_not_game_root");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Root_ok_but_hardfault_acf_cannot_promote()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "x");
            File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "x");
            var acf = """
                "AppState" { "StateFlags" "1190" "buildid" "1" "TargetBuildID" "1"
                "BytesToDownload" "0" "BytesDownloaded" "0" }
                """;
            var r = BranchStoreHealth.Evaluate(dir, acf);
            r.IsGameRoot.Should().BeTrue();
            r.AcfHardFault.Should().BeTrue();
            r.CanPromoteReady.Should().BeFalse();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    static string settledAcf() => """
        "AppState" { "StateFlags" "4" "buildid" "1" "TargetBuildID" "1"
        "BytesToDownload" "0" "BytesDownloaded" "0" }
        """;
}
```

- [ ] **Step 2: Run — expect FAIL (type missing)**

- [ ] **Step 3: Implement `BranchStoreHealth.cs`**

```csharp
namespace MechabellumModManager.Services;

public readonly record struct BranchStoreHealthResult(
    bool IsGameRoot,
    bool AcfHardFault,
    bool AcfAllowsSnapshot,
    bool CanPromoteReady,
    string? PrimaryReason);

public static class BranchStoreHealth
{
    public static BranchStoreHealthResult Evaluate(string? storePath, string? acfText)
    {
        var root = SteamGameLocator.LooksLikeGameRoot(storePath);
        var hard = SteamBetaKeyEditor.LooksAcfHardFault(acfText ?? "");
        var snap = !string.IsNullOrWhiteSpace(acfText)
                   && SteamBetaKeyEditor.LooksSettledForSnapshot(acfText);
        string? reason = null;
        if (!root) reason = "store_not_game_root";
        else if (hard) reason = "acf_hard_fault";
        else if (!snap) reason = "acf_not_settled";
        return new BranchStoreHealthResult(root, hard, snap, root && snap, reason);
    }
}
```

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Services/BranchStoreHealth.cs tests/MechabellumModManager.Tests/BranchStoreHealthTests.cs
git commit -m "feat: add BranchStoreHealth promote/snapshot evaluator"
```

---

### Task 3: Stop deferred `ClearSteamSettle` on snapshot failure

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`DeployBoundProfileAndClearSettle`)
- Modify: `tests/MechabellumModManager.Tests/MainViewModelBranchSwitchTests.cs`
- Modify: `Resources/Strings.resx`, `Strings.en.resx` if new notify key needed

**Interfaces:**
- Consumes: `BranchStoreHealth.Evaluate`, existing `TrySnapshotSettledAcf`
- Behavior: any `!snap.Success` → keep settle; **never** call `ClearSteamSettle` on deferred path

- [ ] **Step 1: Write failing test**

Extend scenario: settle with GameStatus Ready + settled-looking store, but force snapshot fail via unsettled ACF **after** deploy path — existing `ConfirmManualBeta_keeps_settle_when_acf_not_settled` already covers unsettle. Add explicit test that **StoreNotGameRoot / generic snapshot fail** does not clear:

Reuse pattern from `ConfirmManualBeta_keeps_settle_when_exe_missing` — ensure after confirm, still awaiting. Add:

```csharp
[Fact]
public async Task ConfirmManualBeta_keeps_settle_when_snapshot_fails_after_ready_game()
{
    // Ready dual folder, enter settle via switch, write settled ACF,
    // then delete GameAssembly on active store so TrySnapshotSettledAcf fails StoreNotGameRoot
    // while... actually DeployBoundProfile requires GameStatus Ready which needs GameAssembly.
    // So use unsettled ACF after apply (existing test). Add assertion that
    // LogSettleClearedSnapshotDeferred path is gone: with Ready game + HardFault ACF (1190),
    // confirm keeps settle.
}
```

Concrete test (1190 + complete game files so Apply can succeed if Melon ready):

```csharp
[Fact]
public async Task ConfirmManualBeta_keeps_settle_on_acf_1190_hardfault()
{
    using var fx = Fixture.CreateReadyDualFolder();
    // config Ready official, switch to beta → settle, write 1190 acf with matching beta key
    // ConfirmManualBeta → still IsAwaitingSteamSettle
}
```

Mirror `ConfirmManualBeta_blocks_when_update_unhealthy_518` structure with StateFlags 1190 and non-idle bytes if needed so Apply might still run — prefer: set WizardStep AwaitingSteamSettle, ActiveBranch Beta, Melon-ready beta store, ACF 1190, call Confirm — expect still settle (HardFault or not settled).

- [ ] **Step 2: Run — may PASS already for 1190 via LooksSettled; then change deferred clear and add test for generic failure**

Replace deferred-clear block in `DeployBoundProfileAndClearSettle`:

```csharp
// OLD: AppendLog deferred; ClearSteamSettle(); return;
// NEW:
AppendLog(LocalizationService.T("LogSettleKeptAwaiting"));
_notify(LocalizationService.T("NotifySettleBlockedAcfNotSettled")); // or new NotifySettleBlockedSnapshotFailed
return;
```

Also before snapshot, optional: if `!BranchStoreHealth.Evaluate(activeStore, acfText).CanPromoteReady` → keep settle (belt and suspenders).

- [ ] **Step 3: Run branch switch settle tests**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelBranchSwitchTests.ConfirmManualBeta" -v n`

- [ ] **Step 4: Commit**

```bash
git commit -m "fix: never ClearSteamSettle when ACF snapshot fails"
```

---

### Task 4: Auto-degrade hollow Ready + escape hatch

**Files:**
- Modify: `MainViewModel.cs` — `RefreshStatusCore` / post-`LoadBranchSwitchState`, `CanSwitchGameBranch`, `CanTeardownBranchSwitch`, `SwitchToBranchAsync`, `TeardownBranchSwitch`
- Modify: `Strings.resx` / `Strings.en.resx`
- Modify: `MainViewModelBranchSwitchTests.cs`

**Critical L1 behavior:**

```csharp
public bool CanSwitchGameBranch =>
    BranchSwitchEnabled && !IsBranchSwitchBusy;
    // allow during settle (escape / re-pick)

public bool CanTeardownBranchSwitch =>
    (BranchSwitchEnabled || IsBranchWizardInProgress) && !IsBranchSwitchBusy;
    // allow during settle

// SwitchToBranchAsync: remove `if (IsAwaitingSteamSettle) return;`
// Instead: if settle && target store !LooksLikeGameRoot → notify and return
// Teardown: do not abort solely because IsAwaitingSteamSettle (remove or soften that abort)
```

Degrade helper:

```csharp
void TryDegradeHollowReadyStore()
{
    if (!BranchSwitchEnabled || BranchWizardStep != BranchWizardStep.Ready)
        return;
    var cfg = _branchSwitch.LoadConfig();
    var store = ActiveGameBranch == GameBranch.Official ? cfg.OfficialStorePath : cfg.BetaStorePath;
    var path = store;
    // If link resolves, prefer live target for root check when available
    if (!SteamGameLocator.LooksLikeGameRoot(path) || !SteamGameLocator.LooksLikeGameRoot(cfg.SteamLinkPath))
    {
        // Degrade only if active store OR link fails root — if link is junction to active store, both fail together.
        // Spec: degrade when active store/link not game root. Use:
        // activeStoreOk = LooksLikeGameRoot(store); linkOk = LooksLikeGameRoot(SteamLinkPath);
        // if (activeStoreOk && linkOk) return; 
        // if (!activeStoreOk) degrade. If only linkOk false but store ok — still degrade (false Ready via broken link).
        EnterSteamSettle();
        AppendLog(...);
        _notify(LocalizationService.T("NotifyHollowStoreDegraded"));
    }
}
```

Call from end of `RefreshStatusCore` (after GameStatus updated) and after `LoadBranchSwitchState` once GamePath known — avoid double notify: use a bool or only notify when step actually changes from Ready.

**Do not degrade** when store is game root even if ACF has FilesMissing (test).

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Refresh_degrades_ready_when_active_store_hollow()
{
    // Ready config, delete GameAssembly from official (active), RefreshStatus
    // → AwaitingSteamSettle, WizardStep settle, directory still exists
}

[Fact]
public void Refresh_does_not_degrade_ready_when_root_ok_even_if_acf_missing_bits()
{
    // Ready, intact stores, write ACF with FilesMissing but... wait — we must NOT degrade on ACF alone.
    // Don't call degrade path for ACF; only delete nothing; write 1190 acf; Refresh → still Ready
}

[Fact]
public async Task Hollow_ready_degraded_can_switch_to_complete_other_store()
{
    // Hollow official, complete beta, degrade, CanSwitchGameBranch true, SwitchToBeta succeeds (or enters settle on beta)
}
```

- [ ] **Step 2: Implement gates + degrade**

- [ ] **Step 3: Run tests**

- [ ] **Step 4: Commit**

```bash
git commit -m "fix: degrade hollow Ready and allow switch/teardown escape during settle"
```

---

### Task 5: Wizard archive/continue root gate + i18n polish

**Files:**
- Modify: `BranchSwitchService.ArchiveCurrentAs` — if link exists as directory/junction target and `!LooksLikeGameRoot` before move, fail with clear message
- Modify: `MainViewModel.ContinueWizardAfterArchiveAAsync` — before confirm download continue, if other store incomplete keep waiting; after user confirm, if still not root don't archive (ArchiveDownloadedAs already fails)
- Ensure notify strings exist for hollow degrade / snapshot keep

- [ ] **Step 1: Test ArchiveCurrentAs fails on hollow link source** (if not already)

- [ ] **Step 2: Implement minimal guard**

- [ ] **Step 3: Run `BranchSwitchServiceTests` + branch VM tests filter**

- [ ] **Step 4: Commit**

```bash
git commit -m "fix: refuse dual-folder archive when source is not a game root"
```

---

### Task 6: Verification gate

- [ ] **Step 1: Run**

```bash
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~BranchStoreHealth|FullyQualifiedName~SteamBetaKeyEditor|FullyQualifiedName~MainViewModelBranchSwitch|FullyQualifiedName~BranchSwitchService" -v n
```

- [ ] **Step 2: Confirm no deferred-clear string usage remains for success path**

Search: `LogSettleClearedSnapshotDeferred` — should be unused or only historical; remove call sites.

- [ ] **Step 3: Final commit if doc status update**

Update spec status line to「规格已确认；实现见 plan …」.

```bash
git commit -m "docs: mark hollow-store Ready gates spec implemented"
```

---

## Self-review vs spec

| Spec requirement | Task |
|------------------|------|
| BranchStoreHealth | T2 |
| HardFault ⇒ unsettleable | T1 |
| Never clear settle on snapshot fail | T3 |
| Auto-degrade hollow Ready | T4 |
| Escape switch/teardown | T4 (L1) |
| Wizard archive gate | T5 |
| No level-pack checks | Global |
| Tests 1–6 in spec §7 | T3–T4 |
