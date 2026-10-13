# Logic Frame Risk Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Before a local mod is enabled, grade its DLL by whether a Harmony patch sits on a battle-simulation method, confirm Medium and High with the existing dialog, and add three factual lines to the diagnostics summary that separate a window hang, a living log, and a logic-frame exception.

**Architecture:** A pure Cecil scanner and a pure gate live beside the paused keyword heuristic and do not read `RiskHeuristic.DetectionEnabled`. Grades are stored on `ModPackage` in their own fields. Enable is the only moment that asks for confirmation. Diagnostics append a section and do not change `Diagnosis` codes. Author-side hook rewrites of DamageRank and BattleSuite are not in this repository and are not tasks.

**Tech Stack:** C# / net8.0-windows / Mono.Cecil 0.11.6 (already referenced by the manager) / xUnit / FluentAssertions / WPF.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- `RiskHeuristic.DetectionEnabled` stays `false`. Do not write `ModPackage.HighRisk` from this scanner. Do not turn the keyword column back on.
- Confirm only when the grade is Medium or High, and only on the transition to enabled (import auto-enable and the enable checkbox). Reloading the library updates the label and does not disable an already enabled mod and does not open a dialog.
- A catalog item with no local DLL stays Unchecked. Do not confirm, and do not cancel the download, in `AddOneCatalogModAsync` before the file exists.
- The scanner must not special-case a mod by display name, file name, or catalog id. Replay-only gates are not proved. A prefix that returns `bool`, or a postfix on a simulation method that returns a value, is High.
- Unchecked does not confirm. It is shown as 未检测.
- Window-hang evidence is `summary.txt` containing `trigger: hang`. A stale `heartbeat.txt` is not a hang. Do not parse heartbeat age.
- The three diagnostic lines must not change `EvaluateDiagnosis` or any diagnosis code.
- Do not claim a permanent ban, and do not name DamageRank or BattleSuite inside the confirm text.
- New user-visible strings go into `Strings.resx`, `Strings.en.resx`, `Strings.de.resx`, `Strings.ja.resx`, and `Strings.ru.resx`.
- Tests must not download catalog DLLs and must not launch the game.

---

## Workflow

1. Import or reload reads each local `*.dll` with Mono.Cecil, in memory, symbols off.
2. The package grade is the strongest grade among its DLLs: High, then Medium, then Unchecked, then Low.
3. The grade and a reason code are written to `package.json` only when they changed.
4. The library column shows 低 / 中 / 高 / 未检测. The old keyword button stays disabled.
5. Enabling a Medium or High package opens the existing `_confirmHighRisk` dialog with a reason sentence. Cancel leaves it disabled.
6. Exporting diagnostics appends three lines under `summary.md`. Missing material stays the sentence `这次材料没有`.

## File map

| File | Responsibility |
|------|----------------|
| `src/MechabellumModManager/Services/LogicFrameRiskScanner.cs` | Read one DLL or many paths. No UI, no disk writes. |
| `src/MechabellumModManager/Services/LogicFrameGate.cs` | Decide whether enable needs a confirm. Ignores `DetectionEnabled`. |
| `src/MechabellumModManager/Services/IncidentSeparation.cs` | Build the three diagnostic lines from summary text and Melon log text. |
| `src/MechabellumModManager/Models/ModPackage.cs` | Store `LogicFrameGrade` and `LogicFrameReason`. |
| `src/MechabellumModManager/Services/ModLibraryService.cs` | Round-trip the two fields. Scan during import. |
| `src/MechabellumModManager/ViewModels/MainViewModel.cs` | Rescan on reload and on enable. Confirm only on enable. |
| `src/MechabellumModManager/ViewModels/ModItemViewModel.cs` | Label that does not read `DetectionEnabled`. |
| `src/MechabellumModManager/MainWindow.xaml` | Risk column shows the new label. Keyword toggle is not re-enabled. |
| `src/MechabellumModManager/Services/DiagnosticsSummaryBuilder.cs` | Append the three lines. Do not edit diagnosis codes. |
| `src/MechabellumModManager/Services/DiagnosticsExportService.cs` | Pass `summary.txt` text and the Melon log text into the builder. |
| `src/MechabellumModManager/Resources/Strings*.resx` | Confirm sentences and the four labels. |
| `tests/MechabellumModManager.Tests/LogicFrameRiskScannerTests.cs` | Cecil-built fixtures. |
| `tests/MechabellumModManager.Tests/LogicFrameGateTests.cs` | Confirm matrix. |
| `tests/MechabellumModManager.Tests/IncidentSeparationTests.cs` | Hang, stale heartbeat, log gaps, missing stack. |
| `tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj` | Add Mono.Cecil so tests can emit fixtures. |

