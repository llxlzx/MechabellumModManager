# 钢铁指挥官 Mod 管理器 — GitHub Release 更新说明

Mechabellum Mod Manager — GitHub Release guide

面向维护者：如何打新版本安装包，并发布到 GitHub Releases。  
For maintainers: how to build the Setup and publish a GitHub Release.

更完整的技术细节见同目录 `releasing.md`。  
More technical detail: `releasing.md` in this folder.

## 语言 / Language

**请向下滚动本页**，可找到另一种语言的完整发版说明。

**Scroll down this page** to find the full release guide in the other language.

- 中文：[两个仓库](#一两个仓库分别干什么中文) · [标准发版步骤](#三发管理器新版本标准步骤中文) · [latest.json](#四latestjson-怎么写中文) · [FAQ](#八常见问题中文)
- English: [Two repos](#1-what-the-two-repos-are-for-english) · [Release steps](#3-shipping-a-new-manager-version-english) · [latest.json](#4-latestjson-english) · [FAQ](#8-faq-english)

---

## 一、两个仓库分别干什么·中文

> English → [§1 Two repos](#1-what-the-two-repos-are-for-english)

| 仓库 | 地址 | 发什么 |
|------|------|--------|
| **管理器** | https://github.com/llxlzx/MechabellumModManager | Setup 安装包、便携本体、latest.json |
| **Mod 大全** | https://github.com/llxlzx/MechabellumMods | catalog.json、各 Mod 的 dll / 预览图 |

玩家「检查更新」只看**管理器**仓库的 Release。  
「Mod 浏览 → 刷新目录」只看 **Mods** 仓库的 `catalog.json`。两者发版可以不同步。

---

## 二、本地文件怎么分（安装包 vs 本体）·中文

> English → [§2 Layout](#2-local-layout-setup-vs-portable-english)

在管理器仓库的 `release/v版本号/` 下：

```
release/v1.0.4/
  安装包/     → 给绝大多数用户（Setup.exe）
  本体/       → 便携运行（exe + Assets，无 Mod 数据）
  latest.json → 给「检查更新」用
  MechabellumModManager_portable_v1.0.4.zip  → 把「本体」打成的 zip，上传 Release
```

| | **安装包** | **本体（便携）** |
|--|------------|------------------|
| 文件 | `MechabellumModManager_Setup_vX.Y.Z.exe` | `MechabellumModManager.exe` + `Assets\` |
| 适合 | 普通用户一键安装 | 已装 .NET 8、想免安装运行 |
| 含 Melon 离线包 | 是（打在 Setup 里） | 否 |
| 含本地 Mod / 方案 | 否 | 否 |

**禁止**把「Setup 装完后的文件夹」整包当本体上传。  
那种目录里常有 `unins000.*`、`installer-redist\`、`installer-scripts\`，不属于便携本体。  
干净本体请用：`release/vX.Y.Z/本体\` 或构建产生的 `publish\`。

---

## 三、发管理器新版本：标准步骤·中文

> English → [§3 Shipping](#3-shipping-a-new-manager-version-english)

以发布 **v1.0.4** 为例（以后把版本号换成新的即可）。

### 步骤 1 — 改版本号

同时改这三处，数字必须一致：

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>1.0.4</Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "1.0.4"`
3. 稍后的 `latest.json` → `"version": "1.0.4"`

### 步骤 2 — 准备 MelonLoader 离线包（必做）

把官方 `MelonLoader.x64.zip` 放到：

`installer/redist/melonloader/MelonLoader.x64.zip`

下载：https://github.com/LavaGang/MelonLoader/releases  

没有这个文件时，构建脚本会**直接失败**，不允许打正式包。

### 步骤 3 — 测试并推代码

```powershell
cd D:\gongzuo\钢铁指挥官mod管理器开发
dotnet test -c Release
git add ...
git commit -m "说明本版改动"
git push origin master
```

连不上 GitHub 时可先：

```powershell
$env:HTTPS_PROXY='http://127.0.0.1:7890'
$env:HTTP_PROXY='http://127.0.0.1:7890'
```

### 步骤 4 — 打安装包

```powershell
.\installer\build-installer.ps1
```

得到：`dist\MechabellumModManager_Setup_v1.0.4.exe`  
（体积大约二十多 MB 才正常，因为内嵌了 Melon。）

把产物整理进 `release/v1.0.4/安装包/` 与 `release/v1.0.4/本体/`，并写好 `latest.json`。

安装向导应依次出现：**选择目标位置**（管理器安装目录）→ **选择游戏目录**。若只看到游戏目录页，请确认已使用含 `DisableDirPage=no` 的 Setup。

### 步骤 5 — 打本体 zip

```powershell
cd release\v1.0.4
Compress-Archive -Path ".\本体\*" -DestinationPath ".\MechabellumModManager_portable_v1.0.4.zip" -Force
```

解压后应直接看到 exe 和 Assets，而不是多一层无关目录。

### 步骤 6 — 在 GitHub 上建 Release

1. 打开：https://github.com/llxlzx/MechabellumModManager/releases  
2. **Draft a new release**  
3. **Tag**：输入 `v1.0.4` → Create new tag；**Target** 选 `master`  
4. **Title**：`v1.0.4`  
5. **说明**：写本版更新点（可与 latest.json 的 notes 相同；可中英双语）  
6. **上传附件**（拖进虚线框）：

| 必传 / 推荐 | 文件 |
|-------------|------|
| 必传 | `安装包\MechabellumModManager_Setup_v1.0.4.exe` |
| 必传 | `latest.json`（**文件名不能改**） |
| 推荐 | `MechabellumModManager_portable_v1.0.4.zip` |

7. 勾选 **Set as the latest release**  
8. **不要**勾选 Pre-release  
9. **Publish release**

### 步骤 7 — 自检

- 打开：https://github.com/llxlzx/MechabellumModManager/releases/tag/v1.0.4  
  确认三个资源都在  
- 打开：  
  `https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`  
  能显示 JSON  
- 打开管理器 → **设置 → 检查更新**，应提示有新版本（若本机还是旧版）

---

## 四、latest.json 怎么写·中文

> English → [§4](#4-latestjson-english)

```json
{
  "version": "1.0.4",
  "notes": "本版更新说明，可多行。",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.0.4/MechabellumModManager_Setup_v1.0.4.exe",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

| 字段 | 含义 |
|------|------|
| version | 与安装包版本一致 |
| notes | 检查更新时展示的说明 |
| setupUrl | Setup 下载直链 |
| publishedAt | 发布时间（可选） |

上传到 Release 时，附件名必须是 **`latest.json`**，否则「检查更新」优先地址会失败。

---

## 五、只更新 Mod 大全时（不发管理器新版本）·中文

```powershell
cd D:\gongzuo\MechabellumMods
# 改 mods/、catalog.json、preview.png 等
git add -A
git commit -m "说明"
git push origin master
```

确认：  
https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json  

玩家在管理器里点 **Mod 浏览 → 刷新目录** 即可，**不必**重发 Setup。

作者提交方式见：`MechabellumMods` 仓库内的 `README.md`（英汉双语新手教程）。

---

## 六、v1.0.4 本版要点·中文

- **取消 Cloudflare 中继**：投稿 / 举报不再走 Worker，客户端直连 GitHub。
- **投稿**：应用内引导 Fork + 更新 `catalog.json` + Pull Request 到 `MechabellumMods`。
- **举报**：打开 GitHub Issues 预填页，并把 Issue 链接复制到剪贴板；用户登录后点 Submit 才真正提交。
- **MechabellumMods 侧**：Issue 模板 + `catalog.json` CI 校验，便于审稿与防坏目录。
- **安装包**：`DisableDirPage=no`，重装时也会显示管理器安装目录页。
- **界面**：顶栏双行布局，窄窗口按钮可换行。

---

## 七、当前本地对照（写作时）·中文

| 项 | 位置 |
|----|------|
| 最新整理目录 | `release/v1.0.4/` |
| 测试用 Setup 副本 | `D:\gongzuo\钢铁指挥官Mod管理器_安装包测试\MechabellumModManager_Setup_v1.0.4.exe` |
| 流程详版 | `docs/releasing.md` |
| 本说明 | `docs/GitHub-Release更新说明.md`（本文件，英汉双语） |

---

## 八、常见问题·中文

**Q：安装测试目录里的「钢铁指挥官Mod管理器」能整包当本体吗？**  
A：不能。去掉卸载器和 installer 目录后，理论上只留 exe+Assets 可以，但请优先用 `release/vX.Y.Z/本体\`。

**Q：Push / 检查更新失败，网页却能开 GitHub？**  
A：浏览器走了代理，Git/管理器可能没走。给终端设 `HTTPS_PROXY=http://127.0.0.1:7890`（端口按你的代理软件为准）。

**Q：Mod 浏览刷新失败？**  
A：检查 Mods 仓库是否公开、`catalog.json` 是否在 `master` 分支根目录，以及本机能否访问 raw.githubusercontent.com。

---

## 1. What the two repos are for · English

> Chinese → [§一](#一两个仓库分别干什么中文)

| Repo | URL | Publishes |
|------|-----|-----------|
| **Manager** | https://github.com/llxlzx/MechabellumModManager | Setup, portable zip, `latest.json` |
| **Mods catalog** | https://github.com/llxlzx/MechabellumMods | `catalog.json`, mod DLLs / previews |

「Check for updates」 only reads the **manager** Releases.  
「Browse mods → Refresh」 only reads Mods `catalog.json`. The two can ship on different schedules.

---

## 2. Local layout (Setup vs portable) · English

> Chinese → [§二](#二本地文件怎么分安装包-vs-本体中文)

Under `release/vVERSION/` in the manager repo:

```
release/v1.0.4/
  安装包/     → Setup for most users
  本体/       → portable (exe + Assets, no mod data)
  latest.json → used by in-app update check
  MechabellumModManager_portable_v1.0.4.zip  → zip of 本体/, attach to Release
```

| | **Setup** | **Portable** |
|--|-----------|--------------|
| Files | `MechabellumModManager_Setup_vX.Y.Z.exe` | `MechabellumModManager.exe` + `Assets\` |
| Audience | One-click install | Users with .NET 8 who want no installer |
| Melon offline zip | Embedded in Setup | No |
| Local mods / profiles | No | No |

**Do not** zip a post-Setup install folder as portable (`unins000.*`, `installer-redist\`, etc.).  
Use `release/vX.Y.Z/本体\` or `publish\`.

---

## 3. Shipping a new manager version · English

> Chinese → [§三](#三发管理器新版本标准步骤中文)

Example: **v1.0.4** (replace the version everywhere for later releases).

### Step 1 — Bump version (three places, same number)

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>1.0.4</Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "1.0.4"`
3. `latest.json` → `"version": "1.0.4"`

### Step 2 — MelonLoader offline zip (required)

Place official `MelonLoader.x64.zip` at:

`installer/redist/melonloader/MelonLoader.x64.zip`

From: https://github.com/LavaGang/MelonLoader/releases  

Without it, the build script **fails** and will not produce a release Setup.

### Step 3 — Test and push code

```powershell
cd D:\gongzuo\钢铁指挥官mod管理器开发
dotnet test -c Release
git add ...
git commit -m "Describe this release"
git push origin master
```

If GitHub is unreachable from the CLI:

```powershell
$env:HTTPS_PROXY='http://127.0.0.1:7890'
$env:HTTP_PROXY='http://127.0.0.1:7890'
```

### Step 4 — Build the installer

```powershell
.\installer\build-installer.ps1
```

Output: `dist\MechabellumModManager_Setup_v1.0.4.exe`  
(~20+ MB is normal; Melon is embedded.)

Copy into `release/v1.0.4/安装包/` and `release/v1.0.4/本体/`, and write `latest.json`.

Wizard order should be: **Select destination** (manager install dir) → **Select game folder**. If the first page is missing, rebuild with `DisableDirPage=no`.

### Step 5 — Zip portable

```powershell
cd release\v1.0.4
Compress-Archive -Path ".\本体\*" -DestinationPath ".\MechabellumModManager_portable_v1.0.4.zip" -Force
```

Extracted zip should show `exe` + `Assets` at the top level (no extra junk folder).

### Step 6 — Create the GitHub Release

1. Open https://github.com/llxlzx/MechabellumModManager/releases  
2. **Draft a new release**  
3. **Tag:** `v1.0.4` → create tag; **Target:** `master`  
4. **Title:** `v1.0.4`  
5. **Description:** release notes (can match `latest.json` notes; bilingual OK)  
6. **Attach:**

| Required / recommended | File |
|------------------------|------|
| Required | `安装包\MechabellumModManager_Setup_v1.0.4.exe` |
| Required | `latest.json` (**exact filename**) |
| Recommended | `MechabellumModManager_portable_v1.0.4.zip` |

7. Check **Set as the latest release**  
8. Do **not** check Pre-release  
9. **Publish release**

### Step 7 — Verify

- https://github.com/llxlzx/MechabellumModManager/releases/tag/v1.0.4 — all assets present  
- https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json — valid JSON  
- Manager → **Settings → Check for updates** should offer the new version if the installed app is older

---

## 4. latest.json · English

> Chinese → [§四](#四latestjson-怎么写中文)

```json
{
  "version": "1.0.4",
  "notes": "Release notes (can be multi-line).",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.0.4/MechabellumModManager_Setup_v1.0.4.exe",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

| Field | Meaning |
|-------|---------|
| version | Must match Setup version |
| notes | Shown in “Check for updates” |
| setupUrl | Direct Setup download URL |
| publishedAt | Optional timestamp |

The Release attachment **must** be named `latest.json`.

---

## 5. Catalog-only updates (no new manager) · English

```powershell
cd D:\gongzuo\MechabellumMods
# edit mods/, catalog.json, previews, …
git add -A
git commit -m "Describe catalog change"
git push origin master
```

Confirm:  
https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json  

Players only need **Browse mods → Refresh catalog**. No new Setup required.

Author flow: bilingual beginner guide in the MechabellumMods `README.md`.

---

## 6. v1.0.4 highlights · English

- **Cloudflare relay removed**: submit/report talk to GitHub directly.
- **Submit**: in-app guide → Fork + `catalog.json` + Pull Request on MechabellumMods.
- **Report**: pre-filled Issue URL + clipboard copy; user must sign in and Submit on GitHub.
- **MechabellumMods**: Issue template + `catalog.json` CI validation.
- **Installer**: `DisableDirPage=no` so the manager install-dir page always shows.
- **UI**: two-row header; action buttons wrap on narrow windows.

---

## 7. Local paths (at write time) · English

| Item | Path |
|------|------|
| Packaged folder | `release/v1.0.4/` |
| Test Setup copy | `D:\gongzuo\钢铁指挥官Mod管理器_安装包测试\MechabellumModManager_Setup_v1.0.4.exe` |
| Longer tech notes | `docs/releasing.md` |
| This guide | `docs/GitHub-Release更新说明.md` (bilingual) |

---

## 8. FAQ · English

**Q: Can I upload a full post-Setup install folder as portable?**  
A: No. Prefer `release/vX.Y.Z/本体\` (exe + Assets only).

**Q: Push / update check fails but the browser opens GitHub?**  
A: Browser uses a proxy; Git/CLI may not. Set `HTTPS_PROXY=http://127.0.0.1:7890` (port per your proxy app).

**Q: Browse mods refresh fails?**  
A: Repo must be public, `catalog.json` on `master` root, and the machine must reach raw.githubusercontent.com.
