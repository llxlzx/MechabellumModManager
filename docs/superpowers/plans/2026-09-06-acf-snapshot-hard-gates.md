# ACF 快照硬门禁 — 实现计划

> **For agentic workers:** TDD；每任务可独立验证。

**Goal:** 错快照不存/不恢复；同 buildid 对冲突拒绝；空壳仓切服仍禁止。

**Tech:** `SteamAcfSnapshotSanitizer`、`BranchSwitchService.TrySnapshotSettledAcf` / `TryRestoreAcfSnapshot`、文案。

## Task 1: Sanitizer — buildid 冲突

- 加 `ReadBuildId`、`HasBuildIdCollision(officialText, betaText)`
- `SanitizeDirectory`：两文件都在且 buildid 相同 → 删除两者
- 测试：同 buildid 双快照被删；不同 buildid 保留

## Task 2: Snapshot save/restore gates

- `TrySnapshotSettledAcf`：`IsAlignedWith` + `IsValidSnapshot` + 与 sibling 无 buildid 冲突
- `TryRestoreAcfSnapshot`：同上校验 sibling
- `BranchOpMessages` 新常量
- 扩展 `BranchSwitchServiceTests`

## Task 3: 切服文案

- 更新 `NotifySwitchBlockedTargetHollow`（中/英）

## Task 4: 跑相关测试 + 提交
