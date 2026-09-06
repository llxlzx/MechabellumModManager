# 空壳仓 / 假 Ready 硬门 — 设计规格（独立热修）

**日期：** 2026-09-06  
**状态：** 规格待用户审阅  
**工作目录：** `D:\gongzuo\钢铁指挥官mod管理器开发`  
**策略：** 独立热修（不并入 C1 Phase1）；可先发版防空壳，再继续架构分波  
**关联：** 现场诊断（Official 空壳 + ACF `StateFlags=1190` + `WizardStep=Ready`）；C1 见 `2026-09-06-architecture-stability-cleanup-design.md`

---

## 0. 背景与问题

双目录启用且配置显示「就绪」时，激活仓可能只剩 `Mechabellum.exe`（或缺 `GameAssembly.dll`），Steam 清单带 `FilesMissing` / `FilesCorrupt` / 未结算，用户仍感觉管理器「已就绪」并可继续操作。根因不是缺少诊断码（已有 `active_store_incomplete` / `acf_not_settled`），而是 **诊断未变成硬门**，外加结算路径在快照失败时仍可能 `ClearSteamSettle` 升到 `Ready`。

现有部分护栏：

- `SteamGameLocator.LooksLikeGameRoot` / `GameDetector`：exe + `GameAssembly.dll`
- `TrySwapJunction`：允许离开空壳仓，只要目标仓是完整根
- 部分测试：ACF 未结算、结算时缺 exe → 不得清 settle

缺口：假 `BranchWizardStep.Ready`、Corrupt/大下载量 ACF 未硬拦清门、快照失败 deferred clear、向导过早「继续」。

---

## 1. 目标与非目标

### 1.1 目标

1. **单一健康判定入口**（`BranchStoreHealth`）：仓是否像游戏根、ACF 是否允许快照/清 settle。  
2. **禁止假 Ready**：空壳激活仓不得保持或升到 `WizardStep.Ready`；不得在未健康时 `ClearSteamSettle`。  
3. **快照失败绝不升 Ready**：删除「Deploy 已成功则 snapshot best-effort 仍 ClearSteamSettle」行为。  
4. **自动降级**：`Enabled && WizardStep==Ready` 且激活仓/link 非游戏根 → 退出 Ready，进入待修/settle，提示 Steam 验证；不删仓目录。  
5. **逃生通道**：降级后仍允许切到另一侧**完整**仓、teardown/修复、导出诊断。  
6. **可回归测试**：空壳降级、Corrupt 不清门、快照失败不清门、空壳可切完整仓、根完整时短暂 FilesMissing 不降级。

### 1.2 非目标

- 不查 Unity Data / `level0` / `level20`（留待后续加固）  
- 不做贴近 Steam 校验的体积/清单全量对照  
- 不并入 C1 Phase1 `ManagerSessionPolicy` / Orchestrator  
- 不改 Melon 安装协议、不删用户仓目录、不强制结束 `steamservice`  
- 不拦用户在 Steam 客户端内手动点「开始游戏」（管理器外无法拦截）

### 1.3 产品选择（已确认）

| 项 | 选择 |
|----|------|
| 排期 | 独立热修 |
| 完整根定义 | 现有：`Mechabellum.exe` + `GameAssembly.dll` |
| Ready 空壳 | 自动降级到待修/settle |
| 架构 | 集中 `BranchStoreHealth`，各路径接线 |

---

## 2. 方案与否决项

| 方案 | 结论 |
|------|------|
| **集中健康门 `BranchStoreHealth`** | **采用** |
| 各调用点散落 if | 否决：易漏，与 deferred clear 同类复发 |
| 并入 C1 Phase1 Policy | 否决：与独立热修冲突；本热修结果可被 Phase1 日后复用 |

---

## 3. 防连锁约束（自检结论，必须遵守）

1. **降级触发仅看游戏根**：仅当双目录已启用、`WizardStep==Ready`，且**当前激活仓或 Steam link** `!LooksLikeGameRoot`。禁止「仅因 ACF 短暂 `FilesMissing` 且根仍完整」就降级（避免正常更新把管理器锁死）。  
2. **降级不得锁死逃生**：进入 settle/待修后，`IsSessionLocked` 可挡住部署/对空壳启动，但 **必须仍能**：切到另一侧完整仓、teardown/orphan 修复、导出诊断。禁止「进 settle → `CanSwitchGameBranch==false` → 无法逃到好仓」。  
3. **ACF HardFault / 未结算**：硬拦 **清 settle + 写快照**；与根检查正交；**不单独**驱动 Ready→降级。  
4. **快照失败默认不清 settle**：取消 deferred clear 升 Ready；I/O 等异常靠重试文案 + teardown 逃生，不靠假 Ready。  
5. **向导前置**：`Archive*` / 用户确认「继续」前再检 `LooksLikeGameRoot`；不过则停在下载步，不把空壳封成成功 Linked/Ready 路径。  
6. **语义**：本文「假 Ready」指 **`BranchWizardStep.Ready`**，不是 `GameStatusKind.Ready`（后者已有 GameMissing 护栏）。

---

## 4. 组件设计

### 4.1 `BranchStoreHealth`（新）

纯函数优先，便于单测。建议落在 `Services/BranchStoreHealth.cs`。

**输入：** 仓路径（可空）、ACF 文本（可空）。  
**输出（示例形状，实现可微调命名）：**

```text
IsGameRoot          // SteamGameLocator.LooksLikeGameRoot(path)
AcfHardFault        // StateFlags 含 FilesCorrupt | FilesMissing | Uninstalling
AcfAllowsSnapshot   // 现有 LooksSettledForSnapshot（其内已含 LooksUpdateUnhealthy 等）
CanPromoteReady     // IsGameRoot && AcfAllowsSnapshot
ReasonCodes         // 如 store_not_game_root / acf_hard_fault / acf_not_settled
```

