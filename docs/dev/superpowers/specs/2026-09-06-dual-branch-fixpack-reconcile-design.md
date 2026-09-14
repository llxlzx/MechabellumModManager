# 双服修复包冲突调和 — 设计

**日期：** 2026-09-06  
**状态：** 已确认（用户：选最安全 / 最稳定 / 最有效）  
**范围：** H1 / H2 / H3 / H4 / H5 / H11 / H12 一并落地前的逻辑冲突分析与修缮规则

## 0. 已锁定选择（安全优先）

| # | 议题 | 锁定 |
|---|------|------|
| 1 | 向导快照 | **独立 live 快照 API**（不复用硬 `IsAlignedWith`） |
| 2 | 同 buildid | 向导 live：**删毒 sibling 再写**；日常 settle：**只拒存、不删** |
| 3 | 快照失败 | **不阻断 ArchiveB**（打日志 + 可选软提示；免下载尽力而为，向导可走完） |
| 4 | 白名单 / 删实目录 | **不做**官方 build 白名单；**不自动删** Steam 路径完整实目录 |
| — | settled | 位标志白/黑名单（必须 FullyInstalled；仅允许 AppRunning/Locked/Encrypted） |
| — | H4 冲突 | dest 完整且 link 已无 → 接管；link 仍为实目录 → 停 poll + 中文修复路径 |
| — | H11 | `Complete()` 弹巢 + `SyncStickyBranchTaskStrip()`，不依赖 `Kind==DiagnosticsExport` |

原则：**宁可少免下载，不可再造假 Ready / 死循环 / 误删用户游戏目录。**

## 1. 会打架的点（按严重度）

### C1 — 致命：H1「ArchiveB 前快照 other」vs 现有 `IsAlignedWith`

`TrySnapshotSettledAcf` 要求：

- `StorePath(branch)` 已是游戏根  
- `IsAlignedWith`：Steam 路径必须是 **junction 且指向该仓**

ArchiveB **之前**：

- `other` 仓目录还不存在（正要搬过去）  
- Steam 路径是 **实目录**（刚下完的 B），不是 junction  

→ 按现状调用 `TrySnapshotSettledAcf(other)` **几乎必失败**（`StoreNotGameRoot` / `SnapshotNotAligned`），H1 形同虚设。

**修缮：** 新增专用入口（或参数）`TrySnapshotLiveAcfForWizardBranch(other)`：

| 检查 | 日常 settle 快照 | 向导 ArchiveB 前 live 快照 |
|------|------------------|---------------------------|
| 游戏进程 | 拦 | 拦（Steam 已要求退出） |
| 目标仓已是游戏根 | 要 | **不要**（仓尚未创建） |
| junction 对齐 | 要 | **不要**；要求 Steam 路径是**实目录游戏根** |
| ACF BetaKey / Mounted 语义 | 要 | 要（`IsValidSnapshot`） |
| settled | 要 | 要 |
| 与 sibling 同 buildid | 拒存 | 见 C2 |

落盘路径仍是 `official.acf` / `beta.acf`，与 settle 快照同一套文件。

### C2 — 高：H1 存 B vs H5 同 buildid / 假 official

若 A 已误存「假 official」（buildid=测服），再存真 B → **collision 拒存 B** → H1 仍失败。

**修缮（live 优先）：** 向导 live 快照遇到 sibling 同 buildid 时：

1. 若 sibling `IsValidSnapshot` 对**其自称分支**不成立 → **删 sibling，写入 live**  
2. 若 sibling 语义合法但同 buildid → **仍删 sibling，写入 live**（刚下完的 live 比陈旧 AppData 更可信；同 buildid 对本身即毒对）  
3. 日常 `TrySnapshotSettledAcf`（非向导 live）保持：**拒存、不删**（避免 settle 误伤）

「官方 build 合理性」二次校验：**本包不做外部 buildid 白名单**（无权威源易误杀）。改为：

- Official：BetaKey 空 +（无 sibling 或 buildid ≠ sibling）  
- 向导 live 写入用上面的「毒对清 sibling」  

### C3 — 致命：H4「dest 已完整则接管」vs CreateLink / H12

接管若只 `SetStorePath(dest)` 而 **Steam 路径上仍留着实目录**，下一步 `CreateLinkTo` 必报 `Steam link path already exists`（H12 现场）。

**修缮 — ArchiveB 结果三分：**

| 情形 | 行为 |
|------|------|
| dest 不存在 | 正常 Move(link→dest) |
| dest 已是游戏根，**link 已不存在** | 接管成功（写 config → ArchivedB） |
| dest 已是游戏根，**link 已是指向 dest 的 junction** | 接管成功，可直接视为 Linked（或交给 CreateLink 幂等） |
| dest 已是游戏根，**link 仍是实目录游戏根** | **不自动删 link**（防丢档）；失败并返回稳定 token，UI 走 H12 中文：「目标仓已存在且 Steam 路径仍有实目录 → 设置里清除隔离 / 或退出 Steam 后删空残留再继续」 |
| dest 存在但**不是**游戏根 | 失败 token「半成品仓」；提示删除该半成品目录后重试；**不要**退回 poll 死循环（见 C4） |

### C4 — 中：H4 失败 vs WaitingDownloadB 自动重试死循环

今日：ArchiveB 失败 → `WaitingDownloadB` + 再 poll → dest 仍在 → 再失败。

**修缮：**

- 「半成品 dest / link 实目录冲突」→ **停止 poll**，留在可手动处理的步骤，文案给修复路径  
- 仅「尚未下载完 / Steam 未退」类失败才继续 poll

