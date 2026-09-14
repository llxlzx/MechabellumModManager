# Phase0 安全垫（Steam Lifecycle / 去反射 / Fixture / Gate 黄金表）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 Steam 启停收成 `ISteamLifecycle`、去掉 orphan 反射、统一 MainViewModel 测试夹具、加厚 `CriticalOpGate` 黄金表，为 Phase1+ 提供可回归的安全垫（不改切服产品语义）。

**Architecture:** 从 `MainViewModel` 抽出已存在的 exit/force-close/禁止 auto-open 逻辑到纯服务；VM 只转发。测试夹具合并到 `tests/.../Support/`。门禁 Classify 语义冻结，只加组合用例。

**Tech Stack:** .NET 8、WPF、xUnit、FluentAssertions、现有 `IProcessProbe` / `IProcessStarter` / `CriticalOpGate`。

**Spec:** `docs/superpowers/specs/2026-09-06-architecture-stability-cleanup-design.md` §3 Phase0

## Global Constraints

- 不做 Session Policy / Orchestrator / 拆 MainViewModel / 改向导步骤语义。
- `ISteamLifecycle` **永不** 调用 `steam://open/*`；`GameLauncher` 的 `steam://rungameid/669330` 不经 Lifecycle。
- `ProcessProbe` 继续忽略 `steamservice`；ForceClose 不杀 steamservice。
- 用户可见新文案走 `Strings.resx` + `Strings.en.resx`（已有键可复用则不新增）。
- 每 Task 结束：相关测试绿 → 自检清单勾选 → 单独 commit。
- 工作目录：`D:\gongzuo\钢铁指挥官mod管理器开发`。测试命令用 `dotnet test`（PowerShell）。

**Note:** 工作区可能已有未提交的 Steam 修复（VM 内 force-close / 禁止 open）。Phase0 **搬进 Lifecycle**，禁止再留第三套启停逻辑。

---

## File map

| 文件 | 职责 |
|------|------|
| Create: `src/MechabellumModManager/Services/SteamLifecycle.cs` | `ISteamLifecycle` + 实现 |
| Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` | 注入 Lifecycle；删除反射；薄转发 exit/manual-open |
| Modify: `src/MechabellumModManager/Services/CriticalOpGate.cs` | 仅当注释需与黄金表对齐时改注释；**默认不改语义** |
| Create: `tests/MechabellumModManager.Tests/SteamLifecycleTests.cs` | Lifecycle 单测 |
| Modify: `tests/MechabellumModManager.Tests/CriticalOpGateTests.cs` | 组合黄金表 |
| Create: `tests/MechabellumModManager.Tests/Support/FakeProcessProbe.cs` | 共用 Fake |
| Create: `tests/MechabellumModManager.Tests/Support/MainViewModelFixture.cs` | 共用 Fixture |
| Modify: `tests/.../MainViewModelBranchSwitchTests.cs` | 改用 Support |
| Modify: `tests/.../MainViewModelCriticalOpTests.cs` | 改用 Support |
| Modify: `tests/.../MainViewModelTests.cs` | 改用 Support |
| Modify: `tests/.../BranchSwitchServiceTests.cs` | 删除重复 `FakeProcessProbe`，改 using Support |

---

### Task 1: CriticalOpGate 组合黄金表（先钉契约）

**Why:** 纯函数、零产品风险；后续改 Lifecycle/VM 时用它防止 Soft/Hard 漂。

**Files:**
- Modify: `tests/MechabellumModManager.Tests/CriticalOpGateTests.cs`
- Optionally comment-only: `src/MechabellumModManager/Services/CriticalOpGate.cs`

**Interfaces:**
- Consumes: `CriticalOpGate.Classify(bool guardRunning, bool busyDialogOpen, bool branchSwitchBusy, bool recoveryModalPending, bool awaitingSteamSettle, bool wizardInProgress)`
- Produces: 无新 API；测试覆盖组合优先级

- [ ] **Step 1: 在现有测试类追加组合用例（先写断言）**

在 `CriticalOpGateTests.cs` 增加：

```csharp
[Theory]
[InlineData(true, true, true, false, true, true, CriticalOpGateLevel.HardBlock)]   // Guard 压 Soft
[InlineData(false, false, false, true, true, true, CriticalOpGateLevel.HardBlock)] // Recovery 压 Soft
[InlineData(false, true, false, false, true, false, CriticalOpGateLevel.SoftConfirm)] // Busy+Settle
[InlineData(false, false, true, false, false, true, CriticalOpGateLevel.SoftConfirm)] // BranchBusy+Wizard
[InlineData(false, true, true, false, true, true, CriticalOpGateLevel.SoftConfirm)]   // 多 Soft 仍 Soft
public void Classify_combination_priority(
    bool guardRunning,
    bool busyDialogOpen,
    bool branchSwitchBusy,
    bool recoveryModalPending,
    bool awaitingSteamSettle,
    bool wizardInProgress,
    CriticalOpGateLevel expected)
{
    CriticalOpGate.Classify(
            guardRunning,
            busyDialogOpen,
            branchSwitchBusy,
            recoveryModalPending,
            awaitingSteamSettle,
            wizardInProgress)
        .Should().Be(expected);
}
```

- [ ] **Step 2: 跑测（预期全绿；若红则停 — 语义与文档冲突，单开 bug，勿在 Phase0 改产品）**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~CriticalOpGateTests"
```

