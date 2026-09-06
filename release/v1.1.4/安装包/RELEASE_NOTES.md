# v1.1.4 — 启用双服不再写穿正式仓 / Stop Steam rewriting Official after enable-dual

相对 GitHub 上的 **v1.1.2**（本地 1.1.3 预览包的用户请直接装本版）。  
Since GitHub **v1.1.2** (if you already installed a local 1.1.3 preview, install this Setup).

## 中文

### 紧急修复

- **启用双服后 Steam 不再把完整正式仓当缺文件重下。** 向导归档正式服前先保存 Steam 清单快照；收尾与「切服」一样恢复该快照，避免 Steam 经 `Mechabellum` 联接写穿 `_official`。
- **结算失败不再怂恿急救 / 切仓。** Steam 正在往当前仓写文件时，急救或切仓会伤盘。请等 Steam 显示可「开始游戏」后再点继续；请勿在 Steam 里点开始游戏。
- **双仓结构完成后不再把两仓标成「本会话垃圾」。** 避免之后回滚误删正式仓和测试仓。

### 相对 v1.1.2 的累计（含未上架的 1.1.3）

- 三入口双服：**启用双服** / **切换正式·测试** / **急救恢复单目录**。取消或中途关窗会回滚，不会从半截向导接着做。
- 启用等待下载时不再误报「未找到游戏」；测试服下完后须完全退出 Steam，向导会自动归档。
- 单实例：第二次打开会提示已在运行后退出。
- 彻底清理（纯净游戏）：卸载与管理器内共用白名单；不卸系统 .NET、不删游戏本体。
- ACF 快照硬门禁：未对齐 / 同 buildid 的假快照不存不恢复。
- 诊断包、关键操作恢复、Busy 进度条等稳定性修复。

### 使用注意

- 若 Steam 正在经联接写入某一仓：**不要急救、不要切仓、不要点开始游戏**。等可开始游戏后再在管理器点结算继续。
- 已装本地 1.1.3 预览的用户：请用本 Setup 覆盖安装。

## English

### Critical fix

- **Enable-dual no longer makes Steam rewrite a complete Official store.** The wizard snapshots the Official Steam manifest before Archive A, and restores it on finish the same way branch-switch does, so Steam does not treat `_official` as missing files and write through the `Mechabellum` junction.
- **Settle failure copy no longer pushes Emergency / switch.** Those actions can damage folders while Steam is writing. Wait until Steam can Start the game, then continue in the manager. Do not click Play in Steam.
- **Session-owned flags are cleared once both stores are linked**, so later rollback cannot delete the Official and Beta stores as “session trash”.

### Since v1.1.2 (includes unreleased 1.1.3 work)

- Three-entry dual-folder: **Enable dual** / **Switch Official·Beta** / **Emergency: single folder**. Cancel or close mid-enable rolls back; there is no resume from a half-finished wizard.
- Waiting for the other-branch download no longer banners “game not found”. After the download finishes, fully exit Steam; the wizard archives automatically.
- Single-instance: a second launch shows a prompt and exits.
- Pure-game cleanup (whitelist) on uninstall and in-app; does not uninstall system .NET or delete the game itself.
- ACF snapshot hard-gates: refuse unaligned / same-buildid fake snapshots.
- Diagnostics export, critical-op recovery, busy progress, and related stability fixes.

### Notes

- If Steam is writing through the junction: **do not use Emergency, do not switch stores, do not click Play**. Wait until Start is available, then confirm settle in the manager.
- Users who installed a local 1.1.3 preview should overwrite with this Setup.
