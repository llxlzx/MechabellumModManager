# Mechabellum（钢铁指挥官）Mod 管理器 — 设计规格

**日期：** 2026-09-02  
**状态：** 规格已确认；实现计划已就绪（见 `docs/superpowers/plans/2026-09-02-mechabellum-mod-manager.md`）  
**工作目录：** `D:\gongzuo\钢铁指挥官mod管理器开发`  
**目标游戏路径（默认）：** `D:\steam\steamapps\common\Mechabellum`  
**Steam AppID：** `669330`

---

## 1. 目标与非目标

### 1.1 目标

开发一款 Windows 原生桌面 Mod 管理器，用于：

1. 检测本机 Mechabellum 安装是否有效，并报告 MelonLoader 就绪状态  
2. 管理 MelonLoader 生态下的客户端 QoL mods（样本：`QuickCamera.dll`）  
3. 使用外部库 + 按配置方案（Profile）**文件复制**同步到游戏目录  
4. 一键「应用当前方案并启动游戏」（Steam 协议优先，exe 回退）  
5. 为技术可行、相对稳定的非作弊类型预留管理位置  

### 1.2 非目标（一期不做）

- MelonLoader / BepInEx 的安装、卸载、升级  
- BepInEx 插件管理（与 MelonLoader 互斥；当前样本生态为 MelonLoader）  
- 贴图/音效等大型资源替换包（IL2CPP 下脆弱）  
- 在线 Mod 商店 / 自动更新源  
- 任何改战斗逻辑、经济、单位数值、战报结果的作弊能力  
- 多游戏通用框架（仅 Mechabellum）  
- Mod 加载顺序精细编排（跟随 MelonLoader 默认行为）  
- 从 dll 字节码自动判定「是否作弊」（不可靠，不做承诺）  

---

## 2. 环境约束（可行性依据）

| 项 | 结论 |
|---|---|
| 引擎 | Unity IL2CPP（存在 `GameAssembly.dll`、`Mechabellum.exe`） |
| 内核反作弊 | 未发现 EAC / BattlEye |
| 联网校验 | 服务端比对战报；不一致 → Data Error，可能判负/临时封禁 |
| 样本 Mod | `QuickCamera`：MelonLoader 0.7.3 `MelonMod`，相机 QoL |
| 当前安装 | 默认游戏目录下尚未安装 MelonLoader（管理器假设用户自行安装） |
| 运行时提示 | IL2CPP + MelonLoader 通常需要 .NET Desktop Runtime（如 6.x）；管理器只检测/提示，不代装 |

**产品定位：** 仅支持不影响战报一致性的客户端 QoL（相机、UI、快捷键、叠加层、配置等）。改模拟逻辑的 Mod 不作为支持类型。

---

## 3. 支持的 Mod 类型（预留范围）

| 类型 ID | 名称 | 游戏部署目录 | 一期 |
|---|---|---|---|
| `melon_mod` | MelonLoader Mod | `{GameRoot}/Mods/`（**仅根目录**） | 做 |
| `melon_plugin` | MelonLoader Plugin | `{GameRoot}/Plugins/`（**仅根目录**） | 做 |
| `melon_userlibs` | 依赖库（UserLibs） | `{GameRoot}/UserLibs/`（**仅根目录**） | 做（**独立库条目**；见下方说明） |
| `melon_userdata` | UserData / 配置包 | `{GameRoot}/UserData/`（**按包内相对路径**） | 做（严格清单，见 §7.3） |

**UserLibs 与 Mod 包的关系（消除歧义）：**  
一个库条目只有一种主类型。`melon_mod` 包内文件只部署到 `Mods/`，不会自动分流到 `UserLibs/`。若 zip 同时含 `Mods/` 与 `UserLibs/`，导入时拆成两个库条目；用户可在同一方案中一并启用。

**明确排除：** 玩法/数值作弊类；BepInEx；大型 Asset 替换。

类型以枚举 + 部署策略接口预留扩展；新增类型不得推翻外部库与 Profile 模型。

**硬约束（MelonLoader）：** `Mods` / `Plugins` / `UserLibs` **不扫描子文件夹**。部署必须把文件拍平到对应根目录。

---

## 4. 技术选型

| 项 | 选择 |
|---|---|
| 运行时 | .NET 8 |
| UI | WPF |
| MVVM | CommunityToolkit.Mvvm |
| 持久化 | JSON（System.Text.Json） |
| 架构风格 | 精简 WPF 单体：单仓库、服务类分层，不做重度插件宿主 |
| 部署方式 | 文件复制（不用 symlink/junction） |
| 启动 | 优先 `steam://rungameid/669330`，失败回退 `Mechabellum.exe` |

联机场景推荐 Steam 启动（票据/叠层完整）；直启仅作回退，并在设置中说明差异。

