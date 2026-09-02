# Mechabellum（钢铁指挥官）Mod 管理器

Windows 桌面工具：外部 Mod 库 + 方案勾选，一键同步到游戏目录。

## 最终用户

1. 运行安装包 `钢铁指挥官Mod管理器_Setup_v*.exe`（管理员）
2. 安装过程中选择游戏路径，并按需安装 .NET 8 / .NET 6 / MelonLoader（混合：优先离线缓存，否则联网下载）
3. 启动管理器后导入 Mod 并应用方案

详见 `docs/分发-使用说明.txt`。

制作安装包（开发机需安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```powershell
.\installer\build-installer.bat
```

可选：将官方 Runtime / MelonLoader zip 放入 `installer/redist/` 后再编译，以支持离线安装。

## 开发者

```powershell
dotnet build
dotnet test
dotnet publish src\MechabellumModManager -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o 发布
```

## 风险声明

使用第三方 Mod 可能导致封号、存档损坏或异常。高风险包启用需确认。管理器**不含**作弊入口；**不再内置**一键安装 MelonLoader（改由安装包处理）。
