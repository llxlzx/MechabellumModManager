# GitHub Release 更新流程（现行）

> **说明 / Notice**  
> 本文档整理自本项目维护者在使用 Git 仓库与开发 Mechabellum（钢铁指挥官）Mod 管理器过程中的实践记录，旨在为有意开发类似工具或复现本项目发版流程的开发者提供参考。文中涉及的目录、代理与环境变量均为**示例**，请按本机环境自行调整；内容不构成官方承诺、服务条款或完整运维规范。  
> **This document collects the maintainer’s practical notes on using Git and developing the Mechabellum Mod Manager. It is shared as a reference for developers who wish to build similar tools or reproduce this release workflow. Paths, proxy settings, and environment variables are examples only—adapt them to your environment. This text is not an official commitment, terms of service, or a complete operations manual.**

> **⚠️ AI 生成声明 / AI-generated notice**  
> 本流程摘要的中英文主要由 AI 辅助整理，可能存在表述偏差；逐步图文说明见 `GitHub-Release更新说明.md`。  
> **This compact checklist was largely AI-assisted. Prefer `GitHub-Release更新说明.md` for the full bilingual walkthrough.**

两个相关仓库 / Related repos：

| 仓库 / Repo | 用途 / Purpose |
|-------------|----------------|
| https://github.com/llxlzx/MechabellumModManager | 管理器程序 + Setup / 本体 |
| https://github.com/llxlzx/MechabellumMods | Mod 浏览大全（catalog + dll + preview） |

管理器检查更新并行读下列三处，取版本最高的一份 / Update check reads all three in parallel and keeps the highest version：

1. `{镜像基址}/MechabellumModManager/latest.json`
2. `https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`
3. `https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json`（仓库指针 / repo pointer）

版本相同则保留镜像那份（国内玩家仍走镜像下载）；仅当两侧都带 `publishedAt` 且另一侧更新时才改判。版本号无法解析的 manifest 直接丢弃。三处都失败时回退 API / Fallback API：`/repos/llxlzx/MechabellumModManager/releases/latest`。

