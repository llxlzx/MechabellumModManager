# 国内版 / 国际版：明显区分与环境适配

**日期：** 2026-10-14

**范围：** 在「国内包 / 国际包」安装预设之上，给管理器加一个运行时的「版本」（`edition`：`cn` / `global`）。同一个 exe；安装包决定全新安装的版本，设置里可以切换。

## 版本决定什么

| 方面 | 国内版 `cn` | 国际版 `global` |
| --- | --- | --- |
| 标题与标识 | 窗口标题、标题栏徽标、关于页、安装向导与卸载列表都标「国内版」 | 都标「国际版 / Global」 |
| 默认下载线路 | 始终国内镜像（1） | 始终 GitHub（2） |
| 系统语言不受支持时 | 回落简体中文 | 回落英文 |
| 邮箱 | QQ 邮箱排第一并高亮，Gmail 仍可选 | Gmail 排第一并高亮，QQ 邮箱仍可选 |
| 诊断反馈的社区入口 | QQ 群（未配置则不显示） | Discord |
| 投稿说明、发布页链接 | 镜像上的副本（`submit.html`、`download.html`）；镜像关闭或线路为 GitHub 时用 GitHub | GitHub |
| 应用内更新 | 下载 `setupUrlCn`（国内包） | 下载 `setupUrlGlobal`（国际包） |

## 版本从哪来（`EditionPolicy.Resolve`）

1. 配置里有合法的 `edition`：直接用。
2. 没有（升级上来的老用户）：推断一次并保存。线路 2 推断为国际版；线路 1 推断为国内版；线路 0 时看系统语言，中文为国内版，否则为国际版。推断不改动玩家现有的任何设置。
3. 全新安装：安装器写入本包的 `edition`（`cn` 或 `global`），连同上一轮已有的线路预设。管理员身份安装时，`install-defaults.json` 也带上 `edition`，日常账户首次启动时合并。

## 与原计划的一处差异：安装器不覆盖已有配置的版本

原计划是「装哪个包就是哪个版本」，每次安装都写 `edition`。没有采用，原因是会造成升级回退：

- 这个版本发布之前，所有老用户的更新源（GitHub 上的 `latest.json`，以及没有用 `-IncludeSetup` 同步的镜像副本）指向的都是国际包。
- 如果每次安装都写版本，国内老玩家第一次通过应用内更新升级，就会被标成国际版：标题变成国际版、邮箱改为 Gmail，下载线路也被改到 GitHub。

所以安装器只在**全新安装**（配置文件不存在）时写 `edition`。已有配置的 `edition` 原样保留，没有的交给程序推断。之后切换版本只在设置里进行。也因此不需要原计划中的 `EditionApplied` 字段。

## 切换版本（设置页）

设置页「版本」下拉框，切换即时生效：

- 下载线路改为该版本的默认值（国内 1、国际 2），并刷新线路。
- 标题、徽标、关于页、链接和邮箱默认值随之更新。
- 系统语言不受支持时的回落语言随之变化；如果当前语言是「跟随系统」，会立刻重新应用。
- 日志记一行「已切换到 X」。

## 各部分实现

- **模型**：`Models/AppEdition.cs` 定义 `enum AppEdition { Cn, Global }` 与 `EditionPolicy`（解析、格式化、推断、各版本默认值）。`AppConfig.Edition` 是字符串，缺失或非法时由 `Resolve` 补齐。
- **语言**：`LocalizationService.ResolveSystemLanguage(fallback)`、`ResolveConfiguredLanguage(code, fallback)` 增加回落参数，默认仍是 `zh-CN`。程序启动时先读配置里的版本再应用语言。
- **社区**：`GitHubCommunityLinks.PreferredMail(edition)` 取代按界面语言猜测的 `PreferDomesticWebMail()`。国内版 QQ 群号在 `QqGroupNumber`（`1127809375`）。`QqGroupUrl` 是分享链接 `https://qm.qq.com/q/gLdHoBqp58`，入口打开这个页面。链接为空时才退回复制群号。`MailProviderDialog` 按版本排列并高亮首选邮箱，第三个按钮按版本显示 Discord 或 QQ 群。`SubmitGuideDialog` 的邮箱链接和「投稿说明」链接按版本取。
- **链接**：`EditionLinks` 统一算出投稿说明与发布页的地址。只有国内版、配置了 https 镜像、且线路不是「始终 GitHub」时才用镜像副本。
- **更新**：`UpdateManifest` 增加 `setupUrlCn` / `setupUrlGlobal`，缺省时回落 `setupUrl`。GitHub API 兜底时，按资产名里的 `_CN` / `_Global` 选安装包。发布页地址同样按版本给出。
- **镜像同步**：`sync-mirror.ps1` 额外上传 MechabellumMods 的 `docs/submit.html`，并根据镜像那份 `latest.json` 生成 `download.html` 一起上传（`Content-Type: text/html; charset=utf-8`）。`-IncludeSetup` 时同时把 `setupUrlCn` 改写到 COS 上的国内包。
- **安装器**：`#define FlavorEdition`。`AppName` 用带版本后缀的 `AppDisplayNameEdition`，所以安装向导标题和卸载列表都能看出版本。开始菜单与桌面快捷方式的名称不带后缀，换装另一个包时不会留下两个快捷方式。`VersionInfoDescription` / `VersionInfoProductName` 按版本给出（顺带消除了之前的两个编译警告）。
- **标识**：窗口标题绑定 `WindowTitle`，标题栏副标题旁显示版本徽标，关于页上方加一段版本说明。启动日志、诊断包的 `probe.edition` 都记录版本。

## 文案（五语言）

`EditionCn`、`EditionGlobal`（下拉框选项）、`EditionShortCn`、`EditionShortGlobal`（标题与徽标）、`WindowTitleFormat`、`EditionLabel`、`EditionHint`、`EditionAboutCn`、`EditionAboutGlobal`、`LogEditionStartup`、`LogEditionSwitched`、`DiagnosticsMailQqGroup`、`DiagnosticsMailHintCn`、`DiagnosticsQqGroupCopied`、`DiagnosticsQqGroupOpened`、`DiagnosticsQqGroupOpenFailed`。

## 测试

- `EditionPolicyTests`：解析与格式化；推断（线路 2 / 1 / 0 加系统语言）；已有合法版本不改动；各版本默认值。
- `LocalizationServiceTests`：系统语言不受支持时按回落参数取语言。
- `EditionLinksTests`：国内版 + 镜像用镜像副本；国际版、镜像关闭、线路为 GitHub 时用 GitHub。
- `GitHubCommunityLinksTests`：首选邮箱按版本。
- `UpdateCheckerTests`：按版本选 `setupUrlCn` / `setupUrlGlobal`，缺省回落 `setupUrl`；发布页地址按版本。
- `MainViewModel` 层：老配置推断并保存；切换版本后标题、线路、配置都变化；诊断社区入口按版本。
- 安装器：`InstallerScriptContractTests` 覆盖 `edition` 的写入与保留、`AppName` 后缀、快捷方式名称不带后缀；`InstallDefaultsMergerTests` / `InstallConfigSeederTests` 覆盖 `edition` 的种子与合并。两个口味都实际编译；另用临时 Setup 跑全新安装、已有配置有版本、已有配置无版本三种情况。

## 需要维护者补的信息

- QQ 群号与加群链接都已写入：`QqGroupNumber` 为 `1127809375`，`QqGroupUrl` 为 `https://qm.qq.com/q/gLdHoBqp58`。
- 镜像首次同步 `submit.html` / `download.html` 之后，用浏览器确认能直接打开，不是被当作下载。
