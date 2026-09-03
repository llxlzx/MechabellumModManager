# Mechabellum（钢铁指挥官）Mod 管理器

Windows 桌面工具：外部 Mod 库 + 方案勾选，一键同步到游戏目录。

Windows desktop tool: external mod library + profiles, one-click sync into the game folder.

> **⚠️ AI 生成声明 / AI-generated notice**  
> **本管理器程序本体，以及仓库中的说明文档与中英文等内容，均主要由 AI 辅助开发、撰写与翻译。** 可能存在功能偏差、语义偏差、表述不严谨或逻辑疏漏。请以实际软件行为与应用内说明为准；如有疑问，请通过应用内「关于与声明」中的联系方式联系维护者。  
> **This Mod Manager application, as well as repository documentation and translations (Chinese, English, and other languages), were largely produced with AI assistance.** They may contain functional gaps, semantic inaccuracies, awkward wording, or logic issues. Prefer the running application and in-app notices; contact the maintainer via in-app Credits if needed.

## 语言 / Language

使用下方链接在本页各节间跳转（中英文内容均在同一 README 中）。

Use the links below to jump within this page (Chinese and English sections share one README).

- 中文：[功能概览](#功能概览-中文) · [最终用户](#最终用户-中文) · [Mod 作者投稿](#mod-作者投稿-中文) · [开发与发版](#开发与发版-中文) · [风险声明](#风险声明-中文)
- English: [Features](#features-english) · [End users](#end-users-english) · [Authors](#mod-authors-submit-english) · [Developers](#developers-release-english) · [Risks](#risk-notice-english)

| 仓库 / Repo | 用途 / Purpose |
|-------------|----------------|
| **本仓库** [llxlzx/MechabellumModManager](https://github.com/llxlzx/MechabellumModManager) | 管理器程序、安装包、更新检查 |
| [llxlzx/MechabellumMods](https://github.com/llxlzx/MechabellumMods) | 社区 Mod 目录（`catalog.json` + DLL） |

最新安装包请到本仓库 **[Releases](https://github.com/llxlzx/MechabellumModManager/releases/latest)** 下载。

---

## 功能概览 (中文)

- **外部 Mod 库**：DLL / Zip / 文件夹导入；可从游戏目录扫描导入。
- **方案（Profile）**：多套勾选组合，一键「应用方案」扁平同步到游戏的 `Mods` / `Plugins` / `UserLibs` / `UserData`。
- **Mod 浏览**：从 [MechabellumMods](https://github.com/llxlzx/MechabellumMods) 拉取目录，「加入本地库」（**不会**自动启用）。
- **投稿 / 举报**：打开邮件客户端发往 **llxmod@foxmail.com**（标准主题前缀 + 模板正文）；维护者审核后上传社区目录。作者**无需** Fork/PR。
- **多语言**：简体中文 / English / 日本語 / Deutsch / Русский（可跟随系统）。
- **安装包**：可选安装 .NET 8（管理器）、.NET 6（MelonLoader）、MelonLoader；优先使用 `installer/redist/` 离线包。

---

## 最终用户 (中文)

更细的安装与使用说明见 [docs/分发-使用说明.md](docs/分发-使用说明.md)。

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

## Mod 作者投稿 (中文)

投稿 / 更新 / 举报请发邮件至 **llxmod@foxmail.com**（作者**不需要** Fork 或开 PR）。

1. 在管理器点 **投稿 Mod**，阅读弹窗后点「打开发送投稿邮件」。  
2. 主题必须以标准前缀开头，例如 `[Mod投稿/Submit] 你的Mod名`；一封邮件只办一件事。  
3. 投稿/更新请附加 `.dll`（可选 `preview.png`）；附件过大时在正文写网盘链接。  
4. 完整模板与说明见社区仓库：  
   **[投稿与举报](https://github.com/llxlzx/MechabellumMods#投稿与举报--submit--report)** · [submit.html](https://github.com/llxlzx/MechabellumMods/blob/main/docs/submit.html)

维护者审核后会上传到本仓库的社区目录；玩家在管理器里 **刷新目录** 即可看到。

举报：对目录或本地库中的 Mod 点 **举报**，选择分类后打开预填邮件（正文也会复制到剪贴板），发送至 **llxmod@foxmail.com**。

---

## 开发与发版 (中文)

### 构建安装包

开发机需安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)。正式包**必须**准备 MelonLoader 离线 zip：

`installer/redist/melonloader/MelonLoader.x64.zip`

```powershell
.\installer\build-installer.ps1
# 或
.\installer\build-installer.bat
```

产物：`dist\MechabellumModManager_Setup_v*.exe`。整理与发版步骤见 [docs/GitHub-Release更新说明.md（中文）](docs/GitHub-Release更新说明.md#1-两个仓库分别干什么-中文)。

### 开发命令

```powershell
dotnet build
dotnet test
dotnet publish src\MechabellumModManager -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

最终用户请使用 **Setup** 或开始菜单快捷方式，不要依赖仓库根目录下临时 bat / 便捷 exe 副本。

维护者发版流程见 [GitHub Release 更新说明（中文）](docs/GitHub-Release更新说明.md#3-发管理器新版本标准步骤-中文)。更紧凑的流程摘要见 [docs/releasing.md](docs/releasing.md)。

---

## 风险声明 (中文)

- 使用第三方 Mod 可能导致**封号、存档损坏、游戏或系统异常**等，风险自负。  
- 高风险包启用需二次确认；管理器提供的本地检查**不是**完整杀毒。  
- 本管理器**不含**作弊入口；**不再内置**一键安装 MelonLoader（改由安装包处理）。  
- 投稿与目录内容由提供者负责；维护者可拒绝上架或下架，但无法保证每一份文件的安全性。  
- 详细免责声明见应用内「关于与声明」。

---

## Features (English)

- **External mod library**: import DLL / Zip / folder; optional import from the game folder.
- **Profiles**: multiple enable-sets; **Apply profile** flattens files into the game `Mods` / `Plugins` / `UserLibs` / `UserData` folders.
- **Browse mods**: fetch [MechabellumMods](https://github.com/llxlzx/MechabellumMods) catalog; **Add to library** (does **not** auto-enable).
- **Submit / report**: opens mailto to **llxmod@foxmail.com** (standard subject prefixes + templates). Maintainers review and upload; authors do **not** need Fork/PR.
- **Languages**: zh-CN / en / ja / de / ru (or follow the OS).
- **Installer**: optional .NET 8 (app), .NET 6 (MelonLoader), MelonLoader; prefers offline files under `installer/redist/`.

---

## End users (English)

See also [docs/分发-使用说明.md](docs/分发-使用说明.md) (bilingual end-user notes).

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

## Mod authors submit (English)

Submit / update / report by email to **llxmod@foxmail.com** (no Fork/PR required).

1. In the manager, click **Submit Mod**, then **Open submit email**.  
2. Subject must use a standard prefix, e.g. `[Mod投稿/Submit] YourModName`; one email = one request.  
3. For submit/update, attach `.dll` (optional `preview.png`); use a netdisk link if attachments are huge.  
4. Full templates:  
   **[Submit & Report](https://github.com/llxlzx/MechabellumMods#投稿与举报--submit--report)** · [submit.html](https://github.com/llxlzx/MechabellumMods/blob/main/docs/submit.html)

After review, maintainers upload to the community catalog; players **Refresh catalog** in the manager.

**Report**: pick a category; the manager opens a pre-filled mail (body also copied to clipboard) to **llxmod@foxmail.com**.

---

## Developers release (English)

### Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php). Release builds **require**:

`installer/redist/melonloader/MelonLoader.x64.zip`

```powershell
.\installer\build-installer.ps1
```

Output: `dist\MechabellumModManager_Setup_v*.exe`. Packaging / Release steps: [GitHub Release guide (English)](docs/GitHub-Release更新说明.md#1-what-the-two-repos-are-for-english).

### Dev commands

```powershell
dotnet build
dotnet test
dotnet publish src\MechabellumModManager -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

End users should run the **Setup** or Start Menu shortcut—not ad-hoc bat/exe copies in the repo root.

Release checklist: upload Setup + `latest.json` + portable zip; see [Shipping steps (English)](docs/GitHub-Release更新说明.md#3-shipping-a-new-manager-version-english). Compact summary: [docs/releasing.md](docs/releasing.md).

---

## Risk notice (English)

- Third-party mods may cause **bans, save corruption, or instability**—use at your own risk.  
- High-risk packages require confirmation; local checks are **not** a full antivirus.  
- This manager has **no** cheat entry points and **no** in-app one-click MelonLoader install (installer handles Melon).  
- Catalog / submissions are the providers’ responsibility; maintainers may reject or remove entries without guaranteeing every file’s safety.  
- Full disclaimer: in-app **About / Credits**.
