# 管理器应用内下载 Setup 更新

**Date:** 2026-09-17  
**Status:** Approved / shipping as v1.2.9  
**Related:** `UpdateChecker`, `2026-09-05-critical-op-guard-design.md`, `2026-03-22-installer-dotnet-progress-and-updates.md`, `2026-09-10-mod-update-autodetect-design.md`  
**Supersedes:** 管理器更新「仅打开 `setupUrl` / 浏览器下载」路径（`ConfirmOpenDownloadLink` + `TryOpenUrl`）；Mod 目录更新检测不受影响。

## Goal

把管理器本体更新从「打开浏览器下载链接」改为：有更新时弹出轻量更新窗 → 用户确认后应用内下载 Setup → 拉起安装向导 → 管理器退出。无更新时不创建该窗。

## Problem

当前 `CheckForUpdatesAsync` 在发现新版本后用 `ConfirmOpenDownloadLink` + `TryOpenUrl(setupUrl)`，玩家必须手动下安装包再运行。聊天反馈希望：

1. 启动后有更新时走自动安装包路径（确认后下载并拉起 Setup）
2. 不留下多余窗口（拉起 Setup 后管理器退出）
3. 无更新时不要启动/显示更新窗

## Decisions (locked)

| Item | Choice |
|------|--------|
| Approach | 独立 `ManagerUpdateDialog`（方案 2） |
| Automation | 确认一次后再下载并拉起 Setup（非全自动无确认） |
| Triggers | 启动静默检查 + 手动「检查更新」共用同一入口 |
| After Start Setup | 管理器立即 `Shutdown`（只剩安装向导） |
| Skip | 关窗；本会话启动路径不再弹；手动检查仍可再走同一流程 |
| Installer UX | 仍用正常 Inno 向导（不用 `/VERYSILENT`） |
| Critical-op | 保留现有 Hard / Soft 门（**启动路径 Soft 也会先弹强确认，属有意行为**） |
| Downloadable URL | 仅当 manifest `setupUrl` 为可下载安装包时才走应用内下载；Release 浏览页不得当 exe 下载 |
| Dialog modality | `ShowDialog`，`Owner = MainWindow` |
| Close during download | Soft 确认拦截主窗关闭（与更新门同级文案）；确认后取消下载再关 |

## Non-goals

- 进程内热替换正在运行的 EXE
- 独立 `Updater.exe` 旁路进程
- Setup 静默安装参数
- Manifest `sha256` 校验（本期无字段则不做）
- 「永久忽略此版本」配置项
- 改动 Mod 目录启动检查（`CheckModUpdatesOnStartup` 语义不变）
- PE/魔数深度校验（仅路径/扩展名与 https 策略）

## Architecture

```text
OfferCriticalOpRecovery 返回之后（或无需 recovery）
        │
启动路径 ──┼── 点「检查更新」
        │
        ▼
  CriticalOp Hard → 拒绝（启动：静默 return；手动：现有 notify）
  CriticalOp Soft → 强确认（启动与手动均适用；拒则中止）
        │
        ▼
  UpdateChecker.CheckAsync()
        │
        ├── UpToDate / Failed → 见 Error handling（不建更新窗）
        │
        └── UpdateAvailable
              ├── 可下载 SetupUrl（https + path 以 .exe 结尾，忽略 query）
              │     → ManagerUpdateDialog（modal）
              │           跳过 → 关窗 + 本会话启动跳过标记
              │           更新 → ManagerSetupDownloader
              │                 → IProcessStarter.Start(本地路径)
              │                 → Application.Shutdown()
              └── 仅有浏览页（旧 fallback / 非 .exe）
                    → 对话框仍开，主按钮改为「打开发布页」
                      （TryOpenUrl，不下载、不 Shutdown）
```

### Downloadable vs browse URL（修自检 #1）

现状：`UpdateChecker` 在 `setupUrl` 未过 `IsHttpsUrl` 时，仍把 `SetupUrl` 填成  
`https://github.com/.../releases/latest`（**网页**），并返回 `UpdateAvailable`。旧 UI 开浏览器正确；新 UI **禁止**把该 URL 当安装包下载。

