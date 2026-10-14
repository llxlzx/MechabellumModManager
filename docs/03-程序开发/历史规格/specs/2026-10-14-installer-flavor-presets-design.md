# 安装包国内 / 国际预设（CN / Global 两个 Setup）

**日期：** 2026-10-14

**范围：** 只改安装包与首次配置。程序本体仍是同一个 exe，没有编译期分支，测试只跑一套。

## 目标

同一份 Inno 脚本编出两个 Setup：

- `MechabellumModManager_Setup_vX.Y.Z_CN.exe`：国内玩家用
- `MechabellumModManager_Setup_vX.Y.Z_Global.exe`：海外玩家用

两个包 `AppId` 相同，装出的文件完全相同，互相覆盖升级。差别只有两处：

1. 首次安装写进 `config.json` 的下载线路 `downloadRoutePreference`
2. 全新安装时，安装末尾下载运行时组件（Melon / UnityDependencies / Cpp2IL / .NET 8）走不走国内镜像

两个包都是瘦包，运行时组件仍在安装末尾下载。

预设定下之后，`BeginStartupChecksAsync` 不再跑 Auto 线路那次 Cloudflare 探测（大陆常见 2 秒超时），启动后的目录、公告、更新检查直接走对应线路。

## 口味预设

| 口味 | `downloadRoutePreference` | 含义 | 全新安装时的运行时下载 |
| --- | --- | --- | --- |
| `cn` | `1`（AlwaysMirror） | 国内镜像优先，GitHub 兜底 | 镜像优先（`EnsureRedistCli` 内置 COS 默认） |
| `global` | `2`（AlwaysGitHub） | 只走 GitHub，不碰镜像，不产生 COS 账单 | `--no-mirror` |

- `mirrorBaseUrl` 两个口味都不写。缺省（null）由程序填工厂默认 COS 地址，空字符串表示玩家关掉了镜像。
- 安装语言、五语言资源、按界面语言切换投稿邮箱：两个包一致，不动。

## 升级时保留玩家设置

现状缺陷：`WriteManagerConfigNative` 用白名单重写 `config.json`，升级时丢掉 `downloadRoutePreference` 与 `mirrorBaseUrl`。玩家选的「总是 GitHub」每次升级都退回 Auto，关掉的镜像每次升级都被重新填上。

改为：

- `downloadRoutePreference`：已有配置里是 0–2 就原样保留；缺失或越界时写口味预设。
- `mirrorBaseUrl`：已有配置里有这个键（null 或字符串）就把原始 JSON 值原样写回，不做反转义再转义，避免 `\u0026` 一类转义被写坏；没有这个键就不写。
- 老用户（升级前配置里已有 `downloadRoutePreference: 0`）继续用 Auto，行为与今天一致。预设只作用于全新安装和缺这个键的配置。

`config.json` 里其他白名单外的键（`checkModUpdatesOnStartup`、`hideMelonConsole`、`authorInviteCode` 等）同样会被丢掉，不在本次范围。

## 安装期运行时下载走哪条线

安装末尾 `EnsureRedistViaApp` 不再按安装语言判断，也不再写死 COS 地址，改为由 `RedistRouteArg` 读刚写好的 `config.json` 决定参数：

1. `downloadRoutePreference = 2`，或 `mirrorBaseUrl` 是空字符串：传 `--no-mirror`，只用官方源
2. `downloadRoutePreference = 1`：传 `--route-mirror`，用内置 COS 默认镜像优先，不做地区探测
3. `downloadRoutePreference = 0`：不传参数，`EnsureRedistCli` 用内置 COS 默认，并自己探测是否在大陆
4. 读不到配置或没有这个键：按口味的预设值（`cn` 为 1，`global` 为 2）走上面对应的一条

参数只会是这几个固定字符串，`config.json` 里的文本不会拼进命令行。玩家自定义的镜像地址不传给安装期下载，安装期始终用内置默认镜像。

