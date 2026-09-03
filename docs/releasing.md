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

管理器检查更新优先读 / Update check prefers：

`https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`

失败时回退 API / Fallback API：`/repos/llxlzx/MechabellumModManager/releases/latest`。

## 语言 / Language

- 中文：[发版前准备](#1-发版前准备-中文) · [本地出包](#2-本地出包-中文) · [创建 Release](#3-创建-github-release网页-中文) · [gh 备选](#4-命令行备选已装-gh-中文) · [Mod 大全](#5-mod-大全仓库与程序发版分开-中文) · [对照](#6-当前最新本地对照写作时-中文) · [更新行为](#7-管理器更新行为提醒用户-中文)
- English: [Prepare](#1-before-you-ship-english) · [Build](#2-build-locally-english) · [GitHub Release](#3-create-github-release-web-english) · [gh CLI](#4-cli-alternative-gh-english) · [Catalog](#5-mods-catalog-repo-separate-from-app-english) · [Local paths](#6-local-paths-at-write-time-english) · [Update behavior](#7-manager-update-behavior-english)

---

## 1. 发版前准备 (中文)

### 1.1 改版本号（三处一致）

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>X.Y.Z</Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "X.Y.Z"`
3. 稍后写入的 `release/vX.Y.Z/latest.json` → `"version": "X.Y.Z"`

### 1.2 MelonLoader 离线包（打 Setup **必选**）

将官方文件放到：

`installer/redist/melonloader/MelonLoader.x64.zip`

来源：https://github.com/LavaGang/MelonLoader/releases  

缺失时 `build-installer` **硬失败**（退出码 3）。正式发版禁止 `-SkipMelonRedistCheck`。

### 1.3 代码与测试

```powershell
cd <path-to-your-MechabellumModManager-clone>
dotnet test -c Release
git add -A   # 勿提交 Melon zip / publish / dist
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
  "version": "1.0.5",
  "notes": "本版更新说明……",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.0.5/MechabellumModManager_Setup_v1.0.5.exe",
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

- 程序 / Setup：**v1.0.5**（当前最新）→ 仓库内相对路径 `release/v1.0.5/`
- 安装包与本体产物均整理在上述 `release/vX.Y.Z/` 目录（勿依赖本机个人测试文件夹路径）

---

## 7. 管理器更新行为（提醒用户） (中文)

- 不会静默自动安装  
- 「检查更新」提示后，用户自行下载并运行新 Setup  
- 便携「本体」需本机已装 .NET 8 Desktop Runtime  

---

## 1. Before you ship (English)

### 1.1 Bump version (three places, same number)

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>X.Y.Z</Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "X.Y.Z"`
3. `release/vX.Y.Z/latest.json` → `"version": "X.Y.Z"`

### 1.2 MelonLoader offline zip (required for Setup)

Place official file at:

`installer/redist/melonloader/MelonLoader.x64.zip`

From: https://github.com/LavaGang/MelonLoader/releases  

Missing file → `build-installer` **hard-fails** (exit code 3). Do not use `-SkipMelonRedistCheck` for real releases.

### 1.3 Code and tests

```powershell
cd <path-to-your-MechabellumModManager-clone>
dotnet test -c Release
git add -A   # do not commit Melon zip / publish / dist
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

- App / Setup: **v1.0.5** (current latest) → `release/v1.0.5/` relative to this repo
- Keep packaged artifacts under `release/vX.Y.Z/` (do not rely on personal install-test folders)

---

## 7. Manager update behavior (English)

- No silent auto-install  
- After **Check for updates**, the user downloads and runs the new Setup  
- Portable build requires .NET 8 Desktop Runtime on the machine  
