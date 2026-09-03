# 发布说明（GitHub Releases）

仓库：https://github.com/llxlzx/MechabellumModManager  
管理器通过下列地址检查更新（优先）：

`https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`

失败时回退 GitHub API：`/repos/llxlzx/MechabellumModManager/releases/latest`。

## 版本号

同步三处：

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion`
3. Release 上的 `latest.json` → `version`

## 构建安装包

在仓库根目录：

```bat
installer\build-installer.bat
```

或：

```powershell
.\installer\build-installer.ps1
```

产物：`dist\MechabellumModManager_Setup_vX.Y.Z.exe`

可选：将 .NET / MelonLoader 离线包放入 `installer\redist\`（见 `installer\redist\README.md`），安装时优先本地、无需下载。

## latest.json

每个 Release **必须**附带同名资源 `latest.json`（与 Setup 一起上传到 **latest** 标签对应的 Release，或始终维护一个指向最新的 Release）。

示例（见 `docs/latest.example.json`）：

```json
{
  "version": "1.0.1",
  "notes": "修复游戏路径检测；改进安装器 .NET 进度显示。",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.0.1/MechabellumModManager_Setup_v1.0.1.exe",
  "publishedAt": "2026-03-22T00:00:00Z"
}
```

字段：

| 字段 | 说明 |
|------|------|
| `version` | 语义化版本，与 csproj / ISS 一致 |
| `notes` | 更新说明（可多行，管理器弹窗展示） |
| `setupUrl` | Setup 直链（推荐 Releases download URL） |
| `publishedAt` | ISO-8601 时间，可选 |

## 发布步骤

1.  bump 版本（csproj + ISS）
2. 提交并推送 `master`
3. 本地跑 `build-installer`，确认 `dist\` 下 Setup
4. 在 GitHub 创建 Release（tag 建议 `vX.Y.Z`）
5. 上传：
   - `MechabellumModManager_Setup_vX.Y.Z.exe`
   - `latest.json`（文件名必须为 `latest.json`）
6. 用管理器「设置 → 检查更新」验证

## 管理器行为

- **不会**静默自动安装
- 发现新版本：弹窗显示版本与说明，可选「打开下载页 / 下载安装包」
- 用户自行运行新 Setup 完成升级