## Grade rules

Simulation methods, matched by type name (last segment) and method name, both ordinal:

- Observe set, treated as void for postfix purposes: `FightActor.ReduceLife`, `DamagePerformer.Perform`, `Player.AddSupply`, `Player.AddRoundSupply`, `Player.ReduceSupply`, `Player.SetSupply`, `BattleStatisticManager.OnFightEnd`, `BattleSystem.OnFightOver`.
- Result set: `ProcessController.IsStateTimesUp`, `ProcessController.GetMaxStateTime`, `ProcessController.GetDeployTime`.

A method is a hook when its name is `Prefix` or `Postfix`, or it has an attribute whose name contains `HarmonyPrefix` or `HarmonyPostfix`, and either that method or its declaring type has an attribute whose name contains `HarmonyPatch`. The target is the method attribute's `(Type, string)` pair, otherwise the declaring type's pair. Attributes that do not yield that pair do not invent a target.

- Prefix on a simulation method whose return type is `System.Boolean`: High, reason `sim-replace`.
- Postfix on a result-set method: High, reason `sim-replace`.
- Any other hook on an observe-set or result-set method: Medium, reason `sim-observe`.
- The module contains a call whose method name is exactly `Patch` (not `PatchAll`, not `Unpatch`) and whose declaring type name contains `Harmony`, and no `HarmonyPatch` attribute yielded a target: Unchecked, reason `manual-patch`.
- A `HarmonyPatch` attribute has a type but no method-name string: that hook is unresolved. The module's floor becomes Unchecked, reason `manual-patch`, unless a resolved hook is already Medium or High.
- `ReadModule` throws: Unchecked, reason `unreadable`.
- Otherwise: Low, reason `none`.

A UI prefix that returns `bool`, such as `MainUI.OnClickUndoButton`, is not in either set and stays Low.

---

### Task 1: Scanner

**Files:**
- Create: `src/MechabellumModManager/Services/LogicFrameRiskScanner.cs`
- Modify: `tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj`
- Test: `tests/MechabellumModManager.Tests/LogicFrameRiskScannerTests.cs`

**Interfaces:**
- Consumes: Mono.Cecil `ModuleDefinition.ReadModule`
- Produces: `LogicFrameGrade` (`Unchecked`, `Low`, `Medium`, `High`); `LogicFrameScan` with `Grade` and `ReasonCode`; `LogicFrameRiskScanner.ScanFile(string dllPath)` and `ScanPaths(IEnumerable<string> dllPaths)`

- [ ] **Step 1: Add Cecil to the test project**

In `tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj`, inside the existing `PackageReference` item group, add:

```xml
<PackageReference Include="Mono.Cecil" Version="0.11.6" />
```

- [ ] **Step 2: Write the failing scanner tests**

Create `tests/MechabellumModManager.Tests/LogicFrameRiskScannerTests.cs`. Each test writes a temp DLL with Cecil and deletes it. The fixture helper adds a nested type, a `HarmonyPatch` attribute (a type defined in the fixture module is enough; the scanner matches the attribute name), and a `Prefix` or `Postfix` method.

Cover these cases:

- `ReduceLife` void `Prefix` → Medium, `sim-observe`
- `ReduceLife` `Prefix` returning `bool` → High, `sim-replace`
- `GetMaxStateTime` void `Postfix` → High, `sim-replace`
- `OnClickUndoButton` `Prefix` returning `bool` → Low, `none`
- Class-level `HarmonyPatch` plus a method named `Prefix` → same grade as a method-level attribute
- Module with a call whose method name is exactly `Patch` on a type whose name contains `Harmony`, and no `HarmonyPatch` attribute → Unchecked, `manual-patch`
- A call to `PatchAll` only, with no `HarmonyPatch` attribute and no exact `Patch` call → Low, `none`
- `HarmonyPatch` with a type argument and no method-name string → Unchecked, `manual-patch`
- A method named `Prefix` with no `HarmonyPatch` on the method or its declaring type → Low, `none`
- Empty module → Low, `none`
- Path that is not a DLL → Unchecked, `unreadable`
- `ScanPaths` of a Low DLL and a Medium DLL → Medium
- `ScanPaths` of a High DLL and an unreadable path → High
- `ScanPaths` of no paths → Low, `none`

