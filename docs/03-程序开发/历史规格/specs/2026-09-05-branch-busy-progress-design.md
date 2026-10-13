# 双服忙碌进度窗设计

> **状态**：系统性自检后修订，可写实现计划  
> **日期**：2026-09-05  
> **仓库**：Mechabellum Mod Manager  
> **用户确认**：范围 B（向导归档搬家 + 日常切服）；方案 1（`BusyProgressDialog` + VM 回调）；不确定进度 + 阶段文案；不可取消

---

## 1. 背景与目标

测试员反馈：双服目录移动实际成功，但确认对话框关闭后长时间无提示，主界面像「什么都没发生」，容易误以为失败并强制关管理器。

**目标**

- 在耗时磁盘/等待操作期间弹出**交互模态进度窗**，明确告知「正在移动 / 等待 Steam…，请勿关闭」
- 覆盖：**双服向导归档搬家**与**日常切服目录切换**
- 不确定进度（走马灯）+ 可切换阶段文案；**无取消、无百分比**

**非目标**

- 不改归档 / 切服 / Steam 结算业务语义与结果
- 本轮不做 teardown、Melon 安装、诊断导出等其它长操作的进度窗
- 不做可取消搬家（避免半搬状态）
- 不做真实字节百分比

---

## 2. 交互

| 项 | 约定 |
|----|------|
| 何时弹出 | 所有**需要用户点选的确认/选择对话框结束之后**，进入耗时步骤之前 |
| 何时关闭 | 本段耗时操作结束（成功/失败/中止）立刻关；**进入下一次确认/通知/等人操作之前必须先关** |
| 窗口行为 | Owner = 主窗；**技术上用 `Show()`（非 `ShowDialog`）**；禁用 Owner 交互；拦截进度窗关闭；建议在 Busy 期间取消主窗 `Closing` |
| 「模态」含义 | 用户无法操作主窗、无法关掉进度窗；**不是**嵌套 `ShowDialog` 卡住异步方法 |
| 阶段文案 | 「正在请求退出 Steam…」→「正在移动游戏目录，请勿关闭管理器…」→「正在创建目录联接…」→「正在写入 Steam 分支信息…」等 |
| 主界面 | 继续用 `IsBranchSwitchBusy` 灰按钮 / 防重入；进度窗为额外反馈 |

确认取消则**不**开进度窗。

---

## 3. 架构

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `BusyProgressDialog` | 不确定进度条 + 标题/说明；拦截关闭 | 业务逻辑 |
| `App` 注入回调 | `beginBusy(title, message)` / `setBusyMessage(message)` / `endBusy()`；至多一个实例；禁用/恢复 Owner | 向导步骤细节 |
| `MainViewModel` | 在切服 / 向导**各段**耗时区调用回调；`try/finally` 成对 `endBusy` | 窗口句柄细节 |
| `IsBranchSwitchBusy` | 门闩与按钮状态（已有） | 「正在移动」文案 |

**为何不用 `ShowDialog()`：** 流程在 UI 线程 `await`；`ShowDialog` 会卡住同一异步方法。用 `Show()` + 禁用 Owner，结束后 `Close()`。

**线程：** `ArchiveCurrentAs` / `ArchiveDownloadedAs` / `TrySwapJunction` / `CreateLinkTo` 等同步盘操作在调用处 `await Task.Run(...)`，避免卡死进度条动画；文案更新回 UI 线程。

**重复 begin：** 若已有打开的 Busy，则只更新文案（不叠第二个窗）。

---

## 4. 覆盖点与分段（范围 B）— 自检修订

代码里向导会在中途再次 `Confirm(...)`（例如「另一服下载完后继续」），且切服结束会 `EnterSteamSettle` + `_notify`。进度窗与确认框**不能同时开**（Owner 已禁用时确认体验差/易死锁感）。

### 4.1 日常切服 `SwitchToBranchAsync`