---

## 5. 架构

### 5.1 逻辑模块

1. **GameDetector**  
   - 游戏有效：`Mechabellum.exe` + `GameAssembly.dll`  
   - MelonLoader 就绪：存在 `MelonLoader/` **且** 存在代理文件之一（`version.dll` 或 `winhttp.dll`）  
   - 状态分级：`GameMissing` / `GameOkLoaderMissing` / `LoaderPartial` / `Ready`  
   - 路径来源：默认 Steam 路径 → `config.json` → 文件夹浏览  
   - 可选：提示本机是否像缺少 .NET Desktop Runtime（尽力检测，失败则忽略）  
   - MelonLoader 版本：若能从安装目录读取，与库条目声明版本不一致时 **警告不阻断**

2. **ModLibrary**  
   - 导入：`.zip` / `.dll` / 文件夹  
   - 一个库条目 = 一个包（可含多个文件 + 可选 `package.json`）  
   - `modId`：由显示名 slug + 内容哈希短缀生成，保证库内唯一；允许用户在导入后改显示名，不改 Id  
   - 类型识别（按优先级）：  
     1. 包内 `package.json` 的 `type`  
     2. 主 dll 基类/特性：`MelonMod` → `melon_mod`；`MelonPlugin` → `melon_plugin`  
     3. zip/文件夹约定路径：`Mods/`、`Plugins/`、`UserLibs/`、`UserData/`  
     4. 仍不确定 → **强制用户选择**，禁止静默猜错  
   - 仅「引用了 MelonLoader」不足以区分 Mod/Plugin  
   - 元数据：Id、显示名、版本、作者、类型、风险标记、**可部署文件列表**与哈希、可选所需 MelonLoader 版本  
   - 从库删除条目时：同时从所有方案的启用列表移除该 Id；若其文件仍在当前 deploy-manifest 中，UI 标记未同步，提示「应用方案」以从游戏目录卸下  
   - 包内默认**不部署**：`package.json`、`README*`、`*.pdb`、`.git*`；其余文件进入可部署列表（可用 `package.json` 的 `files` 显式覆盖）

3. **ProfileService**  
   - 多方案 CRUD（例：「仅相机」「比赛用」「开发用」）  
   - 每方案保存启用的 library 条目 Id 列表  
   - **勾选立即写入**当前方案 JSON（改的是方案成员，不是游戏目录）  
   - 「应用方案」只负责部署；「应用并启动」= 部署成功后再启动  
   - 当前活动方案 Id 写入 `config.json`  
   - 一期可预置空方案模板名称，不强制内置具体 mod  

4. **DeployService**  
   - 见 §7：拍平复制、冲突策略、manifest、回滚  
   - 部署前若 `Mods`/`Plugins`/`UserLibs`/`UserData` 缺失则创建空目录  

5. **GameLauncher**  
   - 仅在 Deploy 成功后启动  
   - Steam 协议优先；失败回退直启 exe  
   - 检测 `Mechabellum` 进程已在运行时：禁止部署；启动则提示「已在运行」  

6. **RiskGate**  
   - UI 常驻联网 PVP 风险说明  
   - 条目可标「高风险」；勾选进方案需二次确认  
   - **明确：不保证**从 dll 自动识别作弊；不做作弊功能或引导  

### 5.2 管理器侧目录布局

```
%AppData%/MechabellumModManager/     # 默认；可改为 exe 旁 data/ 可移植根
  config.json                        # gamePath、launchMode、activeProfileId、dataRoot
  library/
    mods/{modId}/                    # 包根；可部署文件 + 可选 package.json
    plugins/{modId}/
    userlibs/{modId}/
    userdata/{packId}/               # 包内相对路径 = 部署到 UserData 的相对路径
  profiles/{profileId}.json
  deploy-manifest.json               # 见 §7.2；与 deploy-manifest.prev.json 成对用于回滚
  logs/
```

包元数据文件统一命名为 **`package.json`**（避免与 `deploy-manifest.json` 混淆）。

### 5.3 游戏侧目标

| 类型 | 路径 | 布局 |
|---|---|---|
| Mod | `{GameRoot}/Mods/` | 仅根目录文件 |
| Plugin | `{GameRoot}/Plugins/` | 仅根目录文件 |
| UserLibs | `{GameRoot}/UserLibs/` | 仅根目录文件 |
| UserData | `{GameRoot}/UserData/` | 保留包内相对子路径 |

`GameRoot` 默认：`D:\steam\steamapps\common\Mechabellum`。

---

## 6. 界面（单窗口）

