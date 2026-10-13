# 安装包（Inno Setup）设计

日期：2026-09-03  
状态：已批准

## 1. 目标

将分发方式改为 **Inno Setup 安装包**，在安装过程中一并处理：

- 管理器本体安装
- .NET 8 Desktop Runtime x64（管理器）
- .NET 6 Desktop Runtime x64（MelonLoader）
- MelonLoader（默认勾选，可取消）
- 游戏路径选择并写入 `config.json`

管理器内 **移除**「一键安装 MelonLoader」功能。

## 2. 非目标

- 不附带任何第三方游戏 Mod
- 不实现 WiX/MSI 或自研 WPF 安装向导
- 不强制用户必须安装 MelonLoader

## 3. 分发策略：混合

安装包可选内嵌 `redist/`：

| 组件 | 本地优先 | 回退 |
|------|----------|------|
| windowsdesktop-runtime-8.x-win-x64.exe | `redist/dotnet8/` | Microsoft 官方 CDN |
| windowsdesktop-runtime-6.x-win-x64.exe | `redist/dotnet6/` | Microsoft 官方 CDN |
| MelonLoader.x64.zip | `redist/melonloader/` | GitHub latest release |

已安装的 Runtime 检测后跳过。下载/安装失败：提示官方链接，允许跳过该项继续（管理器本体仍可装完）。

## 4. 向导流程

1. 欢迎 + 风险说明  
2. 管理器安装目录  
3. 游戏路径（自动探测 + 浏览；校验 `Mechabellum.exe` + `GameAssembly.dll`）  
4. 组件：本体必选；.NET8 / .NET6 / MelonLoader（MelonLoader 默认勾选）  
5. 执行安装与进度  
6. 完成（可选启动管理器）

需要 **管理员权限**（系统级 Runtime）。

## 5. 安装动作

1. 复制管理器 exe + `Assets/` 到安装目录；写开始菜单/可选桌面快捷方式  
2. 若勾选且未安装：静默 `/install /quiet /norestart` 安装对应 Desktop Runtime  
3. 若勾选 MelonLoader：解压 zip 到游戏根目录；调用与现网一致的 `Loader.cfg` 优化（`force_quit`、`force_offline_generation`）  
4. 写入 `%AppData%\MechabellumModManager\config.json` 的 `gamePath`（保留其他已有字段若存在）

## 6. 管理器改动

- 删除一键安装 MelonLoader 的按钮、命令、进度条 UI 及对 `MelonLoaderInstaller` 的 UI 绑定  
- `MelonLoaderInstaller` / `MelonLoaderConfigOptimizer` 逻辑可抽到安装脚本侧复用或保留为库供安装辅助工具调用；**UI 不再暴露**  
- 缺少 Loader 时状态文案提示：通过安装包重装/勾选 MelonLoader，或自行安装  
- 设置中的游戏路径浏览保留

## 7. 工程布局

```
installer/
  MechabellumModManager.iss      # Inno 脚本
  zh-cn.isl                      # 如需（或用官方中文）
  scripts/
    DetectDotNet.ps1             # 或 Pascal 内嵌检测
    InstallPrereqs.ps1           # 混合下载+静默装
  redist/                        # 可选离线缓存（gitignore 大文件）
    README.md                    # 如何放入离线包
  build-installer.bat            # 发布 exe 后编译 Setup
```

## 8. 验收

1. 干净 Win10/11 机：安装包可装管理器；缺 Runtime 时能装上或给出跳过提示  
2. 勾选 MelonLoader + 有效游戏路径 → 游戏目录出现 MelonLoader + proxy dll  
3. 取消 MelonLoader → 管理器可用，状态为缺少 Loader  
4. 管理器内无「一键安装 MelonLoader」  
5. 安装后 config 游戏路径正确；库为空  
6. 单元测试（去掉 UI 安装路径后）仍通过  