Expected: 全部 Passed。

- [ ] **Step 3: 自检**

- [ ] `Classify` 方法体未改（或只加注释）
- [ ] 现有 `Classify_hard_block_wins_over_soft_confirm` 仍绿

- [ ] **Step 4: Commit**

```powershell
git add tests/MechabellumModManager.Tests/CriticalOpGateTests.cs src/MechabellumModManager/Services/CriticalOpGate.cs
git commit -m "test: thicken CriticalOpGate combination golden matrix"
```

---

### Task 2: `ISteamLifecycle` TDD（服务本体）

**Files:**
- Create: `src/MechabellumModManager/Services/SteamLifecycle.cs`
- Create: `tests/MechabellumModManager.Tests/SteamLifecycleTests.cs`
- Use Fake probe: 可临时把 `FakeProcessProbe` 拷进测试文件；Task 5 再抽到 Support（避免本 Task 依赖 Fixture 大搬家）

**Interfaces:**
- Consumes: `IProcessProbe`, `IProcessStarter`, `Func<TimeSpan, Task> delay`
- Produces:

```csharp
public interface ISteamLifecycle
{
    bool IsClientRunning();
    bool IsGameRunning();
    /// <summary>
    /// Soft steam://exit (only if client running) → wait → ForceClose → wait → cooldown recheck.
    /// Never starts Steam.
    /// </summary>
    Task<bool> EnsureClientAndGameExitedAsync(CancellationToken cancellationToken = default);
    /// <summary>Log + optional notify. Must not StartShell any steam:// URI.</summary>
    void PreferManualOpen(string message);
}

public sealed class SteamLifecycle : ISteamLifecycle
{
    public SteamLifecycle(
        IProcessProbe probe,
        IProcessStarter starter,
        Func<TimeSpan, Task>? delay = null,
        TimeSpan? exitTimeout = null,
        TimeSpan? exitCooldown = null,
        Action<string>? log = null,
        Action<string>? notify = null);
}
```

- [ ] **Step 1: 写失败测试文件 `SteamLifecycleTests.cs`**

