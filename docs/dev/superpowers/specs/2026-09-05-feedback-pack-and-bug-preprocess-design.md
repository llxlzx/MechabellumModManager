# 群主反馈整包改造 + Bug 预处理设计

> **状态**：已自检并批准开工（2026-09-05）  
> **日期**：2026-09-05  
> **仓库**：Mechabellum Mod Manager  
> **用户确认**：Phase 0→1→2→3；Melon 缺程序集采用方案 **B**（管理器尝试触发/等待生成）

---

## 自检记录

- 修正验收 §10.4 笔误：确认框为 **是左 / 否右**（与 §2.3、§4.5 一致）。
- 方案 B 与「不同步 Il2CppAssemblies」不矛盾：跨服不拷程序集，本服由管理器触发生成。
- 发版默认 **1.0.8**（避免与旧 1.0.7 行为混淆）。
- Phase 2「应用方案」高亮：以「有可应用变更或已多选」为准，语义仍走现有 `ApplyProfile`。

---

## 1. 背景与目标

群主反馈（`Desktop.zip` + 聊天截图）要求：

- 安装器：详细风险/用户协议 + 勾选同意后才能安装
- 主界面：多选、Mod 浏览显式折叠与外框、「应用方案」位置与灰→橙
- 双服向导：Access Denied 可理解处理、是/否对调、当前服直接选正式/测试
- Melon/Mod 未生效：先根因治理再加功能

**非目标（本轮不做）**

- 重做整套 UI 皮肤 / 模仿其他管理器像素级布局
- 改 Steam 官方行为（强制零下载）
- 自动提权 UAC 静默绕过（可引导用户管理员重开，不静默提权）

---

## 2. Bug 预处理结论（已调查）

### 2.1 Melon / Mod 未生效（多因）

| 因 | 证据 / 代码 | 影响 |
|----|-------------|------|
| A. 双服同步跳过 `Il2CppAssemblies` | `MelonLoaderDualStoreSync.SkipDirNames` | 新一侧 Loader「看起来有」但程序集未生成 |
| B. 同步复制了 `Latest.log` | 正式/测试服 `Latest.log` 哈希相同 | 排障误判「已经加载过」 |
| C. `GameDetector` Ready 过宽 | 只查 Melon 目录 + `version.dll`/`winhttp.dll` | UI 显示就绪，实际未可玩 |
| D. 缺 `Config.cfg` 等运行痕迹 | 正式服仅有框架文件 | 该服可能从未成功完成 Melon 首次引导 |
| E. 启动路径 | `LaunchMode` 可为 SteamOnly | 极端情况下绕过注入（次要） |
| F. 外部环境 | 群主机空 0KB 日志、`Failed to load MelonLoader.dll` | 杀软/.NET/损坏 DLL（需检测+提示） |

**方案 B（已选）**：管理器在 Deploy/启动/切服后，若判定「Loader 在但 Il2Cpp 未就绪」，则**主动触发生成并等待**（带超时与进度），而不是只弹一句「请自行首次启动」。

### 2.2 双服向导 Access Denied

- 失败点：`ArchiveCurrentAs` → `Directory.Move`
- 现况：`catch` 只回传英文 `Access to the path ... is denied.`
- 真因候选：Steam 句柄未放、杀软、ACL、只读属性
- 缺口：Move 前可写预检、分类人话、建议步骤；失败勿假装成功

### 2.3 确认框 / 当前服提问

- `ConfirmDialog`：`否` 左、`是` 右 → 对调为 **`是` 左、`否` 右**
- 「当前安装的是正式服吗？」→ 改为 **TypePick / 双按钮：正式服 | 测试服**（非是否题）

---

## 3. 总体分期

| Phase | 内容 | 完成标准 |
|-------|------|----------|
| **0** | Melon 健康检测 + 触发生成；向导 Access Denied；确认框/当前服 UX | 单测 + 本机正式服可补齐 Il2Cpp；向导失败有人话 |
| **1** | 安装器 License 详细文案 + 必须同意 | Setup 未勾选无法 Next |
| **2** | 多选、浏览折叠/外框、应用方案位置与状态色 | 主界面交互符合群主标注 |
| **3** | 回归：切服/Deploy/Melon/安装包；刷新 1.0.7 或 bump 小版本发版 | 测试绿；Release 可下载 |

