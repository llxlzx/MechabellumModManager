# 钢铁指挥官 Mod 管理器 — GitHub Release 更新说明

面向维护者：如何打新版本安装包，并发布到 GitHub Releases。  
更完整的技术细节见同目录 `releasing.md`。

---

## 一、两个仓库分别干什么

| 仓库 | 地址 | 发什么 |
|------|------|--------|
| **管理器** | https://github.com/llxlzx/MechabellumModManager | Setup 安装包、便携本体、latest.json |
| **Mod 大全** | https://github.com/llxlzx/MechabellumMods | catalog.json、各 Mod 的 dll / 预览图 |

玩家「检查更新」只看**管理器**仓库的 Release。  
「Mod 浏览 → 刷新目录」只看 **Mods** 仓库的 `catalog.json`。两者发版可以不同步。

---

## 二、本地文件怎么分（安装包 vs 本体）

在管理器仓库的 `release/v版本号/` 下：

```
release/v1.0.3/
  安装包/     → 给绝大多数用户（Setup.exe）
  本体/       → 便携运行（exe + Assets，无 Mod 数据）
  latest.json → 给「检查更新」用
  MechabellumModManager_portable_v1.0.3.zip  → 把「本体」打成的 zip，上传 Release
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

## 三、发管理器新版本：标准步骤

以发布 **v1.0.3** 为例（以后把版本号换成新的即可）。

### 步骤 1 — 改版本号

同时改这三处，数字必须一致：

1. `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>1.0.3</Version>`
2. `installer/MechabellumModManager.iss` → `#define MyAppVersion "1.0.3"`
3. 稍后的 `latest.json` → `"version": "1.0.3"`

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

得到：`dist\MechabellumModManager_Setup_v1.0.3.exe`  
（体积大约二十多 MB 才正常，因为内嵌了 Melon。）

把产物整理进 `release/v1.0.3/安装包/` 与 `release/v1.0.3/本体/`，并写好 `latest.json`。

### 步骤 5 — 打本体 zip

```powershell
cd release\v1.0.3
Compress-Archive -Path ".\本体\*" -DestinationPath ".\MechabellumModManager_portable_v1.0.3.zip" -Force
```

解压后应直接看到 exe 和 Assets，而不是多一层无关目录。

### 步骤 6 — 在 GitHub 上建 Release

1. 打开：https://github.com/llxlzx/MechabellumModManager/releases  
2. **Draft a new release**  
3. **Tag**：输入 `v1.0.3` → Create new tag；**Target** 选 `master`  
4. **Title**：`v1.0.3`  
5. **说明**：写本版更新点（可与 latest.json 的 notes 相同）  
6. **上传附件**（拖进虚线框）：

| 必传 / 推荐 | 文件 |
|-------------|------|
| 必传 | `安装包\MechabellumModManager_Setup_v1.0.3.exe` |
| 必传 | `latest.json`（**文件名不能改**） |
| 推荐 | `MechabellumModManager_portable_v1.0.3.zip` |

7. 勾选 **Set as the latest release**  
8. **不要**勾选 Pre-release  
9. **Publish release**

### 步骤 7 — 自检

- 打开：https://github.com/llxlzx/MechabellumModManager/releases/tag/v1.0.3  
  确认三个资源都在  
- 打开：  
  `https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json`  
  能显示 JSON  
- 打开管理器 → **设置 → 检查更新**，应提示有新版本（若本机还是旧版）

---

## 四、latest.json 怎么写

```json
{
  "version": "1.0.3",
  "notes": "本版更新说明，可多行。",
  "setupUrl": "https://github.com/llxlzx/MechabellumModManager/releases/download/v1.0.3/MechabellumModManager_Setup_v1.0.3.exe",
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

## 五、只更新 Mod 大全时（不发管理器新版本）

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

作者提交方式见：`MechabellumMods` 仓库内的 `README.md`。

---

## 六、当前本地对照（写作时）

| 项 | 位置 |
|----|------|
| 最新整理目录 | `release/v1.0.3/` |
| 测试用 Setup 副本 | `D:\gongzuo\钢铁指挥官Mod管理器_安装包测试\MechabellumModManager_Setup_v1.0.3.exe` |
| 流程详版 | `docs/releasing.md` |
| 本说明 | `docs/GitHub-Release更新说明.md` |

---

## 七、常见问题

**Q：安装测试目录里的「钢铁指挥官Mod管理器」能整包当本体吗？**  
A：不能。去掉卸载器和 installer 目录后，理论上只留 exe+Assets 可以，但请优先用 `release/vX.Y.Z/本体\`。

**Q：Push / 检查更新失败，网页却能开 GitHub？**  
A：浏览器走了代理，Git/管理器可能没走。给终端设 `HTTPS_PROXY=http://127.0.0.1:7890`（端口按你的代理软件为准）。

**Q：Mod 浏览刷新失败？**  
A：检查 Mods 仓库是否公开、`catalog.json` 是否在 `master` 分支根目录，以及本机能否访问 raw.githubusercontent.com。
