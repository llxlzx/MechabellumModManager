# 发布说明（GitHub Releases）

仓库：https://github.com/llxlzx/MechabellumModManager  
管理器通过下列地址检查更新（优先）：

`https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`

失败时回退 GitHub API：`/repos/llxlzx/MechabellumModManager/releases/latest`。

## 仓库内 release/ 布局

本地整理产物时建议按版本分目录，并区分「安装包」与「本体」：

```
release/vX.Y.Z/
  安装包/          # Setup exe + 用户 README.txt（推荐分发）
  本体/            # 便携 exe + Assets + README.txt（需 .NET 8 Desktop）
  latest.json      # 可选：本地对照；上传 Release 时文件名仍为 latest.json
```

详见 `release/README.md`。上传到 GitHub Releases 时，可将 Setup 与 `latest.json` 作为 Release 资源上传（不必保留仓库子文件夹结构）。

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

### MelonLoader 离线包（发版必选）

在跑 `build-installer` **之前**，把官方文件放到：

`installer/redist/melonloader/MelonLoader.x64.zip`

来源：https://github.com/LavaGang/MelonLoader/releases  

缺失或空文件时构建脚本会以退出码 3 **硬失败**，禁止出包。  
调试可用 `-SkipMelonRedistCheck` / `SKIP_MELON_REDIST_CHECK=1`，**正式发版禁止使用**。

可选：将 .NET Desktop Runtime 离线安装包放入 `installer/redist/dotnet8` / `dotnet6`（见 `installer/redist/README.md`）。

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

1. bump 版本（csproj + ISS）
2. 提交并推送 `master`
3. 下载并放入 `MelonLoader.x64.zip`（见上文「发版必选」）
4. 本地跑 `build-installer`，确认 `dist\` 下 Setup（体积应含 MelonLoader zip）
5. 在 GitHub 创建 Release（tag 建议 `vX.Y.Z`）
6. 上传：
   - `MechabellumModManager_Setup_vX.Y.Z.exe`（来自 `release/vX.Y.Z/安装包/`；可选同时上传本体便携文件）
   - `latest.json`（文件名必须为 `latest.json`）
7. 用管理器「设置 → 检查更新」验证
8. Release notes 可注明：MelonLoader 已离线内嵌，安装一般无需访问 GitHub

## 管理器行为

- **不会**静默自动安装
- 发现新版本：弹窗显示版本与说明，可选「打开下载页 / 下载安装包」
- 用户自行运行新 Setup 完成升级