1. **顶栏：** 游戏/加载器状态、当前方案、「应用方案」「应用并启动」、设置  
2. **左栏：** 方案列表（新建 / 重命名 / 复制 / 删除）  
3. **中栏：** 库列表（类型标签、启用勾选、导入、删除、风险标记、版本警告）  
4. **底栏：** 同步日志、校验与错误信息  
5. **设置：** 游戏路径、启动方式（Steam/直启/自动回退）、数据根（AppData/可移植）、风险文案入口  

文案默认中文。勾选旁可显示「未应用」脏标记：当磁盘部署态与当前方案不一致时提示用户点「应用方案」。

---

## 7. 数据流与部署细则

### 7.0 主流程

```
导入 zip/dll/文件夹
  → 规范化（见 §7.4）写入 library/{type}/{modId}/
  → 解析或手动指定类型与元数据
  → 出现在库列表

勾选 / 取消勾选
  → 立即更新当前 profiles/{id}.json
  → UI 标记「方案已改，游戏目录未同步」直到成功应用

应用方案 / 应用并启动
  → GameDetector：一期要求状态为 Ready，否则拒绝
  → 若 Mechabellum 进程在运行：中止
  → DeployService（§7.1–7.5）
  → 成功后清除「未同步」标记
  →（若启动）GameLauncher

切换左栏方案
  → 更新 config.activeProfileId，加载该方案勾选态
  → 不自动部署；若与 deploy-manifest 不一致则显示未同步
```

### 7.1 拍平部署规则

对 `melon_mod` / `melon_plugin` / `melon_userlibs`：

- 仅复制该包「可部署文件列表」中的文件到对应游戏根目录  
- **禁止**在游戏侧创建以 modId 命名的子目录来存放这些 dll  
- 目标文件名默认 = 包内文件名（不含中间目录）；若同一方案内两包产生同名目标，**应用前拒绝并列出冲突**  
- 空方案（启用列表为空）合法：应用后应移除所有仍由 manifest 托管的文件  

对 `melon_userdata`：

- 目标 = `{GameRoot}/UserData/` + 包内相对路径  
- 允许子目录（与 MelonLoader 配置习惯一致）  

### 7.2 部署清单（deploy-manifest.json）

字段至少包括：

- `gamePath`：清单绑定的游戏根路径  
- `profileId`：上次成功应用的方案  
- `files[]`：`{ relativePath, packageId, sha256 }`（相对 `GameRoot`）  

规则：

- 只删除「在 manifest 中记录、且不再属于新方案目标集合」的文件  
- **不在 manifest 中的文件视为手工文件，默认不删除**  
- 若当前 `config.gamePath` ≠ `manifest.gamePath`：清单视为失效；提示重新应用；**不对旧路径执行删除**  

### 7.3 同名冲突与 UserData 安全

**同名冲突（Mods/Plugins/UserLibs）：**

- 目标路径已存在且 **不在** 当前 manifest 中 → 视为非托管冲突  
- 默认：**阻止覆盖**，弹窗说明；用户明确确认「覆盖并接管」后，方可写入并把该路径记入新 manifest  
- 目标存在且属于 manifest（本管理器上次写入）→ 允许覆盖更新  

**UserData 安全：**

- 只部署包清单声明的相对路径  
- **永远不删除、不覆盖** `UserData/Loader.cfg`（即便包内含同名也拒绝）  
- 删除托管 UserData 文件时：仅限 manifest 中且属于 userdata 包的路径  
- 游戏运行期新产生、未在包清单中的文件：不纳入托管，切换方案时不删  

### 7.4 导入规范化

支持：

- 单文件 `QuickCamera.dll`  
- zip 根下直接 dll  
- 常见嵌套：`Mods/xxx.dll`、`Plugins/xxx.dll`、`UserLibs/xxx.dll`、`UserData/**`  

导入时剥掉约定前缀，归入对应库类型目录；多类型混合 zip 可拆成多个库条目或一个多角色包（一期推荐：按顶层约定文件夹拆成多条目）。

### 7.5 失败回滚

1. 部署开始前：若存在 `deploy-manifest.json`，深拷贝为 `deploy-manifest.prev.json`（含 profileId + files）；若不存在（首次部署），prev 记为空集合  
2. 执行删/拷；任一步失败 → **停止**  
3. 回滚：  
   - prev 非空：按 prev 从 library 重新同步旧目标集合  
   - prev 为空：删除本次尝试已写入且记录在「本轮临时清单」中的文件  
4. 若回滚仍失败：保留错误日志与 prev 文件，UI 提示手动检查游戏目录，不假装成功  
5. 回滚成功后：`deploy-manifest.json` 恢复为 prev 内容（或空）；UI 仍可显示与当前方案勾选不一致的未同步状态  

---

## 8. 错误处理

