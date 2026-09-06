# 双服假死 / 解除 / Steam 结算 / 诊断加强 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复测试员暴露的四类问题（Melon 生成假死与僵尸进程、解除双服无 Busy/未先回正式服、Steam 未结算硬继续、诊断信息不足），并让「导出诊断包」尽量自带结论，减少测试员额外口述与二次操作。

**Architecture:** 在现有 CriticalOp / Busy / BranchSwitch / DiagnosticsProbe 上增量加固：解除双服强制先切正式服并 Busy；关窗与 Busy 取消保证进程可退出；结算门禁识别 `StateFlags` 更新失败态；诊断包增加 timeline 摘要、`summary.md` 人话结论与自动建议，导出时尽量附带 ACF 片段与 junction 探测结果。

**Tech Stack:** .NET 8 WPF、现有 `MainViewModel` / `BranchSwitchService` / `DiagnosticsExportService` / `DiagnosticsProbeBuilder`、xUnit。

## Global Constraints

- 不重开 Melon 程序集 CriticalOp 标记设计（保持 Busy-only）。
- 不削弱「ACF 未 settled 不得假 Ready」的既有修复。
- 诊断默认仍可用 Strong 脱敏；新增字段不得写入明文账号密码。
- 用户可见文案走 `Strings*.resx`（至少 zh-CN + en）。

**Evidence baseline:** 测试员诊断 `…-113215.zip` / `…-115312.zip`（假死→僵尸→解除→sharedassets 损毁→再次向导卡 518）。

---

## File map

| 区域 | 主要文件 |
|------|----------|
| 解除双服 | `MainViewModel.cs`（teardown 命令流）、`BranchSwitchService.TryTeardown` |
| Busy / 关窗 | `App.xaml.cs`、Busy 宿主、`MainWindow` Closing、`CriticalOpGate` |
| Melon 生成 | `EnsureMelonAssembliesForStoreAsync`、Busy 文案 |
| Steam 结算门禁 | `SteamBetaKeyEditor`、结算 Continue、`IsAwaitingSteamSettle` |
| 诊断 | `DiagnosticsExportService`、`DiagnosticsProbeBuilder`、`ExportDiagnosticsDialog` |
| 测试 | `tests/...` 对应 BranchSwitch / Diagnostics / CriticalOp |
| 说明 | `docs/分发-使用说明.md` 或 RELEASE 一小段 |

---

## Task 1 — 解除双服：Busy + 强制先回正式服

**Why:** 测服态直接解除 → Steam `sharedassets` 损毁；且无「正在解除中」。

- [x] **Step 1:** 写失败单测（或扩展现有）：`activeBranch=Beta` 时 teardown 入口必须先请求切到 Official（mock BranchSwitch），未切成功不得调用 `TryTeardown`。
- [x] **Step 2:** 跑测确认红灯。
- [x] **Step 3:** 在 `MainViewModel` 解除流程中：若当前非 Official，先走与「切换到正式服」相同的切服/等待路径（含 Steam 门禁）；成功后再 `TryTeardown`；全程 `RunBusyAsync`（文案：正在切换到正式服… / 正在解除双服配置…）。
- [x] **Step 4:** 确认对话框补充一句：「将先切换到正式服，再解除双目录」。
- [x] **Step 5:** 单测绿灯；手动清单：测服态点解除 → 见 Busy → 结束后 Steam/目录为正式、无 junction。
- [ ] **Step 6:** Commit：`fix: teardown dual-folder via Official first with Busy`

---

## Task 2 — 僵尸进程：关窗 / Busy 取消必须能退出

**Why:** UI 无响应后关窗，进程残留占用 exe → Setup 无法关闭应用。

- [x] **Step 1:** 复盘 Closing + CriticalOp Soft/Hard：Busy/Melon 生成中关窗是 SoftConfirm 还是直接藏窗；写清期望「用户确认退出后进程结束」。
- [x] **Step 2:** 实现：用户确认退出时取消 Melon/Busy CTS，等待有限超时（如 5–10s）后仍阻塞则 `Application.Current.Shutdown` / 确保主进程退出（避免只 Hide）。
- [x] **Step 3:** 安装包旁路：`docs` 或安装前检测文案加强「若提示无法关闭，请任务管理器结束 Mechabellum Mod Manager」（可只做文档若 Inno 已有逻辑）。
- [x] **Step 4:** 单测或手工：Busy 中点关 → 确认 → 任务管理器无残留。
- [ ] **Step 5:** Commit：`fix: ensure process exit after confirmed close during Busy`

---

