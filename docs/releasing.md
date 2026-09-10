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

自 **v1.2.1** 起仓库根目录的 `release/latest.json` 是发版指针，**每次发版都要更新它**。它只负责「宣告」，`setupUrl` 仍必须指向可访问的 https（Release 资产，或 `sync-mirror.ps1 -IncludeSetup` 镜像出来的地址）。

> **⚠️ 指针必须最后推送。** 它一进 master 就会被所有玩家读到，若此时 `setupUrl` 指向的 Release 资产还不存在，玩家会被告知有新版本却下载 404。所以仓库里的 `release/latest.json` 始终指向**当前已经能下载**的那一版；发新版时把它留到最后一步（见 [第 8 节](#8-最后一步推送仓库指针-中文)）再改。

## 语言 / Language

- 中文：[发版前准备](#1-发版前准备-中文) · [本地出包](#2-本地出包-中文) · [创建 Release](#3-创建-github-release网页-中文) · [gh 备选](#4-命令行备选已装-gh-中文) · [Mod 大全](#5-mod-大全仓库与程序发版分开-中文) · [对照](#6-当前最新本地对照写作时-中文) · [更新行为](#7-管理器更新行为提醒用户-中文) · [推送仓库指针](#8-最后一步推送仓库指针-中文)
- English: [Prepare](#1-before-you-ship-english) · [Build](#2-build-locally-english) · [GitHub Release](#3-create-github-release-web-english) · [gh CLI](#4-cli-alternative-gh-english) · [Catalog](#5-mods-catalog-repo-separate-from-app-english) · [Local paths](#6-local-paths-at-write-time-english) · [Update behavior](#7-manager-update-behavior-english) · [Repo pointer](#8-last-step-push-the-repo-pointer-english)

---

## 1. 发版前准备 (中文)

### 1.1 改版本号（三处一致）

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>X.Y.Z</Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "X.Y.Z"`
3. 稍后写入的 `release/vX.Y.Z/latest.json` → `"version": "X.Y.Z"`

此时**不要**动 `release/latest.json`。它是对外生效的指针，留到 [第 8 节](#8-最后一步推送仓库指针-中文)。

### 1.2 运行时离线包（镜像暂存，**不**再打进 Setup）

自 **v1.2.0** 起 Setup 为瘦包。将下列文件放到 `installer/redist\`，供 `tools/sync-mirror.ps1` 上传到 `MechabellumRedist/`（**默认同步**；调试可用 `-SkipRedist`）：

| 路径 | 说明 |
|------|------|
| `melonloader/MelonLoader.x64.zip` | Melon 0.7.3 钉死版 |
| `unity-deps/UnityDependencies_2022.3.62.zip` | Unity 运行时依赖 |
| `cpp2il/Cpp2IL.exe` + plugin dll | 与 Melon 0.7.3 匹配 |
| `dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` | .NET 8 Desktop |

清单模板：`src/MechabellumModManager/Assets/redist-manifest.json`。  
发版顺序：**先 sync-mirror（含 redist）→ verify-mirror → 再打瘦 Setup / 发 GitHub Release**。

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
.\installer\build-installer.ps1
```

产物：`dist\MechabellumModManager_Setup_vX.Y.Z.exe`（应明显大于仅程序体积，因内嵌 Melon zip）。

整理到仓库对照目录：

```
release/vX.Y.Z/
  安装包/
    MechabellumModManager_Setup_vX.Y.Z.exe
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

`latest.json` 示例：

```json
{
  "version": "1.1.6",
  "notes": "本版更新说明……",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.1.6/MechabellumModManager_Setup_v1.1.6.exe",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

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
| 安装包（必传） | `release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z.exe` | 保持原名 |
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
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z.exe" `
  "release/vX.Y.Z/latest.json" `
  "release/vX.Y.Z/MechabellumModManager_portable_vX.Y.Z.zip" `
  --title "vX.Y.Z" `
  --notes-file - `
  --target master
```

若 Release 已存在，只更新附件：

```powershell
gh release upload vX.Y.Z `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z.exe" `
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
curl.exe -I -L "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z.exe"

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
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "X.Y.Z"`
3. `release/vX.Y.Z/latest.json` → `"version": "X.Y.Z"`

Leave `release/latest.json` alone here. It is the pointer players actually read, so it always names the
version that is already downloadable, and it is updated last — see [§8](#8-last-step-push-the-repo-pointer-english).

### 1.2 MelonLoader offline zip (required for Setup)

Place official file at:

`installer/redist/melonloader/MelonLoader.x64.zip`

From: https://github.com/LavaGang/MelonLoader/releases  

Missing file → `build-installer` **hard-fails** (exit code 3). Do not use `-SkipMelonRedistCheck` for real releases.

### 1.2b Unity Il2Cpp deps + .NET 8 offline (required for China-offline fat Setup)

In addition to the Melon zip, release fat Setup requires:

| Path | Source | Notes |
|------|--------|-------|
| `installer/redist/unity-deps/UnityDependencies_{major.minor.patch}.zip` | https://github.com/LavaGang/Unity-Runtime-Libraries | Upstream names like `2022.3.62.zip` **must be renamed** to `UnityDependencies_2022.3.62.zip` under `unity-deps/` |
| `installer/redist/dotnet8/windowsdesktop-runtime-8.*-win-x64.exe` | https://dotnet.microsoft.com/download/dotnet/8.0 | Windows x64 Desktop Runtime offline installer |

Resolve the game Unity version from `Mechabellum_Data\globalgamemanagers` (e.g. `2022.3.62f3` → `UnityDependencies_2022.3.62.zip`). Refresh `unity-deps` and ship a new Setup when Mechabellum’s Unity patch bumps.

**Domestic Setup mirror:** Downloading Setup from GitHub Release may still need VPN or a third-party mirror. The fat Setup goal is **post-install** offline first-run Il2Cpp generation (success level **B**) after Melon is written and UnityDependencies is seeded. Mirror distribution is ops/docs only, not in-app.

**Residual probe before calling B done (Cpp2IL, etc.):** On a real machine, run acceptance **F** in `docs/superpowers/specs/2026-09-05-china-offline-melon-fat-setup-design.md`: disconnect network, launch the game once. If Melon still asks for another missing package under `{game}\MelonLoader\Dependencies\Il2CppAssemblyGenerator\` (often Cpp2IL-related), add it to `installer/redist/`, extend the `build-installer` gate, rebuild Setup, then re-run acceptance before claiming B complete.

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
.\installer\build-installer.ps1
```

Output: `dist\MechabellumModManager_Setup_vX.Y.Z.exe` (should be clearly larger than app-only; Melon zip is embedded).

Layout under the repo:

```
release/vX.Y.Z/
  安装包/
    MechabellumModManager_Setup_vX.Y.Z.exe
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
| Setup (required) | `release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z.exe` | keep filename |
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
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z.exe" `
  "release/vX.Y.Z/latest.json" `
  "release/vX.Y.Z/MechabellumModManager_portable_vX.Y.Z.zip" `
  --title "vX.Y.Z" `
  --notes-file - `
  --target master
```

Update assets on an existing Release:

```powershell
gh release upload vX.Y.Z `
  "release/vX.Y.Z/安装包/MechabellumModManager_Setup_vX.Y.Z.exe" `
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
curl.exe -I -L "https://github.com/llxlzx/MechabellumModManager/releases/download/vX.Y.Z/MechabellumModManager_Setup_vX.Y.Z.exe"

# 2) Copy release/vX.Y.Z/latest.json over release/latest.json
# 3) Commit and push it on its own
git add release/latest.json
git commit -m "release: point repo manifest at vX.Y.Z"
git push origin master
```

Verify `https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json` serves
the new version (raw has a few minutes of CDN cache), then **Check for updates** finds it.