### C5 — 中：H2 放宽 settled vs 假 Ready / 更新中误快照

接受 `68`（FullyInstalled|AppRunning）正确；若改成「只要有 4」会放过 `UpdateRunning` 等。

**修缮 — 位标志白/黑名单：**

- **必须：** `FullyInstalled` (4)  
- **允许叠加：** `AppRunning` (64)、`Locked` (16)、`Encrypted` (8)  
- **拒绝叠加：** `UpdateRequired`(2)、`UpdateRunning`(256)、`UpdateStarted`(512)、`Validating`(8192)、`Downloading`(65536)、`Staging`、`Committing`、`AddingFiles`、`Preallocating`、以及 HardFault 位（缺/坏/卸载中）  
- 保留现有：bytes 对齐、`buildid==TargetBuildID`、`LooksUpdateUnhealthy`、`LooksAcfHardFault`  
- **删除** `Contains('6')` 字符串判断  

说明：`StateFlags==6`（UpdateRequired|FullyInstalled）将变为拒绝——比字符误伤更准；真正装好的 `4`/`68`/`20`(4|16) 可通过。

### C6 — 中：H3 导出前降级 vs H11 sticky 任务条

导出前 `RefreshStatusCore` → 可能 `EnterSteamSettle`，sticky 从 Wizard 切到 Settle；若诊断 `Begin` 嵌套写坏 Message，finally 仍可能乱。

**修缮顺序（强制）：**

1. `RefreshStatusCore`（含 hollow 降级）  
2. 用**刷新后**的 `GameStatus` / step 填 `DiagnosticsExportRequest`  
3. `Begin(DiagnosticsExport)`（允许 sticky 嵌套改 Message）  
4. `ExportToFile`  
5. `finally`：**若发生了嵌套** → `Complete()` 弹一层；再 `SyncStickyBranchTaskStrip()` 按**当前** settle/wizard 重写文案；**不要**只判断 `Kind==DiagnosticsExport` 才 Clear  

诊断包顶层与 probe：summary / `environment.gameStatusKind` **以 probe 当场 Detect 为准**（或导出前 Refresh 后二者同源），禁止再用过期 VM 缓存冒充 Ready。

### C7 — 低：H11 与现有 nest 语义

`TaskProgressSession` 已支持 sticky 嵌套；缺陷在 **ExportDiagnostics finally 未 `Complete()`**。

**修缮：** finally 统一：

```text
if (嵌套深度曾因本次 Begin 增加) Complete();
else if (Kind == DiagnosticsExport) Clear();
SyncStickyBranchTaskStrip();
NotifyTaskProgress();
```

（可用 Begin 前后 `_nestDepth` 或 `Kind` 是否仍为 sticky 外层来判断；优先 `Complete()` 对称于 Busy 路径。）

### C8 — 低：H12 文案 vs 自动修复边界

中文说明可以；**自动删 Steam 实目录**本包不做（与 C3 一致）。修复路径只指向：清除隔离 / 退出 Steam / 删半成品 `_official`/`_beta` 空壳。

---

## 2. 调和后的统一流程（向导 ArchiveB 段）

```text
WaitingDownloadB 判定就绪（disk + settled位标志 + Steam/游戏退出）
    → TrySnapshotLiveAcfForWizardBranch(other)   // C1+C2：可清毒 sibling
         失败：打日志，不阻断 ArchiveB（避免「快照失败卡死下载完成」；免下载能力降级但向导可继续）
    → ArchiveDownloadedAs(other)                 // C3 三分接管
         半成品/link 冲突：停 poll + H12 文案
    → CreateLinkTo(current)
         already exists：Map 中文 + 修复路径（H12）
    → SilentSetBeta(current) + settle
         settle 仍只快照 current（日常路径）；B 已在 live 窗口写入
```

**产品取舍：** live 快照失败 **不阻断** ArchiveB（向导可完成；切服可能仍要下载）。与「从未成功」相比，优先保证向导能走完；免下载是尽力而为。

---

## 3. 本包明确不做

- 官方 buildid 联网白名单  
- 自动删除 Steam 路径上的完整实目录  
- 因 ACF 短暂 FilesMissing 且根仍完整而 Ready 降级（维持 hollow 规格 §3）  
- 改变 Phase0（不自动 `steam://open`）

---

## 4. 建议落地顺序（减冲突）

1. H2 settled 位标志（纯函数，无流程依赖）  
2. H11 诊断 sticky Complete（纯 UI）  
3. C1 live 快照 API + H1 接线 + C2 live 清毒对  
4. H4 ArchiveB 三分接管 + C4 停 poll  
5. H12 CreateLink / Archive 失败中文映射  
6. H3 导出前 Refresh + summary 与 probe 同源  

---

## 5. 验收口径

| 项 | 通过标准 |
|----|----------|
| H2 | `4`/`68`/`20` settled=true；`6`/`516`/`1190`=false；无 `Contains('6')` |
| H11 | sticky 向导中导出诊断后，任务条恢复向导/结算文案，不再残「正在打包日志…」 |
| H1 | ArchiveB 前在合法 live 窗口写入 `beta.acf` 或 `official.acf`；毒 sibling 被替换 |
| H4 | dest 完整且 link 已无 → 不进死循环；半成品/双实目录 → 停 poll + 中文 |
| H12 | 不再只弹英文 `Steam link path already exists` |
| H3 | 新诊断包顶层 `gameStatusKind` 与 probe 一致；空壳 Ready 导出前已降级 |

---

## 6. 已确认（安全优先锁定）

见文首 §0。四问答案均为安全优先选项，可进入实现。