自 **v2.0.1** 起每版出两个 Setup：国内包 `_CN.exe` 与国际包 `_Global.exe`（见 [§2.1](#21-国内包与国际包-中文)）。GitHub 上的 `latest.json` 指向 `_Global.exe`，镜像上的 `latest.json` 只有用 `-IncludeSetup` 同步时才指向 COS 上的 `_CN.exe`。
From **v2.0.1** each release ships two Setups, `_CN.exe` and `_Global.exe` (see [§2.1](#21-china-and-global-packages-english)). The GitHub `latest.json` points at `_Global.exe`; the mirror copy points at the COS `_CN.exe` only when synced with `-IncludeSetup`.

自 **v1.2.1** 起仓库根目录的 `release/latest.json` 是发版指针，**每次发版都要更新它**。它只负责「宣告」，`setupUrl` 仍必须指向可访问的 https（Release 资产，或 `sync-mirror.ps1 -IncludeSetup` 镜像出来的地址）。

> **⚠️ 指针必须最后推送。** 它一进 master 就会被所有玩家读到，若此时 `setupUrl` 指向的 Release 资产还不存在，玩家会被告知有新版本却下载 404。所以仓库里的 `release/latest.json` 始终指向**当前已经能下载**的那一版；发新版时把它留到最后一步（见 [第 8 节](#8-最后一步推送仓库指针-中文)）再改。

## 语言 / Language

- 中文：[发版前准备](#1-发版前准备-中文) · [本地出包](#2-本地出包-中文) · [创建 Release](#3-创建-github-release网页-中文) · [gh 备选](#4-命令行备选已装-gh-中文) · [Mod 大全](#5-mod-大全仓库与程序发版分开-中文) · [对照](#6-当前最新本地对照写作时-中文) · [更新行为](#7-管理器更新行为提醒用户-中文) · [推送仓库指针](#8-最后一步推送仓库指针-中文)
- English: [Prepare](#1-before-you-ship-english) · [Build](#2-build-locally-english) · [GitHub Release](#3-create-github-release-web-english) · [gh CLI](#4-cli-alternative-gh-english) · [Catalog](#5-mods-catalog-repo-separate-from-app-english) · [Local paths](#6-local-paths-at-write-time-english) · [Update behavior](#7-manager-update-behavior-english) · [Repo pointer](#8-last-step-push-the-repo-pointer-english)

---

## 1. 发版前准备 (中文)

### 1.1 改版本号（三处一致）

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>X.Y.Z</Version>`
2. `packaging/installer/MechabellumModManager.iss` → `#define MyAppVersion "X.Y.Z"`
3. 稍后写入的 `release/vX.Y.Z/latest.json` → `"version": "X.Y.Z"`

此时**不要**动 `release/latest.json`。它是对外生效的指针，留到 [第 8 节](#8-最后一步推送仓库指针-中文)。

### 1.2 运行时离线包（镜像暂存，**不**再打进 Setup）

自 **v1.2.0** 起 Setup 为瘦包。将下列文件放到 `packaging/installer/redist\`，供 `tools/sync-mirror.ps1` 上传到 `MechabellumRedist/`（**默认同步**；调试可用 `-SkipRedist`）：

| 路径 | 说明 |
|------|------|
| `melonloader/MelonLoader.x64.zip` | Melon 0.7.3 钉死版 |
| `unity-deps/UnityDependencies_2022.3.62.zip` | Unity 运行时依赖 |
| `cpp2il/Cpp2IL.exe` + plugin dll | 与 Melon 0.7.3 匹配 |
| `dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` | .NET 8 Desktop |

清单模板：`src/MechabellumModManager/Assets/redist-manifest.json`。  
发版顺序：**先 sync-mirror（含 redist）→ verify-mirror → 再打瘦 Setup / 发 GitHub Release**。

### Catalog / latest.json caching (clients ≥ scale-friendly detection)

Mirror objects should keep normal COS ETags. Avoid Cache-Control that forces every client GET to ignore validators and always download a full body. Conditional requests (`If-None-Match`) are how installed clients stay quiet between publishes.

安装向导末尾与程序内安装 Melon 会 `--ensure-redist`：COS → GitHub/微软，强制 sha256。

### 1.2b（历史）fat Setup

v1.1.x 及以前把上述文件嵌入 Setup。新版本不要再依赖 `build-installer` 的硬门禁；旧 design 文档仅作历史参考。

### 1.3 代码与测试

```powershell
cd <path-to-your-MechabellumModManager-clone>
dotnet test -c Release
# 勿提交 Melon zip / publish / dist；release/latest.json 留到第 8 节
git add -A -- . ':!release/latest.json'
git commit -m "..."
git push origin master
```

若浏览器可访问 GitHub，但终端 `git` / `gh` 失败，可按本机代理软件说明设置 HTTP(S) 代理环境变量（地址与端口以你的客户端为准，以下仅为占位示例）：

```powershell
$env:HTTPS_PROXY = 'http://127.0.0.1:<PORT>'
$env:HTTP_PROXY  = 'http://127.0.0.1:<PORT>'
```

---

## 2. 本地出包 (中文)

```powershell
.\packaging\installer\build-installer.ps1
```

产物：`dist\MechabellumModManager_Setup_vX.Y.Z_CN.exe` 与 `dist\MechabellumModManager_Setup_vX.Y.Z_Global.exe`。默认两个都编；只编一个用 `-Flavor cn` 或 `-Flavor global`（`build-installer.bat` 用第一个参数或 `MMM_FLAVOR` 环境变量）。

### 2.1 国内包与国际包 (中文)

两个包同一 `AppId`，装出的程序完全相同，可以互相覆盖升级。区别只在：

| 包 | 全新安装写入的下载线路 | 全新安装时运行时组件（Melon / .NET 等）来源 |
|----|------------------------|---------------------------------------------|
| `_CN.exe` | 始终使用国内镜像（`downloadRoutePreference = 1`，镜像不可用时改用 GitHub） | 国内镜像优先 |
| `_Global.exe` | 始终使用 GitHub（`downloadRoutePreference = 2`，不产生 COS 流量） | 只用官方源（`--no-mirror`） |

- 覆盖安装时保留玩家已有的线路和镜像地址，两个包都一样。运行时组件的来源也跟随已有线路，没有配置时才按包的口味。
- 所以国内玩家通过应用内更新拿到 Global 包也不会被切到 GitHub。
- 准备安装页会写明本包的预设。玩家装错了包，也可以在管理器「设置 → 下载线路」里改。
- 设计说明：`docs/03-程序开发/历史规格/specs/2026-10-14-installer-flavor-presets-design.md`。

### 2.2 管理器的「国内版 / 国际版」 (中文)

全新安装时，安装包还会把版本（`edition`）写进配置；设置页可以随时切换。版本决定：

| | 国内版 | 国际版 |
|--|--------|--------|
| 标识 | 窗口标题、标题栏徽标、关于页、安装向导和卸载列表都标「国内版」 | 都标「国际版」 |
| 默认下载线路 | 始终国内镜像 | 始终 GitHub |
| 系统语言不受支持时 | 简体中文 | 英文 |
| 首选邮箱 | QQ 邮箱 | Gmail |
| 诊断包的社区入口 | QQ 群 `1127809375`，打开 `https://qm.qq.com/q/gLdHoBqp58` | Discord |
| 投稿说明、下载页 | 镜像上的 `submit.html` / `download.html` | GitHub |
| 应用内更新安装的包 | `setupUrlCn` | `setupUrlGlobal` |

- 覆盖安装不会改动已有配置里的版本；升级上来、还没有版本的老配置由程序推断一次（线路 2 为国际版，线路 1 为国内版，否则看系统语言）。这样国内老玩家通过旧的更新源拿到国际包时，不会被改成国际版。
- 在设置里切换版本，会把下载线路改为该版本的默认值。
- 设计说明：`docs/03-程序开发/历史规格/specs/2026-10-14-edition-distinction-design.md`。

整理到仓库对照目录：

```
release/vX.Y.Z/
  安装包/
    MechabellumModManager_Setup_vX.Y.Z_CN.exe
    MechabellumModManager_Setup_vX.Y.Z_Global.exe
    README.txt
  本体/
    MechabellumModManager.exe
    Assets\
    README.txt
  latest.json
```

**本体内容规则（干净便携版，无 Mod 数据）：**

- **只保留**：`MechabellumModManager.exe` + `Assets\`（+ 可选 `README.txt`）
- **不要**把「Setup 安装后的目录」整包当本体（勿含 `unins000.*`、`installer-redist\`、`installer-scripts\`、本地 `data`/`library`）
- 推荐直接用 `publish\` 或本目录 `release/vX.Y.Z/本体\`，与安装测试文件夹无关

将本体打成 zip（上传 Release 用），例如在 `release/vX.Y.Z/` 下：

```powershell
Compress-Archive -Path ".\本体\*" -DestinationPath ".\MechabellumModManager_portable_vX.Y.Z.zip" -Force
```

`latest.json` 示例。`setupUrlCn` / `setupUrlGlobal` 分别给国内版和国际版的应用内更新使用；`setupUrl` 留给还不认识版本的旧管理器，指向国际包。`sync-mirror.ps1 -IncludeSetup` 会把镜像那份的 `setupUrl` 和 `setupUrlCn` 改写为 COS 上的国内包；`setupUrlGlobal` 保持 GitHub：

```json
{
  "version": "X.Y.Z",
  "notes": "本版更新说明……",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z_Global.exe",
  "setupUrlCn": "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z_CN.exe",
  "setupUrlGlobal": "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z_Global.exe",
  "portableUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_portable_vX.Y.Z.zip",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

同步镜像时，`sync-mirror.ps1` 还会：上传 MechabellumMods 的 `docs/submit.html`；按镜像那份 `latest.json` 生成 `MechabellumModManager/download.html` 并上传（国内版的「打开下载页」会打开它）。首次同步后，用浏览器打开 `{镜像}/MechabellumModManager/download.html` 和 `{镜像}/MechabellumMods/docs/submit.html`，确认是直接显示网页而不是下载文件。

把 `release/vX.Y.Z/` 提交并推送到管理器仓库（可选但建议，便于对照）。

---

## 3. 创建 GitHub Release（网页） (中文)

1. 打开 https://github.com/llxlzx/MechabellumModManager/releases → **Draft a new release**
2. **Tag**：`vX.Y.Z`（Create new tag on publish），**Target**：`master`
3. **Title**：`vX.Y.Z`
4. **Describe**：写清本版要点（可与 `latest.json` 的 notes 一致）
5. **Attach binaries**（拖入；注意文件名）：

| 资源 | 本地来源 | 上传后文件名 |
|------|----------|----------------|
| 国内包（必传） | `release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_CN.exe` | 保持原名 |
| 国际包（必传） | `release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_Global.exe` | 保持原名 |
| 更新元数据（必传） | `release/vX.Y.Z/latest.json` | **必须仍是 `latest.json`** |
| 本体便携包（推荐） | `MechabellumModManager_portable_vX.Y.Z.zip` | 建议带版本号 |

6. 勾选 **Set as the latest release**；不要勾 Pre-release  
7. **Publish release**

验证：

- https://github.com/llxlzx/MechabellumModManager/releases/tag/vX.Y.Z  
- https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json  
- 管理器「设置 → 检查更新」

---

## 4. 命令行备选（已装 gh） (中文)

```powershell
cd <path-to-your-MechabellumModManager-clone>
# Optional: set proxy if CLI cannot reach GitHub (port per your proxy client)
$env:HTTPS_PROXY = 'http://127.0.0.1:<PORT>'
$env:HTTP_PROXY  = 'http://127.0.0.1:<PORT>'

gh release create vX.Y.Z `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_CN.exe" `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_Global.exe" `
  "release/vX.Y.Z/latest.json" `
  "release/vX.Y.Z/MechabellumModManager_portable_vX.Y.Z.zip" `
  --title "vX.Y.Z" `
  --notes-file - `
  --target master
```

若 Release 已存在，只更新附件：

```powershell
gh release upload vX.Y.Z `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_CN.exe" `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_Global.exe" `
  "release/vX.Y.Z/latest.json" `
  --clobber
```

---

## 5. Mod 大全仓库（与程序发版分开） (中文)

Mod 内容在 **MechabellumMods**，不随 Setup 版本强制同步，但若改了 catalog / preview / dll：

```powershell
cd <path-to-your-MechabellumMods-clone>
git add -A
git commit -m "..."
git push origin master
```

确认：  
`https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json`

管理器「Mod 浏览 → 刷新目录」应能看到更新。作者提交流程见该仓库 `README.md`。

### 5.1 下架一个 Mod (中文)

下架名单必须用维护者私钥签名。玩家下次启动、刷新目录、导入或应用方案时，管理器校验签名，再把命中的 Mod 从本地库、所有方案的勾选、以及游戏目录里能确认字节的副本中删掉，并在日志和提示里写明原因。「启动时检查 Mod 更新」关掉的玩家同样会执行。没有签名、签名不对或比已采用的名单旧的名单一律忽略，所以镜像被攻破也无法借此删除玩家的 Mod。

**一次性准备（只做一次）：**

```powershell
# 私钥必须放在任何 git 仓库之外；工具会拒绝写进仓库，并把文件权限收紧到当前用户
.\tools\sign-removed.ps1 -NewKey -KeyFile D:\keys\mmm-removal-key.json
```

把输出的 `PublicKey:` 填进 `src/MechabellumModManager/Services/RemovalListVerifier.cs` 的 `PublicKey`，发布这个版本的管理器。在此之前，任何名单都不会生效。私钥离线备份：丢了要换公钥重新发版，泄露则任何人都能下架。

**每次下架：**

1. 下架前先收集这个 Mod 发过的所有 `sha256`（在 MechabellumMods 本地克隆里运行）：

   ```powershell
   $id = "opponent-economy"
   git log --format=%H -- catalog.json | ForEach-Object {
       ((git show "$($_):catalog.json") -join "`n" | ConvertFrom-Json).mods |
           Where-Object id -eq $id | ForEach-Object { $_.sha256 }
   } | Sort-Object -Unique
   ```

2. 把条目从 `catalog.json` 的 `mods` 移走，在 MechabellumMods 根目录的 `removed.json`（JSON 数组，保留全部历史下架项）里加一条：

   ```json
   [
     {
       "id": "opponent-economy",
       "name": "Opponent Economy",
       "sha256": ["<上一步列出的全部哈希>"],
       "reason": "影响对局公平，已下架",
       "since": "2026-10-14",
       "locales": {
         "en": { "reason": "Affects match fairness; removed" },
         "de": { "reason": "…" }, "ja": { "reason": "…" }, "ru": { "reason": "…" }
       }
     }
   ]
   ```

3. 签名，写进 `catalog.json` 顶层的 `removedSigned`（目录其余内容不变）：

   ```powershell
   .\tools\sign-removed.ps1 -ModsRepo <MechabellumMods 本地克隆> -KeyFile D:\keys\mmm-removal-key.json
   ```

   公钥与本仓库 `RemovalListVerifier.PublicKey` 不一致时脚本会警告。

4. 照常 `validate_catalog.py`、提交 `catalog.json` 与 `removed.json`、推送、`sync-mirror.ps1`（`removedSigned` 随 `catalog.json` 一起同步）。

规则：

- **匹配**：本地包的目录 id 等于 `id`，或任一文件哈希在 `sha256` 列表里。不按名称匹配。游戏目录里的文件只有字节能确认（部署记录、包自身哈希或名单哈希）才删，玩家自己改过的同名 DLL 不会被删。
- **哈希清洗**：只接受 64 位十六进制；空文件哈希一律忽略；仍在 `mods` 里上架的 Mod 的哈希会被扣掉并在日志里警告，所以改名迁移时可以放心把旧 id 的全部历史哈希都列上。
- **延后**：游戏在运行、文件被占用、双服的某个仓不可达，或分支向导、恢复门进行中时，本次不动，之后自动重试；应用方案时也不会把被下架的 Mod 部署出去。
- **签名时间**：默认取当前 UTC 时间（与系统区域格式无关）。脚本会拒绝不晚于 `catalog.json` 里已发布名单的时间、超过当前时间 10 分钟以上的未来时间（否则在那之前签的名单都会被忽略），以及格式不是 `2026-10-14T09:30:00Z` 的 `-IssuedAt`。
- **签名前检查**：`id` 仍在 `mods` 里、哈希仍是某个上架 Mod 正在发的字节、空文件哈希、格式不对的哈希、或签名后超过 1 MiB，脚本都会报错且不改动 `catalog.json`。
- **重新上架**：被下架的 `id` 不要再出现在 `mods` 里；两处都有时按下架处理。要重新上架，先从 `removed.json` 删掉它并重新签名。

设计说明：`docs/03-程序开发/历史规格/specs/2026-10-14-catalog-forced-removal-design.md`。

---

## 6. 当前最新本地对照（写作时） (中文)

- 程序 / Setup：**v1.1.6**（当前最新）→ 仓库内相对路径 `release/v1.1.6/`
- 安装包与本体产物均整理在上述 `release/vX.Y.Z/` 目录（勿依赖本机个人测试文件夹路径）

---

## 7. 管理器更新行为（提醒用户） (中文)

- 不会静默自动安装  
- 「检查更新」提示后，用户自行下载并运行新 Setup  
- 便携「本体」需本机已装 .NET 8 Desktop Runtime  

---

## 8. 最后一步：推送仓库指针 (中文)

只有确认新 Setup **真的能下载**之后才做这一步。否则指针会让所有玩家看到新版本却下载 404。

```powershell
# 1) 确认 setupUrl 可下载（应返回 200）
curl.exe -I -L "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z_Global.exe"

# 2) 把 release/latest.json 改成与 release/vX.Y.Z/latest.json 相同的内容
# 3) 单独提交并推送
git add release/latest.json
git commit -m "release: point repo manifest at vX.Y.Z"
git push origin master
```

验证：`https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json` 返回新版本（raw 有几分钟 CDN 缓存），随后管理器「检查更新」即可发现。

---

## 1. Before you ship (English)

### 1.1 Bump version (three places, same number)

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>X.Y.Z</Version>`
2. `packaging/installer/MechabellumModManager.iss` → `#define MyAppVersion "X.Y.Z"`
3. `release/vX.Y.Z/latest.json` → `"version": "X.Y.Z"`

Leave `release/latest.json` alone here. It is the pointer players actually read, so it always names the
version that is already downloadable, and it is updated last — see [§8](#8-last-step-push-the-repo-pointer-english).

### 1.2 MelonLoader offline zip (required for Setup)

Place official file at:

`packaging/installer/redist/melonloader/MelonLoader.x64.zip`

From: https://github.com/LavaGang/MelonLoader/releases  

Missing file → `build-installer` **hard-fails** (exit code 3). Do not use `-SkipMelonRedistCheck` for real releases.

### 1.2b Unity Il2Cpp deps + .NET 8 offline (required for China-offline fat Setup)

In addition to the Melon zip, release fat Setup requires:

| Path | Source | Notes |
|------|--------|-------|
| `packaging/installer/redist/unity-deps/UnityDependencies_{major.minor.patch}.zip` | https://github.com/LavaGang/Unity-Runtime-Libraries | Upstream names like `2022.3.62.zip` **must be renamed** to `UnityDependencies_2022.3.62.zip` under `unity-deps/` |
| `packaging/installer/redist/dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` | https://dotnet.microsoft.com/download/dotnet/8.0 | Windows x64 Desktop Runtime offline installer |

Resolve the game Unity version from `Mechabellum_Data\globalgamemanagers` (e.g. `2022.3.62f3` → `UnityDependencies_2022.3.62.zip`). Refresh `unity-deps` and ship a new Setup when Mechabellum’s Unity patch bumps.

**Domestic Setup mirror:** Downloading Setup from GitHub Release may still need VPN or a third-party mirror. The fat Setup goal is **post-install** offline first-run Il2Cpp generation (success level **B**) after Melon is written and UnityDependencies is seeded. Mirror distribution is ops/docs only, not in-app.

**Residual probe before calling B done (Cpp2IL, etc.):** On a real machine, run acceptance **F** in `docs/dev/superpowers/specs/2026-09-05-china-offline-melon-fat-setup-design.md`: disconnect network, launch the game once. If Melon still asks for another missing package under `{game}\MelonLoader\Dependencies\Il2CppAssemblyGenerator\` (often Cpp2IL-related), add it to `packaging/installer/redist/`, extend the `build-installer` gate, rebuild Setup, then re-run acceptance before claiming B complete.

### 1.3 Code and tests

```powershell
cd <path-to-your-MechabellumModManager-clone>
dotnet test -c Release
# do not commit Melon zip / publish / dist; release/latest.json waits for §8
git add -A -- . ':!release/latest.json'
git commit -m "..."
git push origin master
```

If the browser reaches GitHub but terminal `git` / `gh` fails, set proxy env vars per your local client (placeholder):

```powershell
$env:HTTPS_PROXY = 'http://127.0.0.1:<PORT>'
$env:HTTP_PROXY  = 'http://127.0.0.1:<PORT>'
```

---

## 2. Build locally (English)

```powershell
.\packaging\installer\build-installer.ps1
```

Output: `dist\MechabellumModManager_Setup_vX.Y.Z_CN.exe` and `dist\MechabellumModManager_Setup_vX.Y.Z_Global.exe`. Both are built by default; build one with `-Flavor cn` or `-Flavor global` (`build-installer.bat` takes the first argument or the `MMM_FLAVOR` environment variable).

### 2.1 China and Global packages (English)

Both packages share one `AppId`, install identical files, and upgrade over each other. They differ only in:

| Package | Download route written on a fresh install | Runtime packages (Melon / .NET, etc.) on a fresh install |
|---------|-------------------------------------------|----------------------------------------------------------|
| `_CN.exe` | Always the domestic mirror (`downloadRoutePreference = 1`; GitHub if the mirror fails) | Domestic mirror first |
| `_Global.exe` | Always GitHub (`downloadRoutePreference = 2`; no COS egress) | Official sources only (`--no-mirror`) |

- A reinstall with either package keeps the player's saved route and mirror URL. Runtime packages follow that saved route too; only a machine without a config uses the package flavor.
- So a mainland player who receives the Global package through the in-app updater is not moved onto GitHub.
- The Ready page states the package preset. A player who picked the wrong package can change it under Settings → Download route.
- Design: `docs/03-程序开发/历史规格/specs/2026-10-14-installer-flavor-presets-design.md`.

### 2.2 China and Global editions of the manager (English)

A fresh install also writes the edition (`edition`) into the config; Settings can switch it at any time. The edition decides:

| | China edition | Global edition |
|--|---------------|----------------|
| Labels | Window title, title-bar badge, About, Setup wizard and uninstall list say "China" | They say "Global" |
| Default download route | Always the domestic mirror | Always GitHub |
| Windows language not shipped | Simplified Chinese | English |
| Preferred mail | QQ Mail | Gmail |
| Diagnostics community link | QQ group `1127809375`, opens `https://qm.qq.com/q/gLdHoBqp58` | Discord |
| Submission guide, download page | Mirror copies `submit.html` / `download.html` | GitHub |
| Setup installed by in-app updates | `setupUrlCn` | `setupUrlGlobal` |

- A reinstall never changes the edition already in a config. An older config without one gets it inferred once (route 2 means Global, route 1 means China, otherwise the Windows language decides), so a mainland player who receives the Global package through the old update feed stays on the China edition.
- Switching the edition in Settings sets the download route to that edition's default.
- `latest.json` carries `setupUrlCn` and `setupUrlGlobal` for the two editions, plus `setupUrl` (Global) for managers that predate editions; see `docs/05-版本分析/latest.example.json`. `sync-mirror.ps1 -IncludeSetup` repoints the mirror copy's `setupUrl` and `setupUrlCn` at the COS China package and leaves `setupUrlGlobal` on GitHub.
- `sync-mirror.ps1` also uploads MechabellumMods' `docs/submit.html` and generates `MechabellumModManager/download.html` from the mirror's `latest.json`. After the first sync, open both in a browser and confirm they display instead of downloading.
- Design: `docs/03-程序开发/历史规格/specs/2026-10-14-edition-distinction-design.md`.

Layout under the repo:

```
release/vX.Y.Z/
  安装包/
    MechabellumModManager_Setup_vX.Y.Z_CN.exe
    MechabellumModManager_Setup_vX.Y.Z_Global.exe
    README.txt
  本体/
    MechabellumModManager.exe
    Assets\
    README.txt
  latest.json
```

**Portable rules (clean, no mod data):**

- Keep only: `MechabellumModManager.exe` + `Assets\` (+ optional `README.txt`)
- Do **not** zip a post-Setup install folder (`unins000.*`, `installer-redist\`, etc.)
- Prefer `publish\` or `release/vX.Y.Z/本体\`

Zip portable from `release/vX.Y.Z/`:

```powershell
Compress-Archive -Path ".\本体\*" -DestinationPath ".\MechabellumModManager_portable_vX.Y.Z.zip" -Force
```

Commit/push `release/vX.Y.Z/` to the manager repo (optional but recommended).

---

## 3. Create GitHub Release (web) (English)

1. Open https://github.com/llxlzx/MechabellumModManager/releases → **Draft a new release**
2. **Tag:** `vX.Y.Z` · **Target:** `master`
3. **Title:** `vX.Y.Z`
4. **Describe:** release notes (can match `latest.json` notes)
5. **Attach:**

| Asset | Local source | Upload name |
|-------|--------------|-------------|
| China Setup (required) | `release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_CN.exe` | keep filename |
| Global Setup (required) | `release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_Global.exe` | keep filename |
| Metadata (required) | `release/vX.Y.Z/latest.json` | **must stay `latest.json`** |
| Portable (recommended) | `MechabellumModManager_portable_vX.Y.Z.zip` | prefer versioned name |

6. Check **Set as the latest release**; do not mark Pre-release  
7. **Publish release**

Verify Releases tag page, `latest.json` download URL, and in-app **Check for updates**.

---

## 4. CLI alternative (gh) (English)

```powershell
cd <path-to-your-MechabellumModManager-clone>
$env:HTTPS_PROXY = 'http://127.0.0.1:<PORT>'
$env:HTTP_PROXY  = 'http://127.0.0.1:<PORT>'

gh release create vX.Y.Z `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_CN.exe" `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_Global.exe" `
  "release/vX.Y.Z/latest.json" `
  "release/vX.Y.Z/MechabellumModManager_portable_vX.Y.Z.zip" `
  --title "vX.Y.Z" `
  --notes-file - `
  --target master
```

Update assets on an existing Release:

```powershell
gh release upload vX.Y.Z `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_CN.exe" `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z_Global.exe" `
  "release/vX.Y.Z/latest.json" `
  --clobber
```

---

## 5. Mods catalog repo (separate from app) (English)

Catalog content lives in **MechabellumMods** and need not ship with every Setup. When catalog / preview / dll changes:

```powershell
cd <path-to-your-MechabellumMods-clone>
git add -A
git commit -m "..."
git push origin master
```

Confirm:  
`https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json`

Players: **Browse mods → Refresh catalog**. Author flow: that repo’s `README.md`.

### 5.1 Taking a mod down (English)

The takedown list must be signed with the maintainer key. On the player's next launch, catalog refresh, import or profile apply, the manager verifies the signature and then removes matching mods from the library, from every profile, and from game-folder copies whose bytes it can identify, stating the reason in the log and a notice. It runs even for players who turned off "Check for mod updates on launch". A list that is unsigned, signed by another key, or older than the one already accepted is ignored, so a compromised mirror cannot use it to delete players' mods.

**One-time setup:**

```powershell
# The private key must live outside every git repository; the tool refuses otherwise and limits the file to the current user
.\tools\sign-removed.ps1 -NewKey -KeyFile D:\keys\mmm-removal-key.json
```

Put the printed `PublicKey:` into `PublicKey` in `src/MechabellumModManager/Services/RemovalListVerifier.cs` and ship that manager release. Until then no list takes effect. Back the private key up offline: losing it means a new key and a new release; leaking it lets anyone take mods down.

**Each takedown:**

1. Before taking it down, collect every `sha256` the mod was ever published with (run in your MechabellumMods clone):

   ```powershell
   $id = "opponent-economy"
   git log --format=%H -- catalog.json | ForEach-Object {
       ((git show "$($_):catalog.json") -join "`n" | ConvertFrom-Json).mods |
           Where-Object id -eq $id | ForEach-Object { $_.sha256 }
   } | Sort-Object -Unique
   ```

2. Move the entry out of `mods` in `catalog.json` and add it to `removed.json` at the MechabellumMods root (a JSON array that keeps every takedown):

   ```json
   [
     {
       "id": "opponent-economy",
       "name": "Opponent Economy",
       "sha256": ["<every hash from step 1>"],
       "reason": "影响对局公平，已下架",
       "since": "2026-10-14",
       "locales": { "en": { "reason": "Affects match fairness; removed" } }
     }
   ]
   ```

3. Sign it into the top-level `removedSigned` of `catalog.json` (the rest of the catalog is untouched):

   ```powershell
   .\tools\sign-removed.ps1 -ModsRepo <MechabellumMods clone> -KeyFile D:\keys\mmm-removal-key.json
   ```

   The script warns when the key does not match `RemovalListVerifier.PublicKey` in this checkout.

4. Run `validate_catalog.py`, commit `catalog.json` and `removed.json`, push and run `sync-mirror.ps1` as usual (`removedSigned` travels with `catalog.json`).

Rules:

- **Matching**: a library package whose catalog id equals `id`, or any of whose file hashes is in the `sha256` list. Names never match. A game-folder file is only deleted when its bytes are known (deploy record, the package's own hash, or a listed hash), so a player's own same-named DLL is kept.
- **Hash cleanup**: only 64-hex values count; the empty-file hash is ignored; hashes a mod still listed in `mods` ships are dropped with a log warning, so listing an old id's full history during a rename is safe.
- **Deferral**: while the game runs, a file is in use, a dual-folder store is unreachable, or the branch wizard or recovery gate is active, nothing is removed and it retries later; profile apply also never deploys a taken-down mod.
- **Signing time**: defaults to the current UTC time, independent of regional formats. The script refuses a time not later than the list already in `catalog.json`, a time more than 10 minutes in the future (it would make every list signed before then look stale), and an `-IssuedAt` not shaped like `2026-10-14T09:30:00Z`.
- **Pre-signing checks**: an `id` still in `mods`, a hash a listed mod still ships, the empty-file hash, a malformed hash, or a signed list over 1 MiB all stop the script without touching `catalog.json`.
- **Reinstating**: keep a taken-down `id` out of `mods`; if it is in both it stays taken down. To reinstate, remove it from `removed.json` and sign again.

Design: `docs/03-程序开发/历史规格/specs/2026-10-14-catalog-forced-removal-design.md`.

---

## 6. Local paths (at write time) (English)

- App / Setup: **v1.1.6** (current latest) → `release/v1.1.6/` relative to this repo
- Keep packaged artifacts under `release/vX.Y.Z/` (do not rely on personal install-test folders)

---

## 7. Manager update behavior (English)

- No silent auto-install  
- After **Check for updates**, the user downloads and runs the new Setup  
- Portable build requires .NET 8 Desktop Runtime on the machine  

---

## 8. Last step: push the repo pointer (English)

Only after the new Setup is confirmed downloadable. Otherwise the pointer tells every player a new
version exists while its `setupUrl` still 404s.

```powershell
# 1) Confirm setupUrl resolves (expect 200)
curl.exe -I -L "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z_Global.exe"

# 2) Copy release/vX.Y.Z/latest.json over release/latest.json
# 3) Commit and push it on its own
git add release/latest.json
git commit -m "release: point repo manifest at vX.Y.Z"
git push origin master
```

Verify `https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json` serves
the new version (raw has a few minutes of CDN cache), then **Check for updates** finds it.
