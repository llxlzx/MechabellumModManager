# Mechabellum（钢铁指挥官）Mod 管理器

Windows 桌面工具：外部 Mod 库 + 方案（Profile）勾选，一键同步到游戏目录（`Mods/` / `Plugins/` / `UserLibs/` / `UserData/`）。

## 最终用户（分发版）

1. 安装 [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 解压后运行 `钢铁指挥官Mod管理器.exe`
3. 游戏路径会尝试从 Steam 库自动探测；失败请在「设置」里手动选择

详细说明见分发包内 `使用说明.txt`。

## MelonLoader

- 「缺少 Loader」时可 **一键安装 MelonLoader**（GitHub 最新正式版 x64）
- 首次启动游戏可能需 1～2 分钟生成 IL2CPP 程序集，属正常现象
- MelonLoader 本身可能还需要 [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)

## 开发者构建

```powershell
dotnet build
dotnet test
dotnet publish src\MechabellumModManager -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o 发布
```

本机需 .NET 8 SDK。

## 风险声明

使用第三方 Mod **可能导致封号、存档损坏或游戏异常**，尤其在联网 / PVP。工具内有风险横幅；高风险包启用需确认。名称启发式可能误报/漏报。本工具**不含作弊功能入口**。

## 启动模式

- **SteamThenExe**（默认）：先 Steam URI，异常时回退 exe
- **SteamOnly** / **ExeOnly**
