# 架构稳定化与清拆（C1）— 设计规格

**日期：** 2026-09-06  
**状态：** 规格已分段确认（§1–§5）；待用户审阅后进入 implementation plan  
**工作目录：** `D:\gongzuo\钢铁指挥官mod管理器开发`  
**策略代号：** C1「稳+清组合 / 保守分波」

---

## 0. 背景与诊断

尸山主要不在「缺少 Service 层」，而在 **编排与门禁叠床架屋**：

| 热点 | 约规模 | 问题本质 |
|------|--------|----------|
| `ViewModels/MainViewModel.cs` | ~3800 行 | 流程编排、六套门禁、Steam 启停、切服状态机堆在一处 |
| `Services/BranchSwitchService.cs` | ~1100 行 | 目录/junction/ACF/orphan/teardown IO 缠结 |
| `MainWindow.xaml` | ~1250 行 | 单壳三页，设置页塞满切服 UI |
| 门禁 | 6 套并行 | SessionLock / CriticalOp / TaskProgress / Busy / Settle / Recovery 各自推导 |

约 45 个 Service、领域 I/O 已相对切开；危险点是 **同一运行真相被多处 OR 再算一遍**，再叠加 Steam 外部不确定性（账户选择器、`steamservice` 残留），易出现「要求关 Steam 却又拉起启动器」类自相矛盾。

本规格 **不** 做大爆炸重写；按可独立合并/发版的四期推进。

---

## 1. 目标与非目标

### 1.1 目标

1. **单一权威运行相位合成**：`IsSessionLocked`、关闭/更新 Soft·Hard、`DeployBlockedReason` 只从一个纯策略读取组合结果。  
2. **Steam 生命周期单一所有者**：退出/强制结束客户端、禁止业务路径乱 `steam://open`；需要下载时提示手动打开。  
3. **切服编排外移**：向导 / Ready 切服 / settle / teardown 由 Orchestrator 拥有步骤迁移；磁盘真相仍是 `BranchSwitchConfig`。  
4. **最后再拆 UI VM**：在内核稳定后降低 `MainViewModel` 体积与改动半径。  
5. **全程可回归**：每期有明确完成定义与测试护栏。

### 1.2 非目标（整包 C1 不做）

- 换 UI 框架或 DI 容器大搬家  
- 重做 Melon 安装协议、目录/社区后端  
- 重构 PR 夹带切服 **产品行为** 变更（行为变更走独立 bugfix）  
- 强制结束 `steamservice` 或修改用户 Steam「每次询问账户」系统设置  
- 一次性删光历史 design/plan 文档  
- Phase3 顺手改视觉体系

### 1.3 路线选择

| 变体 | 结论 |
|------|------|
| C1 保守分波 | **采用**：Phase0→1→2→3，每波可发版、可回滚 |
| C2 双轨并行 | 不采用：门禁与门面同抢 `MainViewModel`，冲突大 |
| C3 一次抽干 | 不采用：回归面过大 |

---

## 2. 分期总览

| 期 | 名称 | 做什么 | 刻意不做 | 可发版 |
|----|------|--------|----------|--------|
| **0** | 安全垫 | `ISteamLifecycle`；去 orphan 反射；统一 VM 测试 Fixture；加厚 `CriticalOpGate` 黄金表 | 不做 Session Policy / Orchestrator；不拆 VM；不改向导语义 | 是 |
| **1** | 门禁单一真相 | `ManagerSessionFacts` + `ManagerSessionPolicy`（`DominantPhase` 仅作文案标签，非互斥状态机）；SessionLocked / CloseGate / DeployBlock 只读 Snapshot | 不拆 MainViewModel 文件；不大拆 BranchSwitchService；不搬向导编排 | 是 |
| **2** | 切服状态机 | `BranchSwitchOrchestrator`；Settle 去双真相；可选第二 PR 拆 Service IO | 不拆 Catalog/Library VM | 是 |
| **3** | UI 清拆 | 子 VM + 按页 UserControl；壳转发过渡 | 不改业务规则 | 是 |

**稳定性护栏（全程）：**

1. 行为变更必须先有失败用例再绿（尤其 Steam / Classify / settle）。  
2. 相位/门禁合成表文档化 + 测试；禁止新增平行布尔门而不登记 Facts。  
3. 每期结束后允许发版；不要求一次清完尸山。

---

## 3. Phase0 — 安全垫

### 3.1 `ISteamLifecycle`

新建例如 `Services/SteamLifecycle.cs`。

