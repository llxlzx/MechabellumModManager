# 诊断包导出 + 管理器日志落盘设计

> **状态**：待用户审阅 spec  
> **日期**：2026-09-05  
> **仓库**：Mechabellum Mod Manager  
> **用户确认**：方案 1（DiagnosticsExportService + 轻量对话框）；落盘模式 C；导出时玩家自选原样/强脱敏；另存为 → 打开文件夹 → mailto

---

## 1. 背景与目标

当前「运行日志」仅存在于内存 `LogText`；`Paths.LogsDir` 已预留但几乎不用；仅崩溃时写入固定 `%AppData%\MechabellumModManager\crash.log`。远程测试员难以把双服 / Access Denied / Melon 未加载等现场完整发给维护者。

**目标**

- 管理器日志持续落盘（按天）+ 导出时附带当前会话内存日志
- 一键导出诊断 zip（配置/双服/部署清单/环境/Melon Latest.log/crash 等）
- 导出前知情同意，并由玩家选择 **原样收录** 或 **强脱敏**
- 另存为 → 打开所在文件夹 → 打开邮件草稿（`llxmod@foxmail.com`；系统无法自动附加 zip，正文说明请手动附加）

**非目标**

- 不上传服务器 / 不自动发邮件附件
- 不打包 Mod DLL、`library/`、预览图、安装 redist
- 不改双服/部署业务语义（导出只读）
- 不做强脱敏的完美匿名（以路径/用户名替换为主）

---

## 2. 架构

| 单元 | 职责 | 不负责 |
|------|------|--------|
| `ManagerLogWriter` | 追加写入 `{DataRoot}/logs/manager-yyyy-MM-dd.log`；启动清理过期；进程内锁 + `FileShare.ReadWrite` | UI、组 zip |
| `DiagnosticsExportService` | 只读收集文件 → 可选脱敏 → 写 zip | 对话框、mailto、SaveFileDialog |
| `ExportDiagnosticsDialog` | 知情说明 + 原样/强脱敏 + 确认/取消 | 组包逻辑 |
| `MainViewModel.ExportDiagnosticsCommand` | 编排：对话框 → 导出 → 保存 → 打开文件夹 → mailto → AppendLog | 文件格式细节 |

`AppendLog`：先（或并行）写 `ManagerLogWriter`，再更新 `LogText` / `LatestLogLine`。写盘失败吞掉，可选在内存记一条「日志落盘失败」（避免递归炸盘：落盘失败本身不再强制写盘）。

**路径规则（冲突规避）**

- 日志目录 = `Paths.LogsDir`（跟随 **DataRoot**，含便携模式），禁止写死 AppData。
- `crash.log`：现实现写在固定 `%AppData%\MechabellumModManager\crash.log`；导出时 **两处都查找**（`DataRoot/crash.log` 与固定 AppData），谁存在拷谁（可加后缀区分）。

---

## 3. Zip 包内容

默认文件名：`MechabellumModManager-diagnostics-yyyyMMdd-HHmmss.zip`

| Zip 内路径 | 来源 |
|------------|------|
| `README.txt` | 说明、脱敏模式、维护者邮箱、请手动附加 |
| `environment.json` | 管理器版本、OS、.NET、时区、GamePath、GameStatus 摘要、BranchSwitch Enabled/WizardStep/ActiveBranch、redaction 模式、`missing[]` |
| `config.json` | `Paths.ConfigPath` 副本 |
| `branch-switch.json` | 若存在 |
| `branch-switch-journal.json` | 若存在 |
| `deploy-manifest.json` 等 | 单服 + official/beta 及 `.prev`（若存在） |
| `logs/manager-*.log` | 最近最多 **14 天** 或最多 **7 个** 文件（取较严） |
| `logs/session.log` | 导出瞬间完整 `LogText` |
| `logs/crash.log` / `logs/crash.appdata.log` | 见上 |
| `melon/Latest.log` | `{GamePath}\MelonLoader\Latest.log`（当前活动游戏路径） |
| `melon/note.txt` | 缺失时说明未找到 |

缺失文件不导致整包失败；记入 `environment.json.missing` 与 README。

---

## 4. 脱敏

导出对话框二选一：

- **原样**：文本按源文件原文进入 zip。
- **强脱敏**：对进入 zip 的**文本内容**做替换（不回写本机文件）：
  - `C:\Users\<Name>\` / `C:\Users\<Name>/` → `C:\Users\<User>\`
  - 常见 AppData 绝对前缀 → `<AppData>\MechabellumModManager\...`
  - `environment.json` 同步替换，并设 `"redaction": "strong"`；原样时 `"redaction": "none"`

---

## 5. UI 与流程

1. 入口：设置页 + 日志面板旁，「导出诊断包」（同一 Command）
2. `ExportDiagnosticsDialog`：知情说明（含路径可能暴露）+ 原样/强脱敏 + 取消/继续
3. 组包（防重复点击；失败 `_notify`，不打开邮件）
4. `SaveFileDialog`；取消则结束
5. 打开 zip 所在文件夹（现有 `_openFolder` / `Process.Start` 模式）
6. `mailto:llxmod@foxmail.com`：主题含版本；正文含脱敏模式 + 请手动附加 zip
7. `AppendLog`：「已导出诊断包：{path}」

邮件客户端缺失：仍打开文件夹，并提示手动发邮件。

本地化：新增字符串写入 `Strings*.resx`（与现有一致）。

---

## 6. 错误处理与隔离

- 落盘 / 导出 **不得**改变 BranchSwitch、Deploy、Melon 安装状态
- 单文件读取失败 → 记 missing，继续
- 整包失败 → 用户可见原因；临时目录尽量清理
- 与现有 `crash.log` / 举报 mailto 流程并存，不替换举报

---

## 7. 测试

- `ManagerLogWriter`：写入、并发读、过期清理
- `DiagnosticsExportService`：缺文件仍成功；原样保留用户路径片段；强脱敏替换 `Users\<Name>`；`environment.json` 含 redaction
- VM 层：可用 fake save/open/mail 回调做编排测试（可选）

---

## 8. 自检记录（实现前）

| 项 | 处理 |
|----|------|
| 便携 DataRoot | 日志跟 `Paths.LogsDir` |
| crash 路径不一致 | 导出双路径查找 |
| 读写冲突 | 锁 + `FileShare.ReadWrite` |
| mailto 不能附文件 | README + 邮件正文明确手动附加 |
| 不打包 library/DLL | 已列入非目标 |
| 与反馈包旧 spec | 本 spec 独立实现诊断导出；不依赖未完成的旧 Phase 文档 |

无未决 TBD；范围可单计划落地。

---

## 9. 建议落地版本

随下一版小版本（如 **1.1.2**）与双服搬目录加固一并发布（若该加固仍待提交）。