- [ ] **Step 3: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter LogicFrameRiskScannerTests`

Expected: FAIL because `LogicFrameRiskScanner` does not exist.

- [ ] **Step 4: Implement the scanner**

`LogicFrameRiskScanner.ScanFile` uses:

```csharp
var parameters = new ReaderParameters { ReadSymbols = false, InMemory = true };
using var module = ModuleDefinition.ReadModule(dllPath, parameters);
```

Walk `module.Types` and nested types. Catch `Exception` around the read and return Unchecked / `unreadable`. Do not read method bodies.

`ScanPaths` ignores non-dll paths for the Low case, but a path that ends in `.dll` and throws is Unchecked. Merge with High > Medium > Unchecked > Low. An empty list returns Low / `none`.

- [ ] **Step 5: Run the tests and confirm they pass**

Run the same `dotnet test` filter. Expected: PASS.

### Task 2: Enable gate and copy

**Files:**
- Create: `src/MechabellumModManager/Services/LogicFrameGate.cs`
- Modify: `src/MechabellumModManager/Resources/Strings.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.en.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.de.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.ja.resx`
- Modify: `src/MechabellumModManager/Resources/Strings.ru.resx`
- Test: `tests/MechabellumModManager.Tests/LogicFrameGateTests.cs`

**Interfaces:**
- Consumes: `LogicFrameGrade`
- Produces: `LogicFrameGate.NeedsConfirm(LogicFrameGrade)` and `LogicFrameGate.CanEnable(LogicFrameGrade grade, string displayName, Func<string, string> translate, Func<string, bool> confirm)`

- [ ] **Step 1: Write the failing gate tests**

```csharp
[Theory]
[InlineData(LogicFrameGrade.Low, false)]
[InlineData(LogicFrameGrade.Unchecked, false)]
[InlineData(LogicFrameGrade.Medium, true)]
[InlineData(LogicFrameGrade.High, true)]
public void NeedsConfirm_only_medium_and_high(LogicFrameGrade grade, bool expected)
{
    LogicFrameGate.NeedsConfirm(grade).Should().Be(expected);
}

[Fact]
public void CanEnable_medium_asks_even_while_keyword_detection_is_paused()
{
    RiskHeuristic.DetectionEnabled.Should().BeFalse();
    string? seen = null;
    var ok = LogicFrameGate.CanEnable(
        LogicFrameGrade.Medium,
        "伤害排名",
        key => key,
        message => { seen = message; return false; });
    ok.Should().BeFalse();
    seen.Should().Contain("伤害排名");
}

[Fact]
public void CanEnable_low_does_not_call_confirm()
{
    var called = false;
    LogicFrameGate.CanEnable(LogicFrameGrade.Low, "格线", key => key, _ => { called = true; return false; })
        .Should().BeTrue();
    called.Should().BeFalse();
}
```

- [ ] **Step 2: Run the gate tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter LogicFrameGateTests`

Expected: FAIL because `LogicFrameGate` does not exist.

- [ ] **Step 3: Implement the gate and the five resource files**

`CanEnable` returns true immediately for Low and Unchecked. For Medium and High it builds the sentence with `translate(key).Replace("{0}", displayName)`, not `string.Format`. A display name that contains `{` or `}` must not throw. It does not mention `RiskHeuristic`.

Add these keys to all five resx files:

| Key | zh-CN (`Strings.resx`) | en, and de/ja/ru use this English until a later translation pass |
|-----|------------------------|------------------------------------------------------------------|
| `LogicFrameLow` | 低 | Low |
| `LogicFrameMedium` | 中 | Medium |
| `LogicFrameHigh` | 高 | High |
| `LogicFrameUnchecked` | 未检测 | Not checked |
| `ConfirmLogicFrameMedium` | 「{0}」挂在扣血、补给或战斗结束上。挂钩出错时，原来的函数可能不再执行，这一回合的战斗结果会和服务器不一致。确定加入当前方案吗？ | "{0}" hooks health, supply, or fight-end. If the hook throws, the original method may not run, and this round's battle result can disagree with the server. Add it to the current profile? |
| `ConfirmLogicFrameHigh` | 「{0}」会跳过原来的演算函数，或改写它的返回值。静态查看不能证明这只发生在回放里。确定加入当前方案吗？ | "{0}" can skip a simulation method or replace its return value. This check cannot prove the change is replay-only. Add it to the current profile? |

