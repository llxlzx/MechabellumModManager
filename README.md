# Mechabellum（钢铁指挥官）Mod 管理器

Windows 桌面工具：外部 Mod 库 + 方案（Profile）勾选，一键同步部署到游戏目录（拍平到 `Mods/` / `Plugins/` / `UserLibs/` 等根目录）。不代装 MelonLoader。

## 前置条件

- **.NET 8 SDK**（构建/运行）
- **MelonLoader** 已由用户自行安装到游戏目录（本工具只检测 Ready，不代装）
- 已安装 **Mechabellum（钢铁指挥官）**

## 构建与运行

```powershell
cd "D:\gongzuo\钢铁指挥官mod管理器开发"
dotnet build
dotnet run --project src\MechabellumModManager
```

测试：

```powershell
dotnet test
```

## 风险声明

本工具可向游戏目录写入 Mod/插件文件。使用第三方 Mod **可能导致封号、存档损坏或游戏异常**，尤其在联网 / PVP 场景。请自行承担风险；工具内始终显示风险横幅，高风险包启用前需二次确认。本工具**不含作弊功能入口**。


## 启动模式

- **SteamThenExe**（默认）：best-effort。先尝试通过 Steam URI（`steam://...`）启动；仅当 `Process.Start` **抛出异常**（例如未注册协议处理器）时才回退到直接启动 `Mechabellum.exe`。
- 该模式**不会**校验 Steam 是否已安装，也**不会**确认游戏进程是否真正拉起。
- **SteamOnly** / **ExeOnly**：分别只走 Steam URI 或只走 exe。

## 导入

- UI：导入 DLL / Zip / 文件夹。无法自动识别类型时会弹出类型选择框。
- 服务层亦提供 `ImportFolder(path, forceType?)`，与 Zip 使用相同的前缀分组与两阶段提交逻辑。

## 测试

**全量套件（2026-09-02）：** `dotnet test` → **通过 43 / 失败 0 / 跳过 0**（约 426 ms，`MechabellumModManager.Tests`）。

### 设计规格 §12 成功标准清单

| # | 标准 | 结果 | 自动化 / 手动 |
|---|------|------|----------------|
| 1 | 检测默认或用户指定目录；区分无效 / Loader 缺失或不完整 / Ready | 达标 | **自动化** `GameDetectorTests`；UI 选路径为手动冒烟 |
| 2 | 导入 `QuickCamera.zip`/`QuickCamera.dll` 并登记为 `melon_mod` | 达标 | **自动化** `ModLibraryImportTests` |
| 3 | 应用后位于 `{Game}/Mods/QuickCamera.dll`（根目录，非子文件夹） | 达标 | **自动化** `DeployPlannerTests` / 部署服务测试 |
| 4 | ≥2 套方案切换；托管文件随方案增删，**不删**非托管手工文件 | 达标 | **自动化** `ProfileServiceTests` + `DeployPlanner`/`DeployService` |
| 5 | 非托管同名默认不覆盖；UserData 不同步 `Loader.cfg` | 达标 | **自动化** 冲突规划 + `Rejects_Loader_cfg_in_userdata_package` |
| 6 | `gamePath` 变更后不按旧 manifest 误删 | 达标 | **自动化** `GamePath_mismatch_skips_deletes` 等 |
| 7 | 「应用并启动」在 Ready 时可拉起（Steam 优先） | 达标（逻辑） | **自动化** `GameLauncherTests` / `MainViewModelTests`；真实 Steam/exe 启动需 **手动** |
| 8 | UI 含联网 PVP 风险提示；无作弊入口 | 达标 | **自动化** `RiskGateTests` + 窗口冒烟；无作弊入口为设计/代码审查 **手动** |

真实游戏树上的端到端导入/应用/启动建议在本机再做一次手动验收。