**硬规则**：未完成 Phase 0 合入前，不合并 Phase 2 大 UI；Phase 1 可与 0 尾部并行但发版以 0 通过为前提。

---

## 4. Phase 0 设计

### 4.1 Melon 健康模型

扩展检测（建议新类型或扩展 `GameStatus`）：

```
LoaderMissing | LoaderPartial | LoaderPresentAssembliesMissing | Ready | MelonDllUnloadable
```

**Assemblies 就绪条件（务实）**：

- 存在 `MelonLoader/Il2CppAssemblies` 目录，且至少含 `Assembly-CSharp.dll`（或项目已用的等价标记文件）
- 可选：`MelonLoader/net6/MelonLoader.dll` 可被加载版本信息（已有）

**Ready 收紧**：原「有目录+proxy」不再等于可玩；缺程序集归 `LoaderPresentAssembliesMissing`。

### 4.2 同步策略修正

`MelonLoaderDualStoreSync`：

- **继续跳过** `Il2CppAssemblies`（跨 build 不安全）
- **新增跳过** 根级与日志：`Latest.log`、`Logs`（已有）、`CrashReports`（已有）
- 复制后若目标缺程序集 → 状态标为 AssembliesMissing，并进入生成管线（见 4.3）
- 成功文案明确：「已复制框架；将触发程序集生成」

### 4.3 方案 B：触发 / 等待生成

**触发方式（优先序）**：

1. **托管静默启动游戏 exe 一次**（`ExeOnly` 路径，工作目录=该服 store），注入依赖 `version.dll` 已在目录内 → Melon 正常走 Il2CppAssemblyGenerator
2. 轮询直到：
   - `Il2CppAssemblies/Assembly-CSharp.dll` 出现且稳定（大小/时间连续两次不变），或
   - `Latest.log` 出现本机新时间戳且含 `Assembly is up to date` / `Loading Mods` / 生成完成关键字，或
   - 超时（建议默认 **180s**，可配置常量）
3. 超时：杀掉该次拉起的游戏进程（仅管理器拉起的 PID），提示用户手动开一次并保留日志路径
4. **绝不要**在 Steam 仍持有更新锁时对「正在下载的 store」强制生成

**调用点**：

- `EnsureOnBothStores` / `EnsureOnStore` 成功且 AssembliesMissing 之后
- `SwitchToBranchAsync` 切服 + Melon sync 之后（目标服）
- 「应用并启动」前：若 AssembliesMissing，先跑生成再 Deploy/启动

**UI**：

- 日志区追加进度：「正在为正式服生成 Melon 程序集…」
- 阻塞型短对话框可接受（带取消=中止等待并杀进程）

**风险控制**：

- 只杀我们 Start 的进程树，不杀用户已开的 Steam 全局
- 生成中禁用切服/Deploy
- 单测：用假文件系统断言「缺 assemblies → 调用 generator 钩子」；真实启动用集成可选测

### 4.4 向导 Access Denied

在 `ArchiveCurrentAs` / `ArchiveDownloadedAs` 前：

1. 再次确认 Steam+游戏+steamwebhelper 退出（沿用现有 Wait）
2. 对 `link` 目录做可写探测（临时文件 create/delete）
3. `Directory.Move` 失败映射：
   - UnauthorizedAccess / IOException 含 denied → 中文：退出 Steam、关杀软对该目录的占用、必要时管理员运行管理器、检查只读
4. FailWizard 使用映射后文案，并写日志

### 4.5 确认框与当前服选择

- `ConfirmDialog`：按钮顺序改为 **是 | 否**（是在左，否在右）；默认焦点仍可按 `defaultYes`
- 新增或复用 `TypePickDialog`：两项「正式服」「测试服」；向导 `ConfirmCurrentIsOfficial` 替换为该选择
- 所有「开始双服？」类确认框同步受益于按钮对调

---

## 5. Phase 1 — 安装器协议