```csharp
using FluentAssertions;
using MechabellumModManager.Services;

public class SteamLifecycleTests
{
    sealed class RecordingStarter : IProcessStarter
    {
        public List<string> Starts { get; } = new();
        public void StartShell(string uriOrPath) => Starts.Add(uriOrPath);
    }

    // Minimal fake — replace with Support.FakeProcessProbe in Task 5
    sealed class FakeProbe : IProcessProbe
    {
        public bool GameRunning { get; set; }
        public bool SteamRunning { get; set; }
        public int ForceCloseCalls { get; private set; }
        public bool ForceCloseClearsRunning { get; set; }

        public bool IsGameRunning() => GameRunning;
        public bool IsSteamRunning() => SteamRunning;
        public bool IsGameOrSteamRunning() => GameRunning || SteamRunning;

        public void ForceCloseSteamClientAndGame()
        {
            ForceCloseCalls++;
            if (ForceCloseClearsRunning)
            {
                SteamRunning = false;
                GameRunning = false;
            }
        }
    }

    [Fact]
    public async Task EnsureExited_when_already_stopped_does_not_call_exit_or_force()
    {
        var probe = new FakeProbe();
        var starter = new RecordingStarter();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.Zero, exitCooldown: TimeSpan.Zero);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeTrue();
        starter.Starts.Should().BeEmpty();
        probe.ForceCloseCalls.Should().Be(0);
    }

    [Fact]
    public async Task EnsureExited_when_client_running_requests_exit_then_force_close()
    {
        var probe = new FakeProbe { SteamRunning = true, ForceCloseClearsRunning = true };
        var starter = new RecordingStarter();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.FromSeconds(1), exitCooldown: TimeSpan.Zero);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeTrue();
        starter.Starts.Should().Contain("steam://exit");
        starter.Starts.Should().NotContain(s => s.Contains("open", StringComparison.OrdinalIgnoreCase));
        probe.ForceCloseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EnsureExited_when_only_game_running_skips_steam_exit_but_force_closes()
    {
        var probe = new FakeProbe { GameRunning = true, ForceCloseClearsRunning = true };
        var starter = new RecordingStarter();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.FromSeconds(1), exitCooldown: TimeSpan.Zero);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeTrue();
        starter.Starts.Should().NotContain("steam://exit");
        probe.ForceCloseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EnsureExited_returns_false_if_still_running_after_force()
    {
        var probe = new FakeProbe { SteamRunning = true, ForceCloseClearsRunning = false };
        var starter = new RecordingStarter();
        var logs = new List<string>();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.Zero, exitCooldown: TimeSpan.Zero, log: logs.Add);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeFalse();
        probe.ForceCloseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PreferManualOpen_never_starts_shell()
    {
        var starter = new RecordingStarter();
        var logs = new List<string>();
        var notes = new List<string>();
        var life = new SteamLifecycle(new FakeProbe(), starter, log: logs.Add, notify: notes.Add);

        life.PreferManualOpen("please open steam yourself");

        starter.Starts.Should().BeEmpty();
        logs.Should().ContainSingle(x => x.Contains("please open steam", StringComparison.Ordinal));
        notes.Should().ContainSingle();
    }
}
```

- [ ] **Step 2: 跑测确认红灯**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SteamLifecycleTests"
```

Expected: FAIL（类型不存在 / 编译失败）。

- [ ] **Step 3: 实现 `SteamLifecycle.cs`**

把 `MainViewModel.WaitForSteamAndGameExitAsync` 的算法原样迁入（含 softDeadline ≤8s、force-close、cooldown 复检）。日志字符串：

- force-close：`LocalizationService` **不要**放进 Lifecycle；由 `log` 回调接收 **已本地化** 或固定英文内部键。推荐：Lifecycle 用固定英文短日志（测试好断言），VM 包装时再 `AppendLog(LocalizationService.T(...))` **或者** Lifecycle 的 `log` 由 VM 注入为 `AppendLog`，Lifecycle 内部调用 `log(LocalizationService.T("LogSteamForceCloseClient"))` —— **为少改 resx，允许 Lifecycle 依赖 `LocalizationService.T`（项目内已有先例）**。

最小实现要点：

```csharp
public bool IsClientRunning() => _probe.IsSteamRunning();
public bool IsGameRunning() => _probe.IsGameRunning();

public void PreferManualOpen(string message)
{
    _log?.Invoke(message);
    _notify?.Invoke(message);
}