de, ja, and ru get the English sentences in this task. Do not leave the keys missing, or the UI will show the key name.

- [ ] **Step 4: Run the gate tests and confirm they pass**

Same filter. Expected: PASS. Also run `RiskGateTests`: keyword detection still paused, and `CanEnable(highRisk: true)` still does not confirm.

### Task 3: Persist the grade and scan on import and reload

**Files:**
- Modify: `src/MechabellumModManager/Models/ModPackage.cs`
- Modify: `src/MechabellumModManager/Services/ModLibraryService.cs` (`PackageMeta`, `WritePackageJson`, `TryLoadPackage`, the import method that assigns `HighRisk`)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`ReloadModsCore` only)
- Test: extend `tests/MechabellumModManager.Tests/ModLibraryImportTests.cs` if an import fixture already builds a temp DLL; otherwise add the assertion to `LogicFrameRiskScannerTests` is not enough. Add `LogicFramePersistTests` next to the import tests only if `ModLibraryService` can import a Cecil-built DLL without a game. If import requires a melon-looking assembly, call the same private-equivalent path the import method uses: after `WritePackageJson` the public import returns a package whose `LogicFrameGrade` is set. Read the existing import test and use that helper.

**Interfaces:**
- Consumes: `LogicFrameRiskScanner.ScanPaths`
- Produces: `ModPackage.LogicFrameGrade` (`string`, the enum name) and `ModPackage.LogicFrameReason` (`string`). Deserialization of an old `package.json` that lacks the two fields leaves the property initializers, which are `Unchecked` and `unreadable`. `ReloadModsCore` then scans. A package whose scan sees no `*.dll` becomes Low / `none`. Do not leave the initializer at Low: a load that has not scanned yet must not display 低.

- [ ] **Step 1: Add the two properties and round-trip them**

On `ModPackage` and on `PackageMeta`:

```csharp
public string LogicFrameGrade { get; set; } = nameof(LogicFrameGrade.Unchecked);
public string LogicFrameReason { get; set; } = "unreadable";
```

Copy both in `WritePackageJson` and `TryLoadPackage`. Do not change the `HighRisk` assignment. The keyword line stays:

```csharp
var risk = RiskHeuristic.DetectionEnabled ? _riskHeuristic.Evaluate(pkg) : new RiskHeuristicResult();
pkg.HighRisk = risk.HighRisk;
```

The `File.Copy` loop in `CommitPackage` has already filled `packageDir` before this assignment. Scan those copied `*.dll` paths. Do not scan `stagingPath`: `TryDeleteDir(stagingPath)` runs before the method returns. Do not grade from `DisplayName`.

- [ ] **Step 2: Rescan on reload without confirming**

In `ReloadModsCore`, after the existing `if (RiskHeuristic.DetectionEnabled)` block, for each package scan its dll paths. If grade or reason changed, assign and `PersistPackageMeta`. Do not call `_confirmHighRisk`. Do not call `_profiles.SetEnabled`.

- [ ] **Step 3: Test round-trip and the paused keyword flag**

Assert a package json written with `LogicFrameGrade = High` and `LogicFrameReason = sim-replace` loads those values, and `HighRisk` remains false while `DetectionEnabled` is false.

- [ ] **Step 4: Run import and library tests**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ModLibrary|FullyQualifiedName~LogicFrame"`

Expected: PASS.