- 新增 `installer/EULA.zh-CN.txt`（或 `.rtf`）：在现有一句话基础上扩写
  - 仅客户端 QoL
  - 改战斗逻辑 → Data Error / 处罚风险
  - 官方不支持 Mod，风险自负
  - 管理器/双服/Melon 相关责任边界（简短）
- Inno：`LicenseFile=EULA.zh-CN.txt`
- 使用 Inno 标准 License 页（必须接受才能 Next）；必要时 `DisableDirPage` 等保持现状
- 去掉或保留欢迎页短风险条：避免重复可改为「详见下一页用户协议」

---

## 6. Phase 2 — 主界面

### 6.1 多选

- 本地方案/已启用列表（群主标注勾选列）：`ListView`/`DataGrid` `SelectionMode=Extended`，保留行内启用 CheckBox
- 明确多选作用对象：**本地已安装/方案内 Mod 勾选批量开关**（与目录浏览区分）
- 批量：多选后「应用方案」才高亮（见 6.3）

### 6.2 Mod 浏览折叠

- 分区标题旁增加折叠按钮（▼/▶），与顶部「Mod 浏览」切换并存
- 展开态外框：`Border` 包住浏览区，表示「由按钮展开的面板」

### 6.3 应用方案按钮

- 位置：移到本地列表上方（群主箭头区域），离开右上角拥挤区
- 状态：默认灰禁用；**多选至少一个 Mod 或方案相对上次有变更可应用** 时高亮橙色（对齐现有 `AccentButtonStyle`）
- 语义保持：执行现有 `ApplyProfile` Deploy，不新造第二套 Deploy

---

## 7. Phase 3 — 回归与发版

- 单测：Detector、Sync 跳过日志、Confirm 顺序（若可测）、向导选服、Archive 错误映射
- 手工：正式服缺 Il2Cpp → 管理器生成成功；切测服不误删 assemblies；Setup License 闸门；多选应用
- 发版：优先继续 **1.0.7 clobber** 或按你方习惯 bump **1.0.8**（实施计划里二选一，默认 1.0.8 以免与旧 1.0.7 行为混淆）

---

## 8. 主要改动文件（预期）

| 区域 | 文件 |
|------|------|
| Melon | `GameDetector.cs`、`MelonLoaderDualStoreSync.cs`、新 `MelonLoaderAssemblyGenerator.cs`（或同名服务）、`MainViewModel.cs`、`GameLauncher.cs` |
| 向导 | `BranchSwitchService.cs`、`MainViewModel.cs`、`TypePickDialog` / 文案 resx |
| 确认框 | `ConfirmDialog.xaml(.cs)`、`App.xaml.cs` |
| 安装器 | `MechabellumModManager.iss`、`installer/EULA.zh-CN.txt` |
| UI | `MainWindow.xaml`、`MainViewModel.cs`、`Strings*.resx` |
| 测试 | `GameDetector`/`Melon*`/`BranchSwitch*`/`MainViewModelBranchSwitch*` |

---

## 9. 风险与回滚

| 风险 | 缓解 |
|------|------|
| 生成时拉起游戏被反作弊/用户惊吓 | 明确提示「将短暂启动游戏以生成程序集」；可取消 |
| 超时杀进程误伤 | 仅杀本次 Process.Start 返回的 PID |
| 生成失败循环 | 每服每会话最多自动尝试 1 次，失败改手动指引 |
| UI 大改回归 | Phase 0 先合；Phase 2 独立提交 |

---

## 10. 成功标准（验收）

1. 正式服无 `Il2CppAssemblies` 时，管理器能在超时内生成或给出可操作失败原因（不再只显示 Ready）
2. 双服同步不再污染 `Latest.log`；Detector 区分「缺程序集」
3. Access Denied 显示中文步骤，不再只有英文路径句
4. 确认框**是**左**否**右；向导直接选正式/测试
5. Setup 未同意协议无法安装
6. 多选 + 折叠外框 + 应用方案位置/灰橙 符合群主标注
7. 相关单测通过；Release 包可下载

---

## 11. 请用户审阅

请确认本设计文档无异议（或标注要改的段落）。  
**批准后**再写 `docs/superpowers/plans/2026-09-05-feedback-pack-impl.md` 并按 Phase 0 开工。
