# v1.1.5 — 切服、结算、写信与工坊标签 / Branch switch, settle, mail, and tags

相对 GitHub 上的 **v1.1.4**。若已装更早的 `v1.1.5`，请用本 Setup 覆盖。  
Since GitHub **v1.1.4**. If you already installed an earlier `v1.1.5`, overwrite with this Setup.

build=`v1.1.5-settle-exe`

## 中文

### 本版

- **切服后橙字说清要去点金色继续。** Ready 时不再写成「向导未完成」；下载另一服时仍是向导未完成。
- **空方案会提醒。** 没有启用任何 Mod 仍可部署/启动，但会说明游戏里不会出现 Mod。
- **刚生成过程序集的那次「应用并启动」改为直启 exe。** 不改设置里的启动方式；Steam 优先开的第二扇窗不再顶掉 Melon。
- **未注入结束会提示改「仅直启 exe」。** 不自动再开一扇游戏。Steam 打开另一套库仍会提示。
- **「应用并启动」未注入不再拿补齐 DLL 那次 Melon 日志充数。** 生成程序集时的黑窗会写出 Latest.log 然后被关掉；Steam 再开的窗口若没有黑窗、没有 Mod，仍报未注入。
- **Steam 打开的不是管理器游戏目录时会说明。** 提示改「仅直启 exe」，或把路径改成 Steam 实际打开的库。
- **启用双服撞上已存在仓目录。** 提示先急救恢复单目录，或在纯净复原时勾选删除另一仓（不可恢复）。
- **修复上一包安装后无法打开。** 筛选下拉不能同时设置 DisplayMemberPath 与 ItemTemplate，会在启动时崩。
- **工坊与已装 Mod 能看见标签。** 筛选行有「分类 / 标签 / 排序」文字；列表在分类后有「标签」列。
- **未完成关键操作四钮不再重叠。** 「放弃并清理」等全文可见；诊断仍不关窗、不解除限制。
- **切服后不再假报「Loader 未在本次启动注入」。** 上次启动时间是全机一份；切到另一仓后会清掉再刷新，未注入只对「当前仓、本次启动」生效。
- **诊断包能看见未注入。** Detect 失败时写 `loader_not_injected`；时间线也能匹配中文「Loader 未在本次启动注入」。
- **不再被 `upgrade_skipped` 刷屏。** Melon 已是当前版本（`already_current`）不再记事件。
- **结算钮不再假装已就绪。** 游戏路径缺少 exe/dll 时金色「继续」禁用；等到可开始游戏即可，结算不必先退出 Steam。联接读不到仓内文件时诊断包记 `junction_desync`。
- **写信三选。** 导出诊断、提交 Mod、报告问题、建议：先选 QQ 邮箱 / Gmail / 取消；取消只复制不打开网页。
- **向导盘齐后 Steam 仍开着。** 金色钮改为「我已退出 Steam，继续」；Steam 或游戏仍在运行时不会 ArchiveB。
- **开始游戏后先确认进程再验注入。** 约 15 秒内未见进程会提示，不会因此标成游戏丢失。
- **结算中或 Steam 写盘时禁用切服。** 状态栏文案缩短。工坊失败、急救与纯清理确认也已收短；纯清理默认不删另一仓。

### 相对 v1.1.4 的累计（含未上架的 diagnosis 重建）

- 状态栏与诊断包给出唯一主因（含已排除项与证据）；导出 zip 含 `diagnosis.json`，`summary.md` 以主因为标题。
- 无梯子也能查更新、拉工坊：先国内镜像，失败再 GitHub；目录成功会缓存。
- 关窗放弃结算不再假 Ready；向导改 BetaKey 失败会中止。
- 启用双服收尾恢复正式服 Steam 清单快照，避免 Steam 经联接写穿 `_official`。

### 使用注意

- 若 Steam 正在经联接写入某一仓：**不要急救、不要切仓、不要点开始游戏**。等可开始游戏后再在管理器点结算继续。

## English

### This release

- **After a branch switch, the orange line says to click gold Continue on Settings** — not “wizard unfinished” while Ready. Mid-wizard download still says the wizard is unfinished.
- **Empty profile warns** that no mods are enabled; deploy/launch still proceed.
- **The Apply and Launch that just generated assemblies starts the exe this time** (SteamThenExe setting unchanged) so Melon loads in the manager folder.
- **If still not injected after recheck, asks to switch to Exe only.** Does not start a second window. Still warns if Steam opened another library.
- **“Not injected” no longer credits the DLL-generation Melon log.** That session writes Latest.log and is killed; a later Steam window without the Melon console stays not-injected.
- **Warns if Steam opened a different game folder** than the manager path. Use Exe only, or point the manager at the library Steam actually opened.
- **Enable-dual hitting an existing store folder** tells you to use Emergency, or check “also delete the other store” on a pure restore (cannot undo).
- **Fixes a launch crash in the previous 1.1.5 package.** ComboBoxes cannot set DisplayMemberPath and ItemTemplate together.
- **Workshop and library show Tags.** Filter row labels Category / Tag / Sort; the grid has a Tags column after Category.
- **Critical-op recovery buttons no longer overlap.** Full labels are readable; diagnostics still does not close the dialog or lift the gate.
- **Branch switch no longer reports a false “Loader not injected this launch”.** Last-launch time is global; switching stores clears it and refreshes.
- **Diagnostics record a missed injection.** Detect writes `loader_not_injected`.
- **`upgrade_skipped` is quiet when Melon is already current.**
- **Settle Continue stays disabled until the path is Ready.** Quitting Steam is not required. A junction that cannot see store files is `junction_desync`.
- **Compose mail asks QQ / Gmail / Cancel.** Cancel copies only and does not open a browser.
- **Wizard gold button becomes “I quit Steam, continue” when the disk is ready but Steam is still running.** ArchiveB does not run while Steam or the game is running.
- **Launch verifies the game process (about 15s) before the injection check.** Process not seen is a notice, not Game Missing.
- **Switch is disabled during settle (when Ready) or while Steam is writing.** Workshop-fail, emergency, and cleanup confirms are shorter; cleanup does not delete the other store by default.

### Since v1.1.4 (includes the unreleased diagnosis rebuild)

- Status bar and diagnostics zip name one primary cause.
- First-party mirror then GitHub; catalog cache.
- Closing the window during settle no longer fakes Ready; wizard aborts if silent BetaKey write fails.
- Enable-dual restores the Official ACF snapshot so Steam does not rewrite `_official`.

### Notes

- If Steam is writing through the junction: **do not use Emergency, do not switch stores, do not click Play**.
