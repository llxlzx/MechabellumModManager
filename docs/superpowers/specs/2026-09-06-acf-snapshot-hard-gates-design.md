# ACF 快照硬门禁（防假免下载）— 设计

**日期：** 2026-09-06  
**状态：** 已确认（用户选方案 A）

## 问题

1. 正式服快照可被存成「无 BetaKey，但 buildid 仍是测服」→ 切正式服后 Steam 改 TargetBuild → 校验/下载。  
2. 目标仓不完整时若仍恢复错快照，会加剧空洞。

## 规则

### 存快照 `TrySnapshotSettledAcf`

全部满足才写入：

- 目标仓 `LooksLikeGameRoot`
- `IsAlignedWith(branch)`（联接指向该仓 + BetaKey 语义匹配）
- ACF `LooksSettledForSnapshot`
- `SteamAcfSnapshotSanitizer.IsValidSnapshot`
- 若另一服快照文件存在且两边 `buildid` 相同 → **拒绝保存**（`SnapshotBuildIdCollision`）

### 恢复 `TryRestoreAcfSnapshot`

恢复前：

- 快照 settled + `IsValidSnapshot`
- 若另一服快照存在且 buildid 相同 → **拒绝恢复**
- 失败时 `TryPrepareSteamBranchMetadata` 回退 `TrySilentSetBeta`（不假装免下载）

### Sanitize

删除 BetaKey 语义非法快照；若 official/beta **同时存在且 buildid 相同**，删除二者（或至少删 official，实现选删二者以防毒）。

### 切服 UI

目标仓不完整仍硬拦；文案强调先 Steam 下完该服。

## 非目标

不改 Phase0；不保证「从未下过的正式服」零下载。