## Task 3 — Melon 程序集生成：防再次假死

**Why:** 11:25 开始生成后长时间无 UI 反馈直至路径异常。

- [x] **Step 1:** 审计 `EnsureMelonAssembliesForStoreAsync` 是否仍有同步阻塞或未打 Busy；确认超时与取消。
- [x] **Step 2:** 增加生成超时（可配置，默认如 180–300s）：超时取消、写日志、Busy 结束并提示「生成超时，可稍后重试 / 导出诊断」，禁止无限挂起。
- [x] **Step 3:** 进度日志：开始 / 心跳（每 30s）/ 成功 / 超时 / 取消，全部进 manager 日日志。
- [x] **Step 4:** 单测：超时路径不抛未处理异常、Busy 会 Complete。
- [ ] **Step 5:** Commit：`fix: Melon assembly gen timeout + heartbeat logs`

---

## Task 4 — Steam 结算硬门禁（更新失败 / 518）

**Why:** `StateFlags=518` + 更新失败时点继续只会反复弹「未就绪」。

- [x] **Step 1:** 在 `SteamBetaKeyEditor` / probe 增加 `UpdateUnhealthy`（或复用 settled + StateFlags 含 UpdateStarted/UpdateRequired 且 Bytes 全 0）判定。
- [x] **Step 2:** 结算 Continue：若 unhealthy，**Hard 提示**固定文案（先验证游戏文件 / 等更新成功），不进入部署。
- [x] **Step 3:** 设置页「Steam 已切换…继续」按钮旁或点击时显示同一原因（可绑定 `SettleBlockReason`）。
- [x] **Step 4:** 单测：518 + bytes0 → Continue 失败消息匹配。
- [ ] **Step 5:** Commit：`fix: block Steam settle while update unhealthy`

---

## Task 5 — 诊断加强（减少测试员额外劳动）**【本版重点】**

**Why:** 测试员需截图+口述；诊断包未自带「人话结论」与关键时间线。

- [x] **Step 1:** 导出时新增根文件 **`summary.md`（或 `summary.txt`）**，自动生成，含：
  - 应用版本、导出时间、向导步骤、activeBranch
  - findings 中文解释（映射表）
  - **建议下一步**（1–3 条，按 findings 规则引擎）
  - 最近 session/manager 日志尾部 40 行（已脱敏）
- [x] **Step 2:** `environment.json` / probe 增补：
  - `acf.StateFlagsDecoded`（如 UpdateRequired|FullyInstalled|UpdateStarted）
  - `updateUnhealthy: bool`
  - `junctionTarget`、三路径 `looksLikeGameRoot`（已有则保留）
  - `melonAssemblyGen`：若日志能解析到最近一次生成开始/结束/超时
- [x] **Step 3:** 新增 **`timeline.jsonl`**：从当日 manager 日志解析关键事件（向导完成、等待结算、部署、解除、Melon 生成开始/等待、导出），降低「要测试员回忆点了什么」。
- [x] **Step 4:** 导出 UX：成功后 MessageBox / 日志明确写「已生成 summary.md，把整个 zip 发维护者即可」；可选一键复制 `summary.md` 正文到剪贴板（国内邮件场景）。
- [x] **Step 5:** 单测：给定假 probe+log 片段 → summary 含 findings 与建议句。
- [ ] **Step 6:** Commit：`feat: diagnostics summary + timeline for less tester toil`

---

## Task 6 — 验收与发版说明

- [x] **Step 1:** 对照测试员剧本写最短回归清单（解除测服态、Busy 关窗、518 点继续、导出看 summary）。
- [x] **Step 2:** `RELEASE_NOTES` / 群公告要点 4 条修复 +「诊断包自带 summary」。
- [ ] **Step 3:** Commit：`docs: v1.1.x dual-folder freeze/teardown/diagnostics notes`

---

## 测试员剧本（修复后应几乎只需「导出 zip」）

1. 按提示操作；若失败，只点「导出诊断包」发 zip。  
2. 维护者先读 `summary.md`，再下钻 `environment.json` / `timeline.jsonl`。  
3. 原则上不再要求：额外口述 StateFlags、是否僵尸进程、是否测服解除（summary 应写明）。

---

## 建议实现顺序

`Task 5`（诊断）可与 `1–4` 并行后半段，但 **1 → 2 → 3 → 4** 按用户痛感优先；发预览包给原测试员时务必带 Task 5，减少第二轮拉锯。

## 非目标（本版不做）

- 自动修复 Steam「更新失败」
- 官方沙盒房协议
- 重写整个 BranchSwitchService
