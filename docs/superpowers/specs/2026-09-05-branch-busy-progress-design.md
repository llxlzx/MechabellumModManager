# 双服忙碌进度窗设计

> **状态**：待用户审阅 spec  
> **日期**：2026-09-05  
> **仓库**：Mechabellum Mod Manager  
> **用户确认**：范围 B（向导归档搬家 + 日常切服）；方案 1（`BusyProgressDialog` + VM 回调）；不确定进度 + 阶段文案；不可取消

---

## 1. 背景与目标

测试员反馈：双服目录移动实际成功，但确认对话框关闭后长时间无提示，主界面像「什么都没发生」，容易误以为失败并强制关管理器。

**目标**

- 在耗时磁盘/等待操作期间弹出**模态进度窗**，明确告知「正在移动 / 等待 Steam…，请勿关闭」
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
| 何时弹出 | 用户确认「开始向导 / 切服」之后、进入耗时步骤之前 |
| 何时关闭 | 该次流程成功、失败或中止后立刻关闭（`finally` 保证） |
| 窗口行为 | Owner = 主窗；`Show()`（非 `ShowDialog`）；禁用 Owner 交互；禁止用户关窗（无取消按钮，拦截关闭） |
| 阶段文案（示例） | 「正在请求退出 Steam…」→「正在移动游戏目录，请勿关闭管理器…」→「正在创建目录联接…」→「正在切换 Steam 分支…」等，按实际步骤切换 |
| 主界面 | 继续用 `IsBranchSwitchBusy` 灰按钮 / 防重入；进度窗为额外反馈，不替代 Busy 标志 |

确认类对话框仍先于进度窗出现；确认取消则**不**开进度窗。

---

## 3. 架构

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `BusyProgressDialog` | 展示不确定进度条 + 标题/说明；拦截关闭 | 业务逻辑 |
| `App` 注入回调 | `beginBusy(title, message)` / `setBusyMessage(message)` / `endBusy()`；持有至多一个实例 | 知道向导步骤细节 |
| `MainViewModel` | 在切服 / 向导耗时段调用回调；`try/finally` 成对 `endBusy` | 窗口生命周期细节 |
| `IsBranchSwitchBusy` | 门闩与 UI 按钮状态（已有） | 用户可见「正在移动」文案 |

**为何不用 `ShowDialog()`：** 当前流程在 UI 线程上 `await`；`ShowDialog` 会卡住同一异步方法，无法在关窗前继续执行搬家逻辑。应使用 `Show()` + 禁用 Owner，工作完成后 `Close()`。

**线程：** `Directory.Move` / 复制回退等盘操作尽量 `await Task.Run(...)`，保证进度条动画不被同步 IO 卡死；文案更新回到 UI 线程。

---

## 4. 覆盖点（范围 B）

1. **日常切服** `SwitchToBranchAsync`  
   确认后 → 等待 Steam/游戏退出 → 交换目录 → 后续静默/结算相关等待（若仍属同一次 Busy）→ `finally` 关窗  

2. **双服向导** 中与归档/搬家相关的耗时段  
   例如：`WaitForSteamAndGameExitAsync`、`ArchiveCurrentAs`、`ArchiveDownloadedAs`、创建联接等；阶段文案随步骤更新  

**本轮明确不做：** `RunTeardownCoreAsync`、Melon 安装、诊断导出。

---

## 5. 本地化

新增键写入 `Strings*.resx`（zh/en/de/ja/ru），例如：

- `BusyProgressTitle`（窗口标题）
- `BusyWaitingSteam`
- `BusyMovingGameFolder`
- `BusyCreatingJunction`
- `BusySwitchingSteamBranch`
- （若有其它明确阶段再补键，避免硬编码中文）

说明文案需强调：**请勿关闭管理器 / 不要手动结束进程**。

---

## 6. 错误与隔离

- 任意异常 / `FailWizard` / Steam 等待失败：必须 `endBusy`，再走现有 `_notify` / `AppendLog`
- 重复 `beginBusy`：关闭旧窗或忽略二次 Begin（实现选一种并保持一致；推荐「已有则只更新文案」）
- 导出诊断、部署等路径不调用 Busy 回调
- 不改变 `branch-switch.json` / 目录布局的成功失败条件

---

## 7. 测试

- VM：注入 fake busy 回调，断言切服/向导路径在耗时段调用了 begin/set/end，且失败路径也会 end  
- 可选：对话框本身的「Closing 被取消」单元级较难测，以手工为准  
- 回归：现有 BranchSwitch 相关测试仍绿；不要求测真实 Directory.Move 动画

---

## 8. 自检记录（写 spec 时）

| 项 | 处理 |
|----|------|
| ShowDialog 与 async 冲突 | 明确用 Show + 禁用 Owner |
| 漏关窗 | finally + endBusy；失败路径同 |
| 范围越界 | 写明不做 teardown |
| 业务语义 | 只加反馈与 Task.Run 包装，不改成功条件 |
| 与 Busy 标志关系 | 并存，不互相替代 |