1. `ConfirmSwitchGameBranch`（无 Busy）  
2. **beginBusy** → `WaitForSteamAndGameExitAsync` → `TrySwapJunction`（`Task.Run`）→ `TryPrepareSteamBranchMetadata` 等仍属搬迁链路的步骤  
3. **endBusy**（`finally`）  
4. 再进入 `SettleAfterSilentBetaAsync` / `_notify` / 等人点「确认结算」（无 Busy）

说明：`IsBranchSwitchBusy` 可在整段（含 settle 前）保持现有 true/false 语义；**进度窗只盖住「退出 Steam + 挪目录」**，不要盖住 settle 等待用户的阶段。

### 4.2 向导 `RunWizardFromStartAsync` / `ResumeWizardAsync`

预检对话框全部结束后（确认开始、选当前服、校验路径等）再开 Busy：

| 段 | Busy | 内容 |
|----|------|------|
| 段 A | 开 | 首次 `WaitForSteam…` + `ArchiveCurrentAs` |
| 段 A 结束 | 关 | 然后进入「等另一服下载 / 用户确认继续」交互（`ConfirmDownloadOtherBranchContinue` 等） |
| 段 B | 开 | 确认后再次 `WaitForSteam…` + `ArchiveDownloadedAs` |
| 段 C | 开 | `CreateLinkTo`（可与段 B 同一 Busy 连续，或关开各一次；推荐与 ArchiveB 同一次 Busy 直到建联完成） |
| 失败 / Steam 等待失败 | 关 | 再 `FailWizard` / `_notify` |

`ResumeWizardAsync` 从中途恢复时：只对**仍会执行的耗时段**开 Busy（例如已 ArchivedA、正要 ArchiveB / CreateLink）。

### 4.3 本轮明确不做

`RunTeardownCoreAsync`、Melon 安装、诊断导出、仅 `IsAwaitingSteamSettle` 的长时间等人。

---

## 5. 本地化

新增键写入 `Strings*.resx`（zh/en/de/ja/ru）：

- `BusyProgressTitle`
- `BusyWaitingSteam`
- `BusyMovingGameFolder`
- `BusyCreatingJunction`
- `BusySwitchingSteamBranch`（切服准备 Steam 元数据时可用）
- 可选副文案键强调「请勿关闭管理器」——可并入各阶段字符串末尾，避免过多键

禁止在 VM 里硬编码中文阶段句。

---

## 6. 错误与隔离

- 任意异常 / `FailWizard` / `AbortWizardAfterFailure` / Steam 等待失败：该段 `finally` 必须 `endBusy`，再通知用户  
- 部署、诊断导出等路径不调用 Busy 回调  
- 不改变 `branch-switch.json` / 目录布局成功失败条件  
- Busy 开启时主窗 Closing → Cancel（避免用户关主程序导致半搬）

---

## 7. 测试

- VM：fake `begin/set/end` 计数；切服确认后至少 begin+end；Steam 等待失败也要 end  
- 向导：Archive 前 begin；在会弹出 `ConfirmDownloadOtherBranchContinue` 之前必须已 end（可用 spy 顺序断言）  
- 回归：现有 BranchSwitch 测试仍绿  

---

## 8. 系统性自检（修订版）

| 项 | 结论 |
|----|------|
| ShowDialog vs async | 用 Show + 禁用 Owner；文案写清「交互模态」 |
| 向导中途 Confirm | **必须分段 endBusy**；原「一次 Busy 盖全向导」会挡确认框 → 已修订 §4 |
| Settle / `_notify` | 切服 Busy 在 settle 前结束 |
| Resume 向导 | §4.2 写明按剩余耗时段开窗 |
| 漏关窗 | 每段 try/finally |
| 主窗强关 | Busy 期间取消 MainWindow.Closing |
| 动画卡死 | 盘操作 Task.Run |
| 范围越界 | teardown 等仍非目标 |
| 占位/矛盾 | 「模态」与实现手段已对齐；无 TBD |
| 与 Busy 标志 | 并存；进度窗生命周期可短于 `IsBranchSwitchBusy` |