public async Task<bool> EnsureClientAndGameExitedAsync(CancellationToken cancellationToken = default)
{
    if (!_probe.IsGameOrSteamRunning())
        return true;

    if (_probe.IsSteamRunning())
    {
        try { _starter.StartShell("steam://exit"); }
        catch (Exception ex) { _log?.Invoke(/* LogSteamExitRequestFailed fmt */); }
    }

    // soft wait → ForceCloseSteamClientAndGame → wait to deadline → cooldown → recheck
    // honor cancellationToken between delays
    // return false if still running
}
```

**禁止** 任何 `open/games` / `open/` URI。

- [ ] **Step 4: 跑测确认绿灯**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SteamLifecycleTests"
```

Expected: 全部 Passed。

- [ ] **Step 5: 自检**

- [ ] `rg "open/games|steam://open" src/MechabellumModManager/Services/SteamLifecycle.cs` → 无匹配
- [ ] ForceClose 路径有测覆盖

- [ ] **Step 6: Commit**

```powershell
git add src/MechabellumModManager/Services/SteamLifecycle.cs tests/MechabellumModManager.Tests/SteamLifecycleTests.cs
git commit -m "feat: add ISteamLifecycle with exit/force-close and no auto-open"
```

---

### Task 3: MainViewModel 接入 Lifecycle

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`（字段、ctor、`WaitForSteamAndGameExitAsync`、`PreferManualSteamOpen`）

**Interfaces:**
- Consumes: `ISteamLifecycle`（Task 2）
- Produces: VM 行为对外不变；`RecordingStarter` 仍能看到 `steam://exit`

- [ ] **Step 1: ctor 增加可选 `ISteamLifecycle? steamLifecycle = null`**

在现有 `processProbe` / `processStarter` / timeout 字段赋值之后：

```csharp
_steamLifecycle = steamLifecycle ?? new SteamLifecycle(
    _processProbe,
    _processStarter,
    _delay,
    _steamExitTimeout,
    _steamExitCooldown,
    log: AppendLog,
    notify: msg => _notify(msg));
```

保留 `_processProbe` 给诊断/游戏运行检测等非 Lifecycle 用途。

- [ ] **Step 2: 替换方法体**

```csharp
async Task<bool> WaitForSteamAndGameExitAsync() =>
    await _steamLifecycle.EnsureClientAndGameExitedAsync().ConfigureAwait(true);

void PreferManualSteamOpen(string reasonKey)
{
    var msg = LocalizationService.T(reasonKey);
    _steamLifecycle.PreferManualOpen(msg);
}
```

删除旧的内联 exit/force-close 实现（避免双份）。

- [ ] **Step 3: 跑切服相关测试**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~MainViewModelBranchSwitchTests|FullyQualifiedName~SteamLifecycleTests"
```

Expected: Passed。若 `SwitchToBeta_steam_still_running_aborts` 失败：确认 FakeProbe 的 ForceClose 默认 **不** 清 flag（与现测一致），且 Lifecycle 在 timeout=0 时仍会 ForceClose 一次。

- [ ] **Step 4: 自检 grep**

```powershell
rg "steam://open|open/games" src/MechabellumModManager -g "*.cs"
```

Expected: 仅 `GameLauncher` 的 `rungameid` 或注释；**无** `open/games`。

- [ ] **Step 5: Commit**

```powershell
git add src/MechabellumModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: MainViewModel delegates Steam exit/open policy to ISteamLifecycle"
```

---

### Task 4: 去掉 orphan 反射

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`（`TryInspectOrphanDualLayout`、`TryRunOrphanRepairIfAvailableAsync`）

**Interfaces:**
- Consumes: `_branchSwitch.InspectOrphanDualLayout(string)`、`RepairOrphanDualLayoutCommand`
- Produces: 行为与直接调用路径一致

