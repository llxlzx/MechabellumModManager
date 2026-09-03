# 设计：安装器 .NET 进度 + GitHub 检查更新

日期：2026-03-22  
状态：已批准（用户同意）

## 1. 安装器 .NET Desktop Runtime

### 目标

用户勾选安装 .NET 时能感知进度与大致体积，避免「静默卡住」的感觉。

### 行为

- 组件说明文案带约略下载包体积与安装后占用（.NET 8 / 6 Desktop x64）。
- 已安装则跳过（Detect-DotNetDesktop.ps1）。
- 优先 `installer/redist/dotnet{6|8}/` 本地包；否则从 Microsoft CDN 下载，PowerShell 输出下载状态。
- 安装参数：`/install /passive /norestart`（显示官方安装 UI，非 `/quiet`）。
- Inno `CurStepChanged` 前后用 `WizardForm.StatusLabel`（若可用）或 MsgBox 前状态提示「正在安装 .NET …」。

### 约略体积（文案用，非精确探测）

| 运行时 | 下载包约 | 安装后约 |
|--------|----------|----------|
| .NET 8 Desktop x64 | 55–60 MB | 150–200 MB |
| .NET 6 Desktop x64 | 50–55 MB | 140–180 MB |

## 2. GitHub Releases 检查更新

### 源

- 仓库：`llxlzx/MechabellumModManager`
- 优先：`.../releases/latest/download/latest.json`
- 回退：GitHub Releases API `latest`

### 管理器

- 设置区增加「检查更新」按钮。
- 比较本地 `AssemblyInformationalVersion` / `Version` 与远端 `version`。
- 有新版本：确认框展示 notes；可打开浏览器到 setupUrl 或 Release 页。
- **不**自动静默安装。

### 发布配套

见 `docs/releasing.md`。
