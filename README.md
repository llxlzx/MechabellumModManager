# Mechabellum（钢铁指挥官）Mod 管理器

Windows 桌面工具：外部 Mod 库 + 方案勾选，一键同步到游戏目录。

Windows desktop tool: external mod library + profiles, one-click sync into the game folder.

## 语言 / Language

**请向下滚动本页**，可找到另一种语言的完整说明。

**Scroll down this page** to find the full documentation in the other language.

- 中文：[功能概览（中文）](#功能概览中文) · [最终用户（中文）](#最终用户中文) · [Mod 作者投稿（中文）](#mod-作者投稿中文) · [开发与发版（中文）](#开发与发版中文) · [风险声明（中文）](#风险声明中文)
- English: [Features (EN)](#features-english) · [End users (EN)](#end-users-english) · [Authors (EN)](#mod-authors--submit-english) · [Developers (EN)](#developers--release-english) · [Risks (EN)](#risk-notice-english)

| 仓库 / Repo | 用途 / Purpose |
|-------------|----------------|
| **本仓库** [llxlzx/MechabellumModManager](https://github.com/llxlzx/MechabellumModManager) | 管理器程序、安装包、更新检查 |
| [llxlzx/MechabellumMods](https://github.com/llxlzx/MechabellumMods) | 社区 Mod 目录（`catalog.json` + DLL） |

最新安装包请到本仓库 **[Releases](https://github.com/llxlzx/MechabellumModManager/releases/latest)** 下载。

---

## 功能概览·中文

> 英文版请**向下滚动** → [Features (English)](#features-english)

- **外部 Mod 库**：DLL / Zip / 文件夹导入；可从游戏目录扫描导入。
- **方案（Profile）**：多套勾选组合，一键「应用方案」扁平同步到游戏的 `Mods` / `Plugins` / `UserLibs` / `UserData`。
- **Mod 浏览**：从 [MechabellumMods](https://github.com/llxlzx/MechabellumMods) 拉取目录，「加入本地库」（**不会**自动启用）。
- **投稿 / 举报**：打开 GitHub 新手教程（Fork + PR）或预填 Issue；**不经过** Cloudflare 中转。
- **多语言**：简体中文 / English / 日本語 / Deutsch / Русский（可跟随系统）。
- **安装包**：可选安装 .NET 8（管理器）、.NET 6（MelonLoader）、MelonLoader；优先使用 `installer/redist/` 离线包。

---

## 最终用户·中文

> 更细的安装步骤也可参见 `docs/分发-使用说明.txt`。英文 → [End users (EN)](#end-users-english)

### 系统要求

- Windows 10 / 11 **64 位**
- Steam 版 *Mechabellum*（钢铁指挥官）
- 建议使用本仓库提供的 **Setup 安装包**（管理员运行）

### 安装

1. 下载并运行 `MechabellumModManager_Setup_v*.exe`（或中文文件名的同内容安装包），**以管理员身份**运行。  
2. **选择管理器安装位置**（程序装到哪里）。  
3. **选择游戏目录**：需包含 `Mechabellum.exe` 与 `GameAssembly.dll`（可自动探测 Steam 路径）。  
4. 组件建议：
   - **.NET 8 Desktop Runtime** — 运行管理器  
   - **.NET 6 Desktop Runtime** — MelonLoader 常用依赖  
   - **MelonLoader** — 若游戏里还没有 Loader（已安装会跳过）  
5. 完成安装后从开始菜单启动管理器。

离线环境：制作 Setup 时把官方 Runtime / MelonLoader zip 放进 `installer/redist/`（见该目录说明），再编译安装包。

### 日常使用

1. **导入 Mod**：`导入 DLL` / `导入 Zip` / `导入文件夹`，或「从游戏导入」。  
2. 在左侧选中一个**方案**，勾选要启用的 Mod。  
3. 点 **应用方案**（只同步）或 **应用并启动**（同步后启动游戏）。  
4. 可选：打开 **Mod 浏览** → **刷新目录** → 对条目 **加入本地库**，再回方案里勾选启用。  

数据默认在 `%AppData%\MechabellumModManager\`（本地库与配置；安装包**不会**自带第三方 Mod）。

高风险 Mod 启用前会要求确认。状态栏与同步日志可查看是否与游戏目录一致。

### 检查更新

设置里点 **检查更新**：读取 GitHub Release 上的 `latest.json`，**不会**静默自动安装；需你确认后再打开下载链接。

---

## Mod 作者投稿·中文

投稿方式保持 **GitHub Fork + Pull Request**（网页即可，无需安装 Git）。

1. 在管理器点 **投稿 Mod**，阅读弹窗步骤后打开教程。  
2. 按社区仓库 README 操作：  
   **[新手投稿教程（中文）](https://github.com/llxlzx/MechabellumMods#新手投稿教程网页操作无需安装-git中文)**  
3. 简要流程：Fork → 上传到 `mods/你的id/xxx.dll` → 编辑 `catalog.json` → 提 Pull Request → 等待合并。  

举报：对目录或本地库中的 Mod 点 **举报**，选择分类后打开预填 GitHub Issue（请登录后点 Submit）。

英文作者请看社区仓库的 [English guide](https://github.com/llxlzx/MechabellumMods#beginner-submit-guide-web-only-no-git--english)，或本页下方英文「Authors」一节。

---

## 开发与发版·中文

> 英文 → [Developers (EN)](#developers--release-english)

### 构建安装包

开发机需安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)。正式包**必须**准备 MelonLoader 离线 zip：

`installer/redist/melonloader/MelonLoader.x64.zip`

```powershell
.\installer\build-installer.ps1
# 或
.\installer\build-installer.bat
```

产物：`dist\MechabellumModManager_Setup_v*.exe`。整理进 `release/v版本号/` 的方式见 [docs/GitHub-Release更新说明.md](docs/GitHub-Release更新说明.md)。

### 开发命令

```powershell
dotnet build
dotnet test
dotnet publish src\MechabellumModManager -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

最终用户请使用 **Setup** 或开始菜单快捷方式，不要依赖仓库根目录下临时 bat / 便捷 exe 副本。

维护者发版流程、附件清单（Setup + `latest.json` + portable zip）见上述 GitHub Release 说明文档。

---

## 风险声明·中文

- 使用第三方 Mod 可能导致**封号、存档损坏、游戏或系统异常**等，风险自负。  
- 高风险包启用需二次确认；管理器提供的本地检查**不是**完整杀毒。  
- 本管理器**不含**作弊入口；**不再内置**一键安装 MelonLoader（改由安装包处理）。  
- 投稿与目录内容由提供者负责；维护者可拒绝上架或下架，但无法保证每一份文件的安全性。  
- 详细免责声明见应用内「关于与声明」。

---

## Features · English

> Chinese version is **above** — [功能概览（中文）](#功能概览中文)

- **External mod library**: import DLL / Zip / folder; optional import from the game folder.
- **Profiles**: multiple enable-sets; **Apply profile** flattens files into the game `Mods` / `Plugins` / `UserLibs` / `UserData` folders.
- **Browse mods**: fetch [MechabellumMods](https://github.com/llxlzx/MechabellumMods) catalog; **Add to library** (does **not** auto-enable).
- **Submit / report**: opens the GitHub beginner guide (Fork + PR) or a pre-filled Issue — **no** Cloudflare relay.
- **Languages**: zh-CN / en / ja / de / ru (or follow the OS).
- **Installer**: optional .NET 8 (app), .NET 6 (MelonLoader), MelonLoader; prefers offline files under `installer/redist/`.

---

## End users · English

> Also see `docs/分发-使用说明.txt` (Chinese). Jump to [最终用户（中文）](#最终用户中文) if needed.

### Requirements

- Windows 10 / 11 **x64**
- Steam *Mechabellum*
- Prefer the **Setup** installer from [Releases](https://github.com/llxlzx/MechabellumModManager/releases/latest) (run as Administrator)

### Install

1. Run `MechabellumModManager_Setup_v*.exe` as **Administrator**.  
2. Choose **where to install the manager**.  
3. Choose the **game folder** (must contain `Mechabellum.exe` and `GameAssembly.dll`; Steam path can be auto-detected).  
4. Components (recommended defaults):
   - **.NET 8 Desktop Runtime** — manager  
   - **.NET 6 Desktop Runtime** — common MelonLoader dependency  
   - **MelonLoader** — if not already installed in the game (skipped when present)  
5. Launch from the Start Menu when finished.

Offline builds: place official Runtime / MelonLoader zips into `installer/redist/` before compiling Setup.

### Daily use

1. **Import** mods (DLL / Zip / folder) or **Import from game**.  
2. Select a **profile**, tick the mods to enable.  
3. **Apply profile** or **Apply and launch**.  
4. Optional: **Browse mods** → **Refresh catalog** → **Add to library**, then enable in a profile.

Data defaults to `%AppData%\MechabellumModManager\` (empty library; Setup does **not** ship third-party mods).

High-risk mods require confirmation. Use the status bar and sync log to verify deployment.

### Updates

**Check for updates** reads `latest.json` from GitHub Releases. There is **no** silent auto-install; you confirm before opening the download link.

---

## Mod authors / submit · English

Submission stays **GitHub Fork + Pull Request** (browser only; Git CLI optional).

1. In the manager, click **Submit Mod**, read the steps, open the guide.  
2. Follow the community README:  
   **[Beginner submit guide (English)](https://github.com/llxlzx/MechabellumMods#beginner-submit-guide-web-only-no-git--english)**  
3. Summary: Fork → upload `mods/your-id/xxx.dll` → edit `catalog.json` → open a Pull Request → wait for merge.

**Report**: pick a category; the manager opens a pre-filled GitHub Issue (sign in and click Submit).

Chinese authors: [中文投稿教程](https://github.com/llxlzx/MechabellumMods#新手投稿教程网页操作无需安装-git中文).

---

## Developers / release · English

> Chinese → [开发与发版（中文）](#开发与发版中文)

### Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php). Release builds **require**:

`installer/redist/melonloader/MelonLoader.x64.zip`

```powershell
.\installer\build-installer.ps1
```

Output: `dist\MechabellumModManager_Setup_v*.exe`. Packaging layout: [docs/GitHub-Release更新说明.md](docs/GitHub-Release更新说明.md) (Chinese maintainer doc).

### Dev commands

```powershell
dotnet build
dotnet test
dotnet publish src\MechabellumModManager -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

End users should run the **Setup** or Start Menu shortcut—not ad-hoc bat/exe copies in the repo root.

Release checklist: upload Setup + `latest.json` + portable zip; see the Release doc above.

---

## Risk notice · English

- Third-party mods may cause **bans, save corruption, or instability**—use at your own risk.  
- High-risk packages require confirmation; local checks are **not** a full antivirus.  
- This manager has **no** cheat entry points and **no** in-app one-click MelonLoader install (installer handles Melon).  
- Catalog / submissions are the providers’ responsibility; maintainers may reject or remove entries without guaranteeing every file’s safety.  
- Full disclaimer: in-app **About / Credits**.
