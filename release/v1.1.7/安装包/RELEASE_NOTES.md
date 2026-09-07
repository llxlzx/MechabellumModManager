# v1.1.7 — 安全与稳定性大修 / Safety and stability audit

相对 GitHub 上的 **v1.1.6**。请用本 Setup 覆盖。  
Since GitHub **v1.1.6**. Overwrite with this Setup.

build=`v1.1.7-safety-audit`

## 中文

### 本版（一次全面代码审查后的修复）

- **修复：启动自动回滚可能删掉你原来的游戏目录。** 双目录向导中途崩溃后，管理器重启时的自动回滚不再把「你原本那份完整安装」当成本次新下载的内容删除。现在回滚一律优先把原始安装搬回 Steam 目录；万一搬不回去，也会原地保留并在日志里写明位置，绝不删除。
- **修复：归档阶段现在有操作日志。** 搬动你唯一那份安装之前先落盘记录，中途断电重开后能判断搬完了、没开始搬、还是只解了联接，并据此恢复，不再对半搬的目录做二次搬动。
- **加固：下载 Mod 会校验 sha256。** 目录里写了校验值的条目，下载后哈希不符就删除临时文件并拒绝导入。镜像源下载若没有校验值一律拒绝（GitHub 直连不受影响）。
- **加固：只允许打开 https 链接。** 更新清单里的地址如果是 `file:`、UNC 路径或自定义协议，一律拦截并记日志，避免点一次确认就运行本地程序。镜像地址也必须是 https，否则忽略该设置。
- **加固：Mod 部署路径必须落在游戏目录内。** 清单被改坏或被篡改时，越界的路径会直接报错，而不是照着删除游戏目录外的文件。
- **修复：诊断包里的 json 现在也会脱敏。** 之前 `config.json`、`events.jsonl` 里转义过的路径没被打码，用户名会泄漏；同时超大日志只保留末尾 8 MB，避免导出时吃满内存。
- **修复：注入判定不再把「生成程序集」当成一次注入。** 只点了应用而没启动游戏时，不会再误报已注入。
- **修复：配置写入改为原子替换，损坏时自动备份。** 断电不会再留下半截 `config.json`；真的坏了会另存 `.corrupt-<时间>.bak` 再回落默认值，不会悄悄丢掉游戏路径。
- **补齐日/德/俄三种语言缺失的 33 条文案。** 诊断面板和彻底清理界面不再在非中文界面下露出中文。

### 使用注意

- 若 Steam 正在经联接写入某一目录：**不要点还原官方目录、不要切服、不要点开始游戏**。等可开始游戏后再在管理器点结算继续。
- 如果你自建了下载镜像，请确保 `catalog.json` 中每个条目都写了 `sha256`，否则经镜像的下载会被拒绝。

## English

### This release (fixes from a full code audit)

- **Fix: startup auto-rollback could delete your original game folder.** After an interrupted dual-folder wizard, rollback no longer treats your pre-existing install as this session's download. It now always tries to move the original back to the Steam folder, and when that is impossible it keeps the folder in place and logs where it is instead of deleting it.
- **Fix: the archive step is journalled.** Moving your only install is recorded before touching the disk, so a crash mid-move can be resolved correctly instead of moving a half-copied folder again.
- **Hardening: mod downloads verify sha256.** Entries that declare a hash are dropped when the download does not match. A mirror download without a declared hash is refused outright; direct GitHub downloads are unaffected.
- **Hardening: only https links may be opened.** `file:`, UNC paths, and custom protocol handlers coming from an update manifest are blocked and logged. Mirror addresses must be https or the setting is ignored.
- **Hardening: deploy paths must stay inside the game folder.** A corrupted or tampered manifest now fails loudly instead of deleting files outside the game root.
- **Fix: diagnostics json is redacted too.** Escaped paths in `config.json` and `events.jsonl` leaked the user name. Oversized logs now keep only the last 8 MB.
- **Fix: assembly generation no longer counts as an injection.** Applying without launching no longer reports the loader as injected.
- **Fix: config writes are atomic and corrupt files are backed up** to `.corrupt-<timestamp>.bak` before falling back to defaults.
- **Adds 33 missing strings to Japanese, German, and Russian.** The diagnosis panel and cleanup UI no longer fall back to Chinese.

### Notes

- If Steam is writing through the junction: **do not click Restore Official folder, do not switch stores, do not click Play**.
- If you host your own mirror, give every `catalog.json` entry a `sha256`, otherwise mirror downloads are refused.