- [ ] **Step 1: 写/确认有覆盖 recovery+orphan 的现有测**（`MainViewModelCriticalOpTests` 等）。若无「反射失败当 false」专用测，不新增产品行为测；本 Task 只保证编译与现测绿。

- [ ] **Step 2: 替换实现**

```csharp
bool TryInspectOrphanDualLayout()
{
    try
    {
        if (string.IsNullOrWhiteSpace(GamePath))
            return false;
        return _branchSwitch.InspectOrphanDualLayout(GamePath).IsOrphan;
    }
    catch
    {
        return false;
    }
}

async Task TryRunOrphanRepairIfAvailableAsync()
{
    try
    {
        if (RepairOrphanDualLayoutCommand.CanExecute(null))
            await RepairOrphanDualLayoutCommand.ExecuteAsync(null).ConfigureAwait(true);
    }
    catch
    {
        // Repair must not block recovery routing.
    }
}
```

- [ ] **Step 3: 确认无反射残留**

```powershell
rg "GetMethod\(\"InspectOrphanDualLayout\"\)|GetProperty\(\"RepairOrphanDualLayoutCommand\"\)" src/MechabellumModManager
```

Expected: 无匹配。

- [ ] **Step 4: 跑测**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~CriticalOp|FullyQualifiedName~Orphan|FullyQualifiedName~BranchSwitch"
```

Expected: Passed。

- [ ] **Step 5: Commit**

```powershell
git add src/MechabellumModManager/ViewModels/MainViewModel.cs
git commit -m "refactor: call orphan inspect/repair directly without reflection"
```

---

### Task 5: 统一测试 Fixture + FakeProcessProbe

**Files:**
- Create: `tests/MechabellumModManager.Tests/Support/FakeProcessProbe.cs`
- Create: `tests/MechabellumModManager.Tests/Support/MainViewModelFixture.cs`
- Modify: `MainViewModelBranchSwitchTests.cs`、`MainViewModelCriticalOpTests.cs`、`MainViewModelTests.cs`、`BranchSwitchServiceTests.cs`、`SteamLifecycleTests.cs`

**Interfaces（Support 对外）：**

```csharp
namespace MechabellumModManager.Tests.Support;

public sealed class FakeProcessProbe : IProcessProbe
{
    public bool GameRunning { get; set; }
    public bool SteamRunning { get; set; }
    public int ForceCloseCalls { get; private set; }
    public bool ForceCloseClearsRunning { get; set; }
    public bool IsGameRunning() => GameRunning;
    public bool IsSteamRunning() => SteamRunning;
    public bool IsGameOrSteamRunning() => GameRunning || SteamRunning;
    public void ForceCloseSteamClientAndGame() { /* same as Task 2 */ }
}

public sealed class MainViewModelFixture : IDisposable
{
    public string DataRoot { get; }
    public string GameRoot { get; }
    public string SteamLink { get; set; }
    public string OfficialStore { get; set; }
    public string BetaStore { get; set; }
    public PathsService Paths { get; }
    public JsonStore Store { get; }
    public ProfileService Profiles { get; }
    public FakeProcessProbe Probe { get; }
    public JunctionService Junctions { get; }
    public CriticalOpGuard? Guard { get; set; } // CriticalOp 场景使用

    public static MainViewModelFixture CreateReady();
    public static MainViewModelFixture CreateReadyDualFolder();
    public static MainViewModelFixture CreateReadyForCriticalOp(); // 含 Guard 路径若需要

