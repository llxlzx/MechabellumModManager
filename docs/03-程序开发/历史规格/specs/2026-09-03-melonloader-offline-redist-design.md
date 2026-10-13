# 设计：MelonLoader 发版离线嵌入 + 失败提示

日期：2026-09-03  
状态：已批准并实现（构建硬失败 + 失败提示）  
选型：B（发版嵌入离线 zip）+ A（联网失败明确提示）；构建缺 zip **硬失败**

## 背景

安装 MelonLoader 时若无本地包，会从 GitHub 拉取 `MelonLoader.x64.zip`。国内常无法访问，表现为长时间卡住且原因不明。

## 目标

1. 正式 Setup **必须**内嵌官方 `MelonLoader.x64.zip`，终端用户勾选 Melon 时优先解压本地包，**默认不访问 GitHub**。
2. 仅当安装机上的内嵌/本地 zip 缺失（异常包）才回退联网；失败时提示 GitHub/代理与手动安装路径。
3. `build-installer` 在缺少 redist zip 时 **禁止出包**（硬失败）。

## 非目标

- 不引入 ghproxy 等第三方镜像。
- 不强制嵌入 .NET Desktop Runtime（体积与更新节奏不同）。
- 不恢复管理器内一键安装 MelonLoader。
- 不把大型 zip **提交进 git**（继续由 `.gitignore` 忽略）。

## 行为设计

### 构建（硬失败）

`installer/build-installer.ps1` / `.bat` 在调用 ISCC **之前**：

- 检查路径：`installer/redist/melonloader/MelonLoader.x64.zip`
- 若不存在或大小为 0：打印明确错误（需从 LavaGang Releases 下载官方 x64 zip 放到该路径），以 **非零退出码** 结束，**不生成** / 不覆盖可用 Setup。
- 提供开关：`-SkipMelonRedistCheck`（仅本地调试）；正式发版文档要求**不得**使用该开关。
- `docs/releasing.md` 将「可选放入 redist」改为 MelonLoader zip **发版必选**；步骤写清下载来源与文件名。

### 安装（运行时）

保持现有优先序（`Install-MelonLoader.ps1`）：

1. `{app}\installer-redist\melonloader\MelonLoader.x64.zip`（随 Setup 展开）
2. 否则下载 `https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip`

文案与错误增强：

- Inno 组件 `melon`：说明已内嵌离线包、一般无需访问 GitHub。
- 下载失败 / exit 2：MsgBox 与脚本错误信息包含：
  - 可能无法访问 GitHub，需代理或更换网络；
  - 可取消 Melon 组件后重装，或手动从官方 Releases 安装；
  - 链接：`https://github.com/LavaGang/MelonLoader/releases`。
- 状态栏：有本地 zip 时提示「使用安装包内嵌的 MelonLoader…」；走下载时提示「正在从 GitHub 下载（可能需可访问 GitHub 的网络）…」。

### 仓库与体积

- zip 仅存在于发版机 `installer/redist/melonloader/`，gitignore 不变。
- Setup 体积增加约 MelonLoader zip 大小（量级约数十 MB，以当时官方包为准）。
- `installer/redist/README.md` 标明 MelonLoader 对正式发版为 **required**，dotnet redist 仍为 optional。

## 验收

1. 去掉 zip 后跑 `build-installer` → 失败，无新的成功 Setup。
2. 放入有效 zip 后 → 成功出包；安装勾选 Melon 时日志为 Using local…，断网仍可装 Melon。
3. 人为去掉已安装目录下的内嵌 zip 再跑脚本联网失败 → 出现含 GitHub/代理说明的错误。

## 实现触及文件（预估）

- `installer/build-installer.ps1` / `.bat`
- `installer/MechabellumModManager.iss`
- `installer/scripts/Install-MelonLoader.ps1`
- `installer/redist/README.md`
- `docs/releasing.md`
- 本设计对应实现计划（另文）