说明：`AcfHardFault` 用于文案与诊断对齐；`CanPromoteReady` / 清 settle 以 `AcfAllowsSnapshot && IsGameRoot` 为准（`LooksSettledForSnapshot` 已拒绝未结算与部分坏 flags；若现有 settled 判定未覆盖全部 Corrupt/Missing 组合，则在 `SteamBetaKeyEditor` 补强，使 HardFault ⇒ `!LooksSettledForSnapshot`）。

### 4.2 `SteamBetaKeyEditor`

- 新增或内联：`LooksAcfHardFault(acfText)`（bits：32 Missing、128 Corrupt、1024 Uninstalling）。  
- 保证 HardFault ⇒ 不可 `LooksSettledForSnapshot`（避免「Flags 坏但仍 settled」裂缝）。  
- 不改变正常 `StateFlags=4` 已结算清单的通过路径。

### 4.3 `BranchSwitchService`

- `TrySnapshotSettledAcf`：继续要求 store 为游戏根 + settled；失败原因可区分（供 VM 决定是否清门——答案恒为否，除非未来显式逃生 API）。  
- `ArchiveCurrentAs` / `ArchiveDownloadedAs`：源/结果非游戏根则失败，不写成功步骤。  
- `TrySwapJunction`：保持「目标必须是游戏根；允许离开空壳当前仓」。

### 4.4 `MainViewModel` 接线

| 时机 | 行为 |
|------|------|
| Refresh / 加载分支配置 | 若 `Enabled && Ready && !激活仓或link游戏根` → 降级：`EnterSteamSettle`（或等价待修步）、notify、日志；不删目录 |
| `DeployBoundProfileAndClearSettle` | 根不完整或 `!AcfAllowsSnapshot` 或 `TrySnapshotSettledAcf` 失败 → **不**调用 `ClearSteamSettle`；去掉 snapshot deferred 成功清门 |
| 切服到目标 | 目标非根 → 禁；当前空壳可离开 |
| 应用并启动 | 空壳 / session 待修时禁（与 `CanDeployOrLaunch` 对齐并补分支文案） |
| 向导继续 / Archive | 非根则停并提示去 Steam |
| teardown / 诊断 / 切完整另一仓 | 降级后仍允许 |

---

## 5. 门禁表

| 操作 | 激活仓非根 | ACF HardFault / 未结算 | 根完整 + ACF 可快照 |
|------|------------|------------------------|---------------------|
| `ClearSteamSettle` / 升 `WizardStep.Ready` | 禁 | 禁 | 允许且快照须成功 |
| 快照失败仍清 settle | — | — | **禁** |
| 切到目标仓 | 目标非根禁；当前空壳可离开 | 不单独禁切服（切后仍进 settle） | 允许 |
| 应用并启动 | 禁 | 不强制叠加 | 现有 Melon Ready 规则 |
| 向导 Archive / 继续 | 非根则停 | 提示 Steam | 继续 |
| Ready 空壳自动降级 | **触发** | 不单独触发 | 不触发 |
| teardown / 诊断 / 切完整另一仓 | **允许** | 允许 | 允许 |

---

## 6. 文案与诊断

- Notify/日志：激活仓不完整 → 请在 Steam 验证或完成下载至可「开始游戏」；可尝试切到另一完整仓。  
- 结算被拦：复用/扩展现有 `NotifySettleBlocked*`（update unhealthy / not settled / game not ready）；HardFault 可走同一「先验证」语义。  
- 诊断：继续输出 `active_store_incomplete` / `acf_not_settled` / `acf_update_unhealthy`；若便于对齐可增加 `acf_hard_fault`（可选，非必须发版条件）。

---

## 7. 测试要求（完成定义）

至少覆盖：

1. `WizardStep=Ready` + 激活仓删 `GameAssembly` → Refresh 后不再 Ready，进入 settle/待修；目录仍在。  
2. ACF `StateFlags` 含 Corrupt/Missing（如 1190）且未结算 → 点结算继续 **保持** settle，不升 Ready。  
3. Deploy 成功但快照失败（未结算或 HardFault）→ **不** `ClearSteamSettle`。  
4. 当前仓空壳、另一仓完整 → 仍可切换到完整仓（逃生）。  
5. 激活仓仍为游戏根、ACF 仅短暂更新态但 `LooksSettledForSnapshot==false` → **不**因 FilesMissing  alone 从 Ready 降级；若本就在 settle 则不清门。  
6. 回归：既有 `ConfirmManualBeta_keeps_settle_when_acf_not_settled` / 缺 exe 保持 settle 仍绿。

---

## 8. 发版与和 C1 的关系

- 本规格独立合入/发版（版本号由发布流程定，不绑定 Phase1）。  
- 不阻塞 C1 Phase1；Phase1 可将 `BranchStoreHealth` 结果纳入 Facts，避免第三套并行布尔。  
- 行为变更属产品 bugfix，符合 C1「重构 PR 不夹带行为变更」的边界（本热修是显式行为修复）。

---

## 9. 风险摘要

| 风险 | 缓解 |
|------|------|
| settle 粘住无法切好仓 | 门禁表强制保留切完整仓 / teardown |
| 正常更新误降级 | 降级只看 `LooksLikeGameRoot` |
| 快照 I/O 失败永久 settle | 文案重试 + teardown；不升假 Ready |
| 向导仍封空壳 | Archive/继续前置根检查 |
| 与 Melon Ready 混淆 | 规格明确双语义；启动门两边都要过 |
