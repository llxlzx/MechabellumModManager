# MelonLoader 一键安装 — 设计规格

**日期：** 2026-09-03  
**状态：** 已实现（管理器支持一键安装最新正式版 MelonLoader）  
**范围：** 在现有 Mechabellum Mod 管理器中新增「一键安装 / 更新 MelonLoader」  
**关联：** 修正原规格「一期不代装 MelonLoader」；用户确认采用方案 1 + 始终最新正式版  

---

## 1. 目标

当游戏已找到但 MelonLoader 缺失或不完整时，用户可在管理器内一键完成安装，无需手动下 zip、解压。

## 2. 非目标

- 不安装 BepInEx  
- 不代装 .NET 6 Desktop Runtime / VC++（可提示下载链接）  
- 不修改 Steam 游戏文件校验策略以外的无关文件  
- 不做 MelonLoader 完整卸载器（可二期）  

## 3. 行为

### 3.1 来源与版本

- 官方仓库：`LavaGang/MelonLoader`  
- 使用 GitHub Releases **最新正式版**（非 pre-release）  
- 资源：`MelonLoader.x64.zip`（Mechabellum 为 64 位）  
- 解析方式：优先 `GET https://api.github.com/repos/LavaGang/MelonLoader/releases/latest`，取名为 `MelonLoader.x64.zip` 的 `browser_download_url`；若 API 失败可回退  
  `https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip`

### 3.2 UI

- 游戏路径有效且状态为 `GameOkLoaderMissing` 或 `LoaderPartial`：显示主按钮 **「一键安装 MelonLoader」**  
- 状态为 `Ready`：显示次要按钮 **「重新安装 / 更新 MelonLoader」**  
- 状态为 `GameMissing`：按钮禁用  
- 安装进行中：按钮禁用，日志显示进度（下载中 / 解压中）  

### 3.3 流程

1. 用户确认（简短风险：第三方加载器、需关游戏、需联网）  
2. `ProcessProbe`：游戏运行中则中止  
3. `GameDetector`：游戏路径必须有效（非 GameMissing）  
4. 下载 zip 到临时目录（带 User-Agent，超时与错误可读）  
5. 解压到游戏根目录：写入 `MelonLoader/`、`version.dll`、`dobby.dll`（以包内实际文件为准）  
6. 覆盖已存在的同名加载器文件  
7. 清理临时文件  
8. 重新 `Detect`；成功则期望 `Ready`，日志记录 tag/version  

### 3.4 失败处理

| 场景 | 行为 |
|---|---|
| 无网络 / GitHub 失败 | 中文错误 + 官方 Releases 链接 |
| 游戏占用 | 提示先关闭 Mechabellum |
| 解压权限不足 | 提示以管理员重试或检查目录权限 |
| 安装后仍非 Ready | 报告检测结果，提示检查 .NET 6 Desktop Runtime |

## 4. 实现要点

- 新服务：`MelonLoaderInstaller`（下载 + 解压 + 结果）  
- `MainViewModel`：`InstallMelonLoaderCommand`、`CanInstallMelonLoader`、`IsInstallingLoader`  
- `MainWindow`：顶栏状态旁按钮  
- 单元测试：用假 `HttpMessageHandler` / 本地 zip 固件测解压到临时游戏根；检测 Ready 条件  

## 5. 成功标准

1. 在缺少 Loader 的有效游戏目录上，一键后出现 `MelonLoader/` 与代理 dll，状态变为 Ready（环境依赖满足时）  
2. 游戏运行中拒绝安装  
3. 网络失败有明确中文提示，不造成半残目录无说明（尽量完整解压或回滚本次写入清单）  

## 6. 已确认决策

| 项 | 结论 |
|---|---|
| 方案 | 管理器内下载并解压 |
| 版本 | 始终 GitHub 最新正式版 |
| 架构 | x64 zip |