### Task 4: Confirm on enable, and show the label

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`TryEnableImportedPackages`, `OnModEnabledChanged`)
- Modify: `src/MechabellumModManager/MainWindow.xaml` (the risk column near line 1348)
- Test: `tests/MechabellumModManager.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `LogicFrameGate.CanEnable`, `LogicFrameRiskScanner.ScanPaths`, `LocalizationService.T`
- Produces: `ModItemViewModel.LogicFrameGradeLabel`

- [ ] **Step 1: Label**

```csharp
public string LogicFrameGradeLabel => Package.LogicFrameGrade switch
{
    nameof(LogicFrameGrade.Medium) => LocalizationService.T("LogicFrameMedium"),
    nameof(LogicFrameGrade.High) => LocalizationService.T("LogicFrameHigh"),
    nameof(LogicFrameGrade.Unchecked) => LocalizationService.T("LogicFrameUnchecked"),
    _ => LocalizationService.T("LogicFrameLow")
};
```

Do not AND this with `RiskHeuristic.DetectionEnabled`. `NotifyRiskChanged` also raises `LogicFrameGradeLabel`.

- [ ] **Step 2: Rescan the one package, then confirm**

Add `bool ConfirmLogicFrame(ModPackage pkg)`:

- Scan the dlls in `pkg.PackageDirectory` again.
- If the grade or reason changed, persist. If persist throws, log the existing high-risk persist failure string. A disk error must not skip the confirm, and must not throw out of the checkbox handler.
- Use the grade just returned by the scan. An unknown grade string is Unchecked. Do not read the grade back from disk after persist.
- Return `LogicFrameGate.CanEnable(grade, pkg.DisplayName, LocalizationService.T, _confirmHighRisk)`.

Call it from `TryEnableImportedPackages` before `SetEnabled(..., true)` and from `OnModEnabledChanged` when `enabled` is true, in place of using the scan result through `_riskGate`. Leave `_riskGate.CanEnable` in place as well, so a future keyword flag still works, but today it returns true without a dialog because detection is paused. The new dialog is the one that must appear for Medium and High.

On cancel, keep the existing `SetEnabledSilent(false)` and the existing "not enabled" log. Do not call `SetEnabled`.

- [ ] **Step 3: Replace the risk-column button with a text block**

The column header stays `ColumnRisk`. The cell becomes a `TextBlock` bound to `LogicFrameGradeLabel`. Do not bind `LogicFrameReason`: `sim-observe` and `sim-replace` are not user-facing. The keyword `ToggleHighRisk` button is removed from this cell so a disabled button is not mistaken for the new grade. Do not set `HighRiskDetectionEnabled` to true.

- [ ] **Step 4: Run the view-model tests that cover enable**

`OnModEnabledChanged` is public. Using the existing `MainViewModelFixture`, create a view model whose `confirmHighRisk` records whether it was called.

- Package grade Low: call `OnModEnabledChanged(item, true)`. The delegate is not called, and the profile contains the package id.
- Package grade Medium and the delegate returns false: call `OnModEnabledChanged(item, true)`. The delegate is called once. The profile does not contain the package id. The item's enabled flag is false. The call must go through `SetEnabledSilent`, the same method the keyword path uses, so the checkbox write does not enter `OnModEnabledChanged` again.

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel|FullyQualifiedName~LogicFrame"`

Expected: PASS.

### Task 5: Three diagnostic lines

**Files:**
- Create: `src/MechabellumModManager/Services/IncidentSeparation.cs`
- Modify: `src/MechabellumModManager/Services/DiagnosticsSummaryBuilder.cs`
- Modify: `src/MechabellumModManager/Services/DiagnosticsExportService.cs` (the call that builds the summary, after `BlackBoxExport.Collect`)
- Test: `tests/MechabellumModManager.Tests/IncidentSeparationTests.cs`

**Interfaces:**
- Consumes: optional `summary.txt` body and optional Melon `Latest.log` body. It does not take `heartbeat.txt`.
- Produces: `IncidentSeparation.Evaluate(string? summaryText, string? melonLogText)` returning three strings: `Window`, `LogRate`, `LogicFrame`.

- [ ] **Step 1: Write the failing separation tests**

- `summary` null → Window is `这次材料没有`
- `summary` contains a line `trigger: hang` → Window contains `trigger=hang` and contains `不表示战斗结果`
- `summary` contains `trigger: unhandled-exception` and no hang → Window contains `不是窗口卡死`
- A heartbeat timestamp older than 20 seconds is not an input. There is no test that treats file age as a hang.
- Melon log with one timestamp → LogRate is `这次材料没有`
- Melon log with `[14:00:00.000]` and `[14:00:01.000]` → LogRate contains `中位数` and `不说明逻辑帧`
- Melon log with `MissingMethodException` and `FightActor.ReduceLife` → LogicFrame contains `ReduceLife` and contains `不指认`
- Melon log with `MissingMethodException` and no simulation method name → LogicFrame is `这次材料没有`
- Melon log null → both LogRate and LogicFrame are `这次材料没有`

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter IncidentSeparationTests`

Expected: FAIL because the type does not exist.

- [ ] **Step 3: Implement the lines and append them**

Timestamp pattern: `\[(\d{2}:\d{2}:\d{2}\.\d{3})\]`. Compute adjacent gaps in file order. A negative gap whose absolute value is under 2 seconds is clock noise: use 0. A negative gap of 2 seconds or more adds 24 hours (midnight). Fewer than two stamps yields `这次材料没有`. Do not sort the stamps; sorting would hide a real backwards jump and invent a midnight gap.

Simulation names searched in the Melon log, ordinal: `ReduceLife`, `DamagePerformer`, `AddSupply`, `ReduceSupply`, `SetSupply`, `OnFightEnd`, `OnFightOver`, `GetMaxStateTime`, `GetDeployTime`, `IsStateTimesUp`. A hit counts only when the same text also contains `Exception`.

`DiagnosticsSummaryBuilder.Build` gains two optional parameters, `string? blackboxSummaryText` and `string? melonLogText`, defaulting to null so existing callers still compile. After the BlackBox section, append:

```markdown
## 卡死与逻辑帧
- 窗口：...
- 日志：...
- 逻辑帧：...
```

`DiagnosticsExportService` reads the already-copied staging files `blackbox/summary.txt` and `melon/Latest.log` if present, and passes those strings. It does not read `heartbeat.txt` for this section. It does not pass the result into `EvaluateDiagnosis`.

- [ ] **Step 4: Run diagnostics tests and the new filter**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~Diagnostics|FullyQualifiedName~IncidentSeparation"`

