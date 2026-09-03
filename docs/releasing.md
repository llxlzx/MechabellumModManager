# GitHub Release 更新流程（现行）

两个相关仓库：

| 仓库 | 用途 |
|------|------|
| https://github.com/llxlzx/MechabellumModManager | 管理器程序 + Setup / 本体 |
| https://github.com/llxlzx/MechabellumMods | Mod 浏览大全（catalog + dll + preview） |

管理器检查更新优先读：

`https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`

失败时回退 API：`/repos/llxlzx/MechabellumModManager/releases/latest`。

---

## 1. 发版前准备

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
cd D:\gongzuo\钢铁指挥官mod管理器开发
dotnet test -c Release
git add -A   # 勿提交 Melon zip / publish / dist
git commit -m "..."
git push origin master   # 若需代理：$env:HTTPS_PROXY='http://127.0.0.1:7890'
```

---

## 2. 本地出包

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
  "version": "1.0.3",
  "notes": "本版更新说明……",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.0.3/MechabellumModManager_Setup_v1.0.3.exe",
  "publishedAt": "2026-09-03T00:00:00Z"
}
```

把 `release/vX.Y.Z/` 提交并推送到管理器仓库（可选但建议，便于对照）。

---

## 3. 创建 GitHub Release（网页）

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

## 4. 命令行备选（已装 gh 并登录）

```powershell
cd D:\gongzuo\钢铁指挥官mod管理器开发
$env:HTTPS_PROXY='http://127.0.0.1:7890'
$env:HTTP_PROXY='http://127.0.0.1:7890'

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

## 5. Mod 大全仓库（与程序发版分开）

Mod 内容在 **MechabellumMods**，不随 Setup 版本强制同步，但若改了 catalog / preview / dll：

```powershell
cd D:\gongzuo\MechabellumMods
git add -A
git commit -m "..."
git push origin master
```

确认：  
`https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json`

管理器「Mod 浏览 → 刷新目录」应能看到更新。作者提交流程见该仓库 `README.md`。

---

## 6. 当前最新本地对照（写作时）

- 程序 / Setup：**v1.0.3** → `release/v1.0.3/`
- 测试安装包副本：`D:\gongzuo\钢铁指挥官Mod管理器_安装包测试\MechabellumModManager_Setup_v1.0.3.exe`

---

## 管理器更新行为（提醒用户）

- 不会静默自动安装  
- 「检查更新」提示后，用户自行下载并运行新 Setup  
- 便携「本体」需本机已装 .NET 8 Desktop Runtime  