本期约定：

1. **编排层**（非仅靠「是 https」）判定可下载：  
   `ExternalUrlPolicy.IsHttpsUrl(url)` **且** `Uri.AbsolutePath` 以 `.exe` 结尾（OrdinalIgnoreCase；query/fragment 不影响）。
2. `UpdateChecker` **建议同期收紧**（测试跟着改）：  
   - 合法 https `.exe` → `SetupUrl = manifest.setupUrl`  
   - 否则 → `SetupUrl = null`，另设 `BrowseUrl = https://github.com/.../releases/latest`（或等价字段）  
   - 有新版本但仍无下载 URL 时仍返回 `UpdateAvailable`，由 UI 走「打开发布页」分支  
3. 过渡期若暂不改 checker 字段：编排层必须用同一 `.exe` 规则，**绝不能**对 releases 页 `GET` 落盘。

### Components

| Unit | Responsibility |
|------|----------------|
| `UpdateChecker` | 发现版本；区分可下载 `SetupUrl` 与 `BrowseUrl`（见上）；不下载、不安装 |
| `ManagerSetupDownloader`（新） | 仅下载可下载 URL；进度/取消；重定向后最终 URI 仍须 https；路径消毒 |
| `ManagerUpdateDialog`（新） | Modal；版本/notes/进度；更新或打开发布页 / 跳过；下载中可取消 |
| `IProcessStarter`（或等价注入） | 启动**本地** Setup；单测断言 Start→Shutdown 顺序 |
| `MainViewModel` / `App` | 共用编排；门控；会话跳过标记；recovery **之后**再启动自检 |

## Dialog UX

- **模态**：`ShowDialog()`，`Owner = MainWindow`（与 `CriticalOpRecoveryDialog` 一致）
- 标题：发现新版本（本地 → 远端）
- 正文：`notes`（长文本可滚动）
- **可下载时**：下载前 **更新** / **跳过**；下载中进度条 + 已下载量/百分比；**更新** 禁用
- **仅浏览页时**：主按钮 **打开发布页** / **跳过**（无进度条、不调用 downloader）
- 下载并成功 `Process.Start` 后：立即 `Shutdown`，不保留主窗或更新窗

### 取消 vs 跳过（修自检歧义）

| 动作 | 行为 |
|------|------|
| **跳过**（下载前） | 关窗；置本会话「启动跳过」；不退出进程 |
| **取消**（下载中） | 中止下载；**删除不完整临时文件**；对话框回到「下载前」态（可再点 **更新** 或 **跳过**）；**不**因取消自动置启动跳过 |
| **跳过**（下载失败后） | 同下载前跳过 |
| **重试**（下载失败后） | 留在对话框，重新走下载；不置跳过 |

即：只有明确点 **跳过**（或关闭对话框且视为跳过——关闭对话框 = 跳过）才标记本会话启动不再弹。取消下载 ≠ 跳过。

## Startup timing

1. `App`：`window.Show()` → **同步** `OfferCriticalOpRecovery(...)`（现有）→ **仅在其返回后** fire-and-forget `RunStartupManagerUpdateCheckAsync()`  
   - **禁止**与 recovery `ShowDialog` 并行启动管理器自检  
   - Mod 目录 `RunStartupModUpdateCheckAsync` 可继续与管理器自检并行（互不阻塞）
2. 管理器自检 **独立于**「启动时检查 Mod 更新」；本期默认每次启动静默查 `latest.json`
3. 已打开更新窗、正在下载、或本会话已跳过：启动路径不再弹
4. Soft 门在启动路径上与手动相同：先 `CriticalOpSoftUpdateConfirm`，再检查/弹更新窗（有意，非缺陷）

## Download security & paths