Expected: PASS. Existing summary snapshots that assert the whole `summary.md` body must be updated to include the three lines when the new parameters are null: all three are `这次材料没有`.

### Task 6: Full test pass for the projects touched

- [ ] **Step 1: Run the manager test project**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj`

Expected: PASS. Failures in unrelated tests are not fixed by weakening a LogicFrame assertion.

---

## 审查结论

These are the defects found by reading this workflow against the current manager, and the rule in the tasks that closes each one.

1. **Keyword gate would swallow the new confirm.** `RiskGate.CanEnable` returns true without a dialog while `DetectionEnabled` is false, and `ModItemViewModel.HighRisk` hides the flag the same way. Task 2 uses a new gate that does not read that flag. Task 4 does not store the grade in `HighRisk`.
2. **A stale heartbeat looks like a hang.** `heartbeat.txt` is overwritten every 30 seconds and is left behind after a normal exit. Task 5 does not read it. Only `trigger: hang` in `summary.txt` is window evidence.
3. **Log gaps do not prove a healthy logic frame.** Idle time and a midnight wrap both exist. Task 5 reports the median and max gap and says they do not describe the logic frame. One timestamp is `这次材料没有`.
4. **A name-based ReplayTool exception would be the banned keyword path.** Task 1 grades `GetMaxStateTime` postfixes and `bool` prefixes as High for every assembly. The confirm text says the scanner cannot prove a replay-only gate.
5. **A `bool` prefix on the undo button is not a simulation skip.** `OnClickUndoButton` is outside the method set, and Task 1 has a test that it stays Low.
6. **Confirming before download grades a filename.** `AddOneCatalogModAsync` still uses the paused keyword probe only. The new scan runs after the DLL is on disk, in import, reload, and the enable transition.
7. **Reload must not uncheck mods or open dialogs.** Task 3 writes the grade and stops. Task 4 is the only confirm site.
8. **An unreadable DLL must not fail the library refresh.** `ScanFile` catches read errors and returns Unchecked. Unchecked does not confirm, matching the scheme's rule that 未检测 does not block. The hole this leaves — a hand-written `Harmony.Patch` whose target Cecil cannot see — is accepted and displayed as 未检测. A `PatchAll` call is not that hole: the method name check is ordinal equality with `Patch`, so `PatchAll` alone stays Low. A `HarmonyPatch` that names a type and no method is Unchecked, not Low.
9. **Release builds drop parameter names.** The scanner does not look for a parameter named `__result`. High for postfixes is decided from the result-set method names.
10. **The three lines must not become a ban verdict.** They are appended after the BlackBox section. `EvaluateDiagnosis` is unchanged. The window line for a hang says it does not mean the battle result disagreed.
11. **Old package.json must not display 低 before the scan.** The property initializer is Unchecked / `unreadable`. `CommitPackage` scans the copied files in `packageDir` before `WritePackageJson`, and it must not scan `stagingPath` after that directory is deleted.
12. **`string.Format` throws when a mod name contains a brace.** The confirm sentence is built with `Replace("{0}", displayName)`.
13. **A failed persist on enable must not skip the confirm and must not escape the checkbox handler.** The grade used for the dialog is the grade just scanned, not a value read back from disk.
14. **A one-millisecond backwards timestamp is not midnight.** Only a negative gap of at least 2 seconds adds 24 hours. Stamps are not sorted.