```text
ISteamLifecycle
  Task<bool> EnsureClientAndGameExitedAsync(CancellationToken ct = default)
  void PreferManualOpen(string messageOrKey)   // 只日志/通知；禁止 StartShell(open/games)
  bool IsClientRunning() / IsGameRunning()  // 委托 ProcessProbe（不含 steamservice）
```

| 约定 | 内容 |
|------|------|
| 依赖 | `IProcessProbe`、`IProcessStarter`、delay、超时/冷却 |
| 退出策略 | 仅客户端在跑时 `steam://exit` → 短等 → `ForceCloseSteamClientAndGame` → 再等 → cooldown 复检 |
| 硬约束 | 门面 **永不** `steam://open/*`；`GameLauncher` 的 `rungameid` 不经 Lifecycle |
| VM | 删除或薄转发 `WaitForSteamAndGameExitAsync` / `PreferManualSteamOpen` |

**可行性：** 高（逻辑已在 VM）。  
**连锁：** `RecordingStarter` 断言仍有效；ctor 可内部 `new SteamLifecycle` 减少测试改动。  
**稳定性：** 堵住 settle/向导再次自动弹启动器。

### 3.2 去掉 orphan 反射

`TryInspectOrphanDualLayout` / `TryRunOrphanRepairIfAvailableAsync` 改为直接调用 `_branchSwitch.InspectOrphanDualLayout` 与 `RepairOrphanDualLayout` 命令路径。  
反射失败当「无 orphan」的静默分支删除。

### 3.3 统一测试 Fixture

- 抽出 `tests/.../Support/MainViewModelFixture.cs`（名称可微调）  
- 合并 `MainViewModelBranchSwitchTests` / `MainViewModelCriticalOpTests` / `MainViewModelTests` 私有 Fixture  
- `FakeProcessProbe` 只保留一份  
- 不强制合并 Diagnostics / BranchSwitchService 领域夹具  

### 3.4 `CriticalOpGate` 黄金表加厚

在现有 Theory 上补：Recovery/Guard 压过 Soft；Busy+Settle、Wizard+Busy 等组合。  
**默认不改 Classify 语义**；若与文档冲突，单开 bugfix。

### 3.5 Phase0 完成定义

1. 相关 `dotnet test` 全绿（含新 Lifecycle 单测）。  
2. `MainViewModel` 无 orphan 的 `GetMethod`/`GetProperty` 反射。  
3. 业务路径无 `steam://open`（`GameLauncher` 除外）。  
4. 可选手动：账户选择器开启时 Busy 退出能清客户端；结算不自动弹启动器。

---

## 4. Phase1 — ManagerSession 单一真相

### 4.1 不做假互斥枚举一把梭

多旗可并存。采用：

```text
ManagerSessionFacts     ← 从现有布尔/任务条采集
ManagerSessionSnapshot  ← Policy.Evaluate 输出
  DominantPhase         ← 仅状态条文案优先级
  SessionLocked
  CloseGateLevel
  DeployBlockKey        ← 资源键或空
```

**DominantPhase 优先级（仅标签）：**  
Recovery → CriticalDiskWrite（Guard.Running）→ BranchBusy → AwaitingSteamSettle → WizardInProgress → TaskSessionLock → Idle  

### 4.2 API

| 符号 | 职责 |
|------|------|
| `ManagerSessionFacts` | 只读事实袋 |
| `ManagerSessionPolicy.Evaluate` | 纯函数 → Snapshot |
| `CriticalOpGate.Classify` | 保留或由 Evaluate 调用；Phase0 黄金表继续有效 |

VM：`CollectSessionFacts` → `Evaluate`；`IsSessionLocked` / `EvaluateCloseOrUpdateGate` / `DeployBlockedReason` 只读 Snapshot。  
现有字段仍作 **事实源**；Phase1 只统一合成。

### 4.3 语义冻结（与今日对齐）

| 输出 | 规则 |
|------|------|
| SessionLocked | Recovery ∨ AwaitSettle ∨ WizardInProgress ∨ BranchBusy ∨ (Task.HasTask ∧ SessionLock)。**不含** Guard.Running |
| CloseGate | Recovery / Guard → Hard；BusyDialog ∨ BranchBusy → Soft；AwaitSettle ∨ Wizard → Soft；否则 Allow |
| DeployBlock | 先 NotReady（GameStatus）；再 Recovery → Settle → Wizard → Busy/TaskLock → Generic |
| CanDeployOrLaunch | `IsReady && !SessionLocked` |

禁止在 Phase1 PR 里 silent 改产品行为。

