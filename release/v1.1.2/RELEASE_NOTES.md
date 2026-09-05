## v1.1.2

### 修复（双服结算）
- **假 Ready**：确认 Steam 结算时若部署失败、游戏路径未就绪、或 ACF 清单未 settled，不再强制结束结算。
- **误报已是该服**：目录/分支键已对齐但 Detect 未 Ready 时，改为提示游戏尚未就绪，不再显示「无需再次切换」。

### 修复（双服路径 / 安装包）
- GamePath 落在 `Mechabellum_official` / `Mechabellum_beta` 时：有 `Mechabellum` 链路则自动归一；否则拒绝开向导并给出中文指引。
- 归档失败英文 `Store path already exists.` 映射为本地化「另一服独立目录已存在」。
- 解除双服且保留另一仓时，提示残留目录会阻挡再次向导。
- **安装包**：`Detect-GamePath` 优先 `Mechabellum`；`Restore-DualFolderConfig` 在双服关闭且 Steam 空闲时可把仓目录改回 `Mechabellum`。

### 修复（orphan / Steam 下载掏空当前仓）
- 双服已关闭但仍有 junction / 残留仓时，提供 **「恢复为单目录」**。
- Steam 经 junction 下载导致当前仓缺 `GameAssembly.dll` 时：允许切到仍完整的另一服；解除双服时优先从完整仓还原；诊断增加 `active_store_incomplete` / `orphan_dual_layout`；中文说明下载中勿强切。

### 体验（启动）
- 启动成功后追加 Melon 黑色控制台验证提示。

### 诊断
- environment.json 含 probe/findings（含 `game_path_is_branch_store`、`leftover_dual_store_folders`、`orphan_dual_layout`、`active_store_incomplete` 等）。

### MelonLoader
- 捆绑 MelonLoader **0.7.3**；若本机仍为 **0.7.1**（或更旧），可通过 **Setup 勾选安装 MelonLoader**，或在管理器内 **安装 / 升级 MelonLoader**，升级到 0.7.3。

### 升级说明
- 覆盖安装 Setup（可勾选 MelonLoader 以从 0.7.1 升到 0.7.3）。若当前仓被 Steam 掏空：先完全退出 Steam，再切正式服或解除双服/恢复单目录。
### 安装包（v1.1.2 续）
- **脏 ACF 快照清洗**：安装后删除无效的 AppData\MechabellumModManager\steam-acf-snapshots 下 official.acf / beta.acf（例如正式服快照仍带 public_test），避免一键切服假成功；不修改 Steam 本体清单。
- **Melon 版本门控**：Setup 仅在 MelonLoader 大于等于 0.7.3 时跳过；组件说明改为「低于 0.7.3 会升级」。
### 会话锁与任务进度
- 关键任务未完成时锁定冲突写操作（部署/切服/Melon/改路径等）；目录刷新与检查更新仅防重入。
- 顶栏任务进度条：向导/结算/Busy/Melon 百分比/部署/目录/更新/诊断全覆盖。

### 关键操作保护
- **写盘期间（切服/Melon/孤儿修复等）**：检查更新与关闭窗口硬拦截并提示，避免安装包覆盖或半写状态。
- **等待 Steam 结算/下载时**：仍可检查更新或关闭，但需二次确认并说明双服/结算中断风险。
- **异常退出后**：若 AppData 残留 `critical-op.json`，启动时强制选择「继续未完成工作」或「修复/清理」；完成前锁定写操作。
- **安装包**：Setup 检测到管理器仍在运行时，提示先完成或取消模式切换/Melon 任务再关闭后安装。