为什么跟随已有配置而不是只看口味：国内玩家用应用内更新时，镜像上的 `latest.json` 只有在 `sync-mirror -IncludeSetup` 上传过 Setup 时才指向 COS 上的 CN 包，否则指向 GitHub 上的 Global 包。如果只看口味，Global 包会让大陆玩家在 Melon 版本升级时去 GitHub 下载运行时组件。

安装进度文字按实际线路选：走镜像用现有的 `StatusEnsureRedist`，不走镜像用新增的 `StatusEnsureRedistOrigin`（五语言）。

## 首次启动合并

管理员身份安装时，安装器写的是管理员账户的 AppData，日常账户靠 `ProgramData\MechabellumModManager\install-defaults.json` 合并。

- `InstallDefaults` 增加 `int? DownloadRoutePreference`。
- `InstallDefaultsMerger.TryMerge`：用户配置文件不存在、且种子值在 0–2 之间时写入，规则与 `UiLanguage` 一致。配置已存在时不覆盖。
- 安装器写种子时带上实际写入的 `downloadRoutePreference`。
- `InstallConfigSeeder`（`--seed-install-config` 命令行）增加 `--download-route N`，同时写进用户配置（仅当配置文件原本不存在）和种子，与原生写入器对齐。
- 修复脚本 `Write-ManagerConfig.ps1` 增加 `-DownloadRoute`，只在键不存在时写。

## 安装器脚本

- 顶部：`#ifndef MMM_FLAVOR` 时默认 `cn`；只接受 `cn` / `global`，其他值 `#error`。由此派生 `FlavorSuffix`（`CN` / `Global`）与 `FlavorRoute`（`1` / `2`）。
- `OutputBaseFilename=MechabellumModManager_Setup_v{#MyAppVersion}_{#FlavorSuffix}`。
- 准备安装页（Ready 页）备注里加一节「下载线路」，说明本包的预设和可在设置里更改，五语言。用 Ready 页而不是完成页：完成页标签变高会盖住「立即启动」复选框。

## 构建与发版

- `build-installer.ps1 -Flavor cn|global|both`（默认 `both`）：publish 一次，按口味各调一次 `ISCC /DMMM_FLAVOR=<f>`。
- `build-installer.bat`：可选参数 `cn` / `global` / `both`，或环境变量 `MMM_FLAVOR`，默认两个都编。
- GitHub Release 两个 Setup 都上传。GitHub 上的 `latest.json`（Release 资产与 `release/latest.json` 指针）`setupUrl` 指向 `_Global.exe`。
- `sync-mirror.ps1 -IncludeSetup` 优先上传 `*_CN.exe`，没有再退回任意 `*Setup*.exe`，并把镜像 `latest.json` 的 `setupUrl` 改写到 COS 上的 CN 包。
- `latest.json` 结构不变。`UpdateChecker` 的 API 兜底取第一个含 `Setup` 的资产，可能拿到另一口味，因为升级保留线路设置，所以无害。

## 测试

自动化：

- `InstallDefaultsMergerTests`：首次且种子有合法值时写入；配置已存在时不覆盖；越界值忽略。
- `InstallConfigSeederTests`：`--download-route` 解析；写入用户配置与种子；已有配置不覆盖线路。
- `InstallerScriptContractTests`（读 `.iss` 文本）：有 `MMM_FLAVOR` 校验；文件名带口味后缀；不再出现 `myqcloud.com`；不再按 `ActiveLanguage` 决定镜像；`WriteManagerConfigNative` 读写 `downloadRoutePreference` 与 `mirrorBaseUrl`；种子带线路。
- 两个口味都用 ISCC 实际编译通过。

真机手工验收：

1. 干净机器装 CN：`config.json` 里 `downloadRoutePreference` 为 1；`ensure-redist.log` 显示 `mirror=https://...myqcloud.com`；启动后不跑线路探测。
2. 干净机器装 Global：`downloadRoutePreference` 为 2；`ensure-redist.log` 显示 `mirror=(none)`。
3. 装 CN，在设置里改成「总是 GitHub」并清空镜像地址；再用 Global 或 CN 包覆盖安装：线路仍为 2，`mirrorBaseUrl` 仍为空字符串，运行时下载走 `--no-mirror`。