    public MainViewModel CreateVm(/* 合并三处可选参数的超集 */);
    public void WriteBranchConfig(BranchSwitchConfig cfg);
    public BranchSwitchConfig LoadBranchConfig();
}
```

`CreateVm` 参数超集至少包含 BranchSwitch 夹具已有项 + `criticalOp` + `requestProcessExit` + `confirmHighRisk` + `updateChecker`（CriticalOp/Main 测需要的）。

- [ ] **Step 1: 抽出 `FakeProcessProbe` 到 Support；`BranchSwitchServiceTests` 删除本地 sealed class，改为 `using MechabellumModManager.Tests.Support`**

- [ ] **Step 2: 以 `MainViewModelBranchSwitchTests.Fixture` 为蓝本写入 `MainViewModelFixture`（含 dual-folder / SeedLibrary / CreateReadyGame）**

- [ ] **Step 3: BranchSwitch 测试类改为 `using Fixture = MainViewModelFixture` 或直接替换类型名；删除私有 Fixture**

- [ ] **Step 4: 迁移 CriticalOp / MainViewModelTests；删私有 Fixture**

- [ ] **Step 5: `SteamLifecycleTests` 改用 `Support.FakeProcessProbe`，删文件内 FakeProbe**

- [ ] **Step 6: 全量相关测试**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~MainViewModel|FullyQualifiedName~SteamLifecycle|FullyQualifiedName~BranchSwitch|FullyQualifiedName~CriticalOpGate|FullyQualifiedName~Orphan"
```

Expected: Passed。

- [ ] **Step 7: 自检**

- [ ] 仓库内仅一处 `class FakeProcessProbe`
- [ ] 三处 VM 测试无重复 `sealed class Fixture`

```powershell
rg "sealed class FakeProcessProbe|sealed class Fixture" tests/MechabellumModManager.Tests
```

Expected: Fixture 仅 Support（或无）；FakeProcessProbe 仅 Support。

- [ ] **Step 8: Commit**

```powershell
git add tests/MechabellumModManager.Tests/
git commit -m "test: unify MainViewModelFixture and FakeProcessProbe in Support"
```

---

### Task 6: Phase0 完成门禁（全绿 + 清单）

**Files:** 无新代码（或仅补漏）

- [ ] **Step 1: 全量测试**

```powershell
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj"
```

Expected: Failed = 0。

- [ ] **Step 2: Spec §3.5 清单自检**

- [ ] Lifecycle 单测存在且绿
- [ ] `MainViewModel` 无 orphan `GetMethod`/`GetProperty` 反射
- [ ] `rg "steam://open|open/games" src -g "*.cs"` 无业务 open（GameLauncher rungameid 除外）
- [ ] `CriticalOpGate` 组合黄金表已合入
- [ ] FakeProcessProbe / MainViewModel Fixture 已统一

- [ ] **Step 3:（可选手工）** 开「每次启动询问账户」→ 一键切服 Busy 退出能清选择器；结算页不自动弹 Steam

- [ ] **Step 4: 若有文档状态行，把 spec 状态改为「Phase0 plan 可执行 / Phase0 完成后勾选」——仅当有未提交文档改动时：**

可选一行 commit：`docs: note Phase0 plan ready`（改 spec 状态字段）——非必须。

- [ ] **Step 5: 最终确认无遗漏 commit；工作区 Phase0 相关文件已提交**

---

## Plan self-review（对 spec §3）

| Spec 要求 | 对应 Task |
|-----------|-----------|
| 3.1 ISteamLifecycle | Task 2–3 |
| 3.2 去 orphan 反射 | Task 4 |
| 3.3 统一 Fixture | Task 5 |
| 3.4 CriticalOpGate 黄金表 | Task 1 |
| 3.5 完成定义 | Task 6 |
| 收编已有 Steam 修复、禁止第三套启停 | Task 3 Step 2 |
| 不做 Policy/Orchestrator/拆 VM | Global Constraints |

**Placeholder scan:** 无 TBD；Fixture 超参用「超集」列出关键项，实现时从三份现有 `CreateVm` 合并签名即可。  
**类型一致:** `EnsureClientAndGameExitedAsync` / `PreferManualOpen(string message)` 在 Task 2–3 一致。

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-06-phase0-safety-pad.md`.

**Two execution options:**

1. **Subagent-Driven（推荐）** — 每 Task 派生子代理，Task 间复核，适合一边自检一边推进  
2. **Inline Execution** — 本会话按 `executing-plans` 连续执行并设检查点  

Which approach?
