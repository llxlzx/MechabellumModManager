# v1.1.5 — 切服不再假「未注入」 / No false not-injected after branch switch

相对 GitHub 上的 **v1.1.4**。若已装本地 `v1.1.4-diagnosis` 预览，请用本 Setup 覆盖。  
Since GitHub **v1.1.4**. If you already installed a local `v1.1.4-diagnosis` preview, overwrite with this Setup.

build=`v1.1.5-inject-fix`

## 中文

### 本版修复

- **切服后不再假报「Loader 未在本次启动注入」。** 上次启动时间是全机一份；切到另一仓后会清掉再刷新，未注入只对「当前仓、本次启动」生效。
- **诊断包能看见未注入。** Detect 失败时写 `loader_not_injected` 事件；时间线也能匹配中文「Loader 未在本次启动注入」。
- **不再被 `upgrade_skipped` 刷屏。** Melon 已是当前版本（`already_current`）不再记事件；该升级却没有本地 zip 仍会记。

### 相对 v1.1.4 的累计（含未上架的 diagnosis 重建）

- 状态栏与诊断包给出唯一主因（含已排除项与证据）；导出 zip 含 `diagnosis.json`，`summary.md` 以主因为标题。
- 无梯子也能查更新、拉工坊：先国内镜像，失败再 GitHub；目录成功会缓存。
- 关窗放弃结算不再假 Ready；向导改 BetaKey 失败会中止。
- 启用双服收尾恢复正式服 Steam 清单快照，避免 Steam 经联接写穿 `_official`。

### 使用注意

- 若 Steam 正在经联接写入某一仓：**不要急救、不要切仓、不要点开始游戏**。等可开始游戏后再在管理器点结算继续。

## English

### This release

- **Branch switch no longer reports a false “Loader not injected this launch”.** Last-launch time is global; switching stores clears it and refreshes. Not-injected only applies to the current store after a launch from this manager.
- **Diagnostics record a missed injection.** Detect writes `loader_not_injected`; the timeline also matches the Chinese needle.
- **`upgrade_skipped` is quiet when Melon is already current.** `already_current` is not written; a needed upgrade skipped for `no_local_zip` still is.

### Since v1.1.4 (includes the unreleased diagnosis rebuild)

- Status bar and diagnostics zip name one primary cause. The zip includes `diagnosis.json`; `summary.md` is titled by the primary cause.
- First-party mirror then GitHub; catalog cache.
- Closing the window during settle no longer fakes Ready; wizard aborts if silent BetaKey write fails.
- Enable-dual restores the Official ACF snapshot so Steam does not rewrite `_official`.

### Notes

- If Steam is writing through the junction: **do not use Emergency, do not switch stores, do not click Play**.