| 场景 | 行为 |
|---|---|
| 游戏路径无效 | 错误状态；禁用部署/启动 |
| Loader 缺失或不完整 | 警告；禁止部署/启动；提示自行安装 MelonLoader |
| 游戏进程运行中 | 阻止同步与「应用并启动」 |
| 方案内文件名冲突 | 阻止应用；列出冲突包 |
| 非托管同名文件 | 默认阻止；确认后可覆盖并接管 |
| 复制失败 | 按 §7.5 回滚；记录失败路径 |
| 方案引用缺失库条目 | 打开时标红；计算部署计划时排除并记日志；可仍应用其余项 |
| 从库删除仍已部署的包 | 从各方案启用列表移除；标记未同步；应用后按 manifest 卸下 |
| gamePath 与 manifest 不一致 | 清单失效；不删旧路径文件；要求重新应用 |
| Steam 启动失败 | 回退直启 exe；仍失败则报错 |
| 类型无法识别 | 强制用户选择 |
| MelonLoader 版本可能不匹配 | 警告，不阻断 |

---

## 9. 风险与合规说明（产品文案要点）

- 本工具用于客户端 QoL Mod 管理，不支持也不鼓励破坏对战公平的修改  
- 修改战斗模拟可能导致 Data Error 与处罚  
- 游戏官方未提供 Mod 支持；使用第三方加载器与 Mod 风险自负  
- MelonLoader 需用户自行安装；本管理器一期不代装  
- 风险标记依赖用户与作者声明，**不能保证**检出全部危险 Mod  

---

## 10. 测试策略

1. **单元测试：** 路径/Loader 检测分级、Profile JSON、部署差异计算、拍平路径生成、同名冲突判定、UserData 保护规则（含 `Loader.cfg`）、manifest 与 gamePath 绑定失效  
2. **集成测试：** `_samples/QuickCamera` → 导入 → 勾选写入方案 → 应用后 `{Game}/Mods/QuickCamera.dll` 存在且无子目录 → 换空方案后托管文件移除  
3. **手工测试：** Loader 缺失、进程占用、非托管同名冲突、Steam/直启回退、改 gamePath 后清单失效、UserData 包不碰 `Loader.cfg`  

---

## 11. 工程结构（实现时）

```
src/
  MechabellumModManager/              # WPF 应用
    Views/
    ViewModels/
    Services/
      GameDetector.cs
      ModLibraryService.cs
      ProfileService.cs
      DeployService.cs
      GameLauncher.cs
      RiskGate.cs
    Models/
  MechabellumModManager.Tests/
```

一期保持单体；若测试需要再抽 `Core` 项目（非必须）。

---

## 12. 成功标准（一期完成定义）

1. 能检测默认或用户指定目录，并区分游戏无效 / Loader 缺失或不完整 / Ready  
2. 能导入 `QuickCamera.zip`/`QuickCamera.dll` 并登记为 `melon_mod`  
3. 应用后文件位于 `{Game}/Mods/QuickCamera.dll`（根目录），不是子文件夹  
4. 能创建至少两套方案并切换；托管文件随方案增删，**不删**非托管手工文件  
5. 非托管同名默认不覆盖；UserData 同步不触碰 `Loader.cfg`  
6. gamePath 变更后不按旧 manifest 误删  
7. 「应用并启动」在 Ready 时可拉起游戏（Steam 优先）  
8. UI 含联网 PVP 风险提示；无作弊功能入口  

---

## 13. 已确认决策摘要

| 决策点 | 结论 |
|---|---|
| 形态 | Windows 桌面 GUI |
| MelonLoader 安装 | 不代装（用户自备） |
| 类型预留 | Mod / Plugin / UserLibs / UserData（各为独立条目）；排除作弊与脆弱资源包 |
| 技术栈 | .NET 8 + WPF |
| 存储与启用 | 外部库 + 同步部署 |
| Profile | 一期就做；勾选立即存方案，「应用」才部署 |
| 部署介质 | 文件复制；必须拍平到根目录 |
| 启动 | Steam 优先，exe 回退 |
| 总体方案 | 精简 WPF 单体 |

---

## 14. 修订记录

**2026-09-02 自检修订：** 补充拍平部署、同名冲突、UserData/`Loader.cfg` 保护、manifest↔gamePath 绑定、Mod/Plugin 识别、勾选 vs 应用职责、回滚算法、UserLibs、导入规范化、Loader 检测增强、版本警告与已知限制。  

**2026-09-02 最终自检：** 统一 `package.json` 命名；明确 UserLibs 为独立条目、禁止从 melon_mod 隐式分流；澄清可部署文件过滤、modId 生成、删库与卸下、切方案不自动部署、空方案清托管、首次部署回滚。

---

## 15. 下一步

用户确认本修订稿后，编写实现计划：  
`docs/superpowers/plans/2026-09-02-mechabellum-mod-manager.md`