- 仅下载通过「可下载 URL」规则的地址；否则不调用 downloader
- `HttpClient`：允许自动重定向时，**最终** `RequestUri` / 响应 URI 仍须为 https；若落到非 https → 失败，不落盘
- 落盘目录：`%TEMP%\mmm-update\<sanitizedVersion>\`  
  - `sanitizedVersion`：只保留 `[A-Za-z0-9._-]`，其余替换为 `_`；空则用 `unknown`
  - 文件名固定：`MechabellumModManager_Setup.exe`（覆盖同路径旧文件）
- 长超时（分钟级）；支持 `CancellationToken`；取消或失败清理 part/不完整文件
- 启动 Setup：**本地文件路径** only；禁止把 URL 交给 Shell 当「安装」
- 仅在 `IProcessStarter.Start` **成功之后**才 `Shutdown`

## Close / session lock during download

- 下载进行中：主窗 `Closing` 走与更新相同的 Soft 确认（或专用文案「正在下载更新，确认关闭并取消下载？」）  
  - 用户确认 → 取消下载 → 允许关闭  
  - 用户拒绝 → `e.Cancel = true`
- CriticalOp Hard 期间本就不会进入下载

## Error handling

| Case | Startup | Manual |
|------|---------|--------|
| Already latest | 静默 | 状态栏/日志「已是最新」 |
| Check failed | **仅**日志 + 状态栏；**不**弹 Confirm / 不弹更新窗 | 保留现有国内提示 Confirm → 复制说明 / 仍开 GitHub Releases |
| UpdateAvailable + 可下载 | 开更新窗 | 同左 |
| UpdateAvailable + 仅浏览页 | 开更新窗（打开发布页） | 同左 |
| Download fail | （仅手动/已开窗）对话框内错误 + **重试** / **跳过**；进程不退 | 同左 |
| Cancel download | 回下载前态；进程不退；不置跳过 | 同左 |
| Setup Start fails | 对话框/通知错误；进程不退 | 同左 |
| CriticalOp Hard | 静默 return | `_notify` 阻止 |
| CriticalOp Soft | 强确认，拒则中止 | 同左 |
| Skipped this session | 启动不再弹 | 手动仍可开窗 |

## Testing

- `ManagerSetupDownloader`：https `.exe` 可下；非 https / 重定向到 http 拒绝；取消返回 cancelled 且无残留 part；进度在已知 Content-Length 时上报
- URL 分类：releases 页 / 无 `.exe` → 不调用 download；`.exe` + query 仍算可下载
- `UpdateChecker`（若收紧字段）：非法 setupUrl → `SetupUrl` null + `BrowseUrl` 有值；合法 exe 则相反
- 编排：有更新才开窗；跳过 → 启动路径不再开；取消下载 → 启动路径仍可再开（因未跳过）；手动始终可开
- Recovery：自检在 recovery 返回后才开始（可用探测钩子/调用顺序测试）
- Gates：Hard 不下载；Soft 确认后才继续；下载中关闭需确认
- 进程生命周期：假 `IProcessStarter` 断言 Start→Shutdown；Start 失败 → 无 Shutdown

## Acceptance

1. 无新版本启动：不出现更新窗
2. 有新版本启动：出现更新窗；点跳过后可进主界面；同会话启动路径不再弹
3. 点更新：应用内下载有进度；成功拉起 Setup 后管理器退出
4. 手动「检查更新」与启动共用同一对话框与下载路径
5. CriticalOp Hard/Soft 行为与现网一致（含启动 Soft 强确认）
6. 下载/启动失败时主程序仍可用；取消下载≠会话跳过
7. `setupUrl` 缺失或仅为 Release 页时：**不**把网页当 exe 下载；可打开发布页
8. 启动检查失败不弹 Confirm；手动失败仍走国内提示流
9. 用户文档同步：`README.md`「检查更新」节、`docs/user/国内镜像搭建.md` 中「原样交给浏览器打开」改为「客户端下载 `setupUrl` 指向的 Setup」（镜像仍须把 `setupUrl` 改写成镜像 exe）

## Out of scope follow-ups

- Manifest 增加 `sha256` 并校验
- 设置项关闭「启动时检查管理器更新」
- Inno `/SILENT` 可选路径
- PE 头/ Authenticode 校验