### 4.4 测试与完成定义

- Policy 表驱动单测（含多旗）  
- Classify 黄金表绿  
- 现有 CriticalOp / BranchSwitch 烟测证明门禁未漂  
- 规范：新门禁布尔必须进 Facts + Policy  

---

## 5. Phase2 — BranchSwitchOrchestrator

### 5.1 职责

| 组件 | 拥有 | 不拥有 |
|------|------|--------|
| `BranchSwitchOrchestrator` | 步骤迁移；Ready 切服；向导 Start/Resume/Continue；Enter/Clear Settle；调 Lifecycle + BranchSwitchService；Busy 经回调 | WPF 绑定；Confirm 文案可由端口注入 |
| `BranchSwitchService` | 目录/junction/ACF/orphan/teardown **原子 IO** | 弹窗时机、用户「继续」 |
| `MainViewModel` | 命令入口、确认、绑定、Deploy/Melon 回调 | 不再散落双写 `SetWizardStep` |

**持久化铁律：** `WizardStep` / `Enabled` / paths / `ActiveBranch` 只经 Orchestrator → SaveConfig。  
`BranchWizardStep` 枚举与 JSON **兼容不变**。

### 5.2 Settle 去双真相

- **权威：** `Enabled && WizardStep == AwaitingSteamSettle`  
- VM 布尔仅为镜像；禁止只拨布尔不改 Step  
- `DegradeToManualBeta` 仍进程内、不写 JSON  

### 5.3 可选：拆 BranchSwitchService（建议第二 PR）

- ACF snapshot/silent beta → 独立类型  
- Orphan inspect/repair → 独立类型  
- `BranchSwitchService` 作门面委托  

先 Orchestrator + 测试绿，再拆 IO，避免同 PR 双爆炸。

### 5.4 完成定义

1. 主路径长方法离开 VM（薄命令层）。  
2. Step 写入收敛；Settle 无孤立布尔。  
3. `MainViewModelBranchSwitchTests` 全绿 + 迁移表单测。  
4. Official-first teardown 测试保留（风险 R4）。  

---

## 6. Phase3 — UI 清拆

**前置硬门：** Phase2 已合并且相关测试绿。

| 子 VM | 内容 |
|-------|------|
| `BranchSettingsViewModel` | 双目录/切服/向导结算/teardown/orphan |
| `LibraryViewModel` | 库、导入、筛选、脏状态 |
| `CatalogViewModel` | 目录、多选添加 |
| `ProfileBarViewModel`（可选） | 方案条、应用/启动 |
| 壳 `MainViewModel` | 组合、GamePath/语言/更新/诊断/关闭门禁、Facts 采集、日志、翻页 |

XAML：先抽 UserControl **不改绑定路径**，再切到子 VM；可用壳转发过渡。

---

## 7. 风险登记

| ID | 风险 | 阶段 | 缓解 |
|----|------|------|------|
| R1 | 再次引入 `steam://open` | 0+ | Lifecycle 禁止 + 评审/grep |
| R2 | Soft/Hard 关闭语义漂 | 1 | Classify/Policy 黄金表 |
| R3 | WizardStep 与 VM 镜像分叉 | 2 | 单一写入 + Load 往返测 |
| R4 | Official-first teardown 丢失 | 2 | 专用测试保留 |
| R5 | Phase3 绑定漏导致按钮死 | 3 | 壳转发；手工点检 |
| R6 | Fixture 合并假绿 | 0 | 全量 `dotnet test` |

---

## 8. 建议交付节奏（用户可感知）

| 里程碑 | 用户可感知 |
|--------|------------|
| Phase0 | Steam 启停更稳（含不自动弹账户选择器） |
| Phase1 | 几乎无感；内部不易再加错门 |
| Phase2 | 无感或日志更清晰；编排 bug 更好修 |
| Phase3 | 无感；后续改设置页不再挖 3800 行 |

---

## 9. 与近期 Steam 修复的关系

2026-09-06 会话中已落地的方向（ProcessProbe 忽略 `steamservice`、禁止 settle 自动 `open/games`、exit 后 force-close 客户端）与 **Phase0 Lifecycle** 一致，应在 Phase0 中 **收编进门面** 并补单测，而不是另起第三套启停逻辑。

---

## 10. 后续流程

1. 用户审阅本 spec，提出修改则改文档后再确认。  
2. 确认后调用 **writing-plans**，优先写 **Phase0** 实现计划（可按期拆 plan 文件）。  
3. 未批准 plan 前不大规模开拆；行为 bugfix 仍可独立进行。
