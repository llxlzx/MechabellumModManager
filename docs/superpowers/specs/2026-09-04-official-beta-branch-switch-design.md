# 正式服 / 测试服双目录一键切换 — 设计规格

> **说明**  
> 本文档描述嵌在 Mechabellum Mod 管理器设置页中的「正式服 / 测试服」切换功能。路径均为示例，请按本机 Steam 库调整。

**日期：** 2026-09-04  
**状态：** 规格待维护者通读确认（已含自检补漏 + 静默对齐 Beta 联合自查修订）  
**前置规格：** `docs/superpowers/specs/2026-09-02-mechabellum-mod-manager-design.md`  
**Steam AppID：** `669330`

---

## 0. 联合自查结论（改「静默对齐」之前）

对照：**现有管理器代码行为** × **原半自动规格** × **拟改为静默对齐 Beta**。

### 0.1 现有管理器里会碰到的点

| 模块 | 现状 | 静默对齐若处理不当的风险 | 隔离要求 |
|------|------|--------------------------|----------|
| `GameLauncher`（`steam://rungameid/669330` / ExeOnly） | 不关心 Beta | Beta 未对齐时用 Steam 启动会触发校验写穿；ExeOnly 可能绕过 Steam 却留下「目录已切、Steam 仍认旧分支」 | 双服就绪且切服流程未完成时禁用启动；双服启用后切服结束前不以 ExeOnly 作为推荐路径 |
| `DeployService` / `IsReady` | 只查游戏进程与 Loader | Steam 仍在校验/下载时 Deploy 可能锁文件或与校验竞态 | 切服后须等 Steam 空闲（或超时降级）再 Deploy；Deploy **永不**调用 Beta 写入 |
| `ProcessProbe` | 仅 `Mechabellum` | 不知 Steam 是否退出 → 写 appmanifest 极危险 | 扩展检测 `steam`；写 Beta / 切 Junction **仅当 Steam 与游戏均未运行** |
| `PathsService` / 单清单 | 一份 `deploy-manifest.json` | 与双旁路冲突（已有结论） | 分支清单 + `.prev`；与 Steam 配置读写无关 |
| `AppConfig` / `MainViewModel` | 巨石式配置保存 | 把 Beta 名、acf 路径塞进 `AppConfig` 会污染主配置、增加启动失败面 | Beta 映射与 journal 只进 `branch-switch.json`；解析失败不得阻止管理器主界面启动 |
| `SteamGameLocator` | 启动时可能改写 `GamePath` | 向导中路径空洞时「纠正」到错误位置 | 向导未完成时禁止自动改路径 |
| MelonLoader 安装/检测 | 写当前 `GamePath` | 只装到当前旁路是预期；勿当成全局失败 | 缺 Loader 只影响 `IsReady`/Deploy，不回滚 Junction、不回滚 Beta 写入 |
| 权限 | 库常在 Program Files | 改名与写 `appmanifest` 都可能 Access Denied | 失败则提示提权/迁库；**不得**部分成功却显示就绪 |

### 0.2 静默对齐本身的风险（相对半自动）

| 风险 | 说明 | 缓解 |
|------|------|------|
| Steam 配置格式变化 | `appmanifest_*.acf` 的 `UserConfig.BetaKey` 等字段可能变更或被客户端覆盖 | 写入前备份；启动后校验；失败则**降级半自动手选**，功能不拖垮管理器 |
| 写错文件 | 误改其它 AppID | 硬编码仅 `appmanifest_669330.acf`；路径必须落在 `GamePath` 所在库的 `steamapps\` |
| Steam 运行中写入 | 被覆盖或损坏清单 | 进程门禁；禁止热写 |
| 测试服分支名未知 | 各时期 Beta 名可能不同 | 向导中由用户选择/粘贴分支名，写入 `BetaBranchName`；正式服用空 BetaKey（或等价「无 Beta」） |
| 账号级 localconfig | 仅改 acf 可能不够 | 先做 acf；若重启后仍不对齐，降级手选并记录；本期不承诺改全用户 `localconfig.vdf` |
| 大额误下载 | 对齐失败导致写穿当前旁路 | 重启 Steam 后若检测到明显下载/校验风暴 → 警告并**暂缓 Deploy**；提示重建向导 |

### 0.3 对「管理器本体」的红线（必须遵守）

1. **主路径零依赖**：不启用双服、或 Beta 写入失败时，Mod 库 / Profile / Deploy / 更新检查行为与今日一致。  
2. **故障隔离**：`BranchSwitchService` / `SteamBetaKeyEditor` 异常只影响双服区块，不得导致 `App` 启动失败或清空 `config.json`。  
3. **职责不串联**：`DeployService`、`GameLauncher`、`ModLibraryService` **不**直接读写 Steam 清单。  
4. **可降级**：静默对齐失败 → 同一套 Junction 流程 + 原半自动手选 Beta，而不是卡死。  
5. **可逆**：写 Beta 前备份 acf；解除双服时不留下半残 Steam 配置（恢复切服前备份或明确提示用户在 Steam 里再选一次分支）。

### 0.4 自查结论

**可以改成「静默对齐优先」**，前提是把上述隔离与降级写进规格；否则会把 Steam 配置脆弱性扩散进管理器核心。  
下方正文已按此结论修订（不再「绝不改 appmanifest」，改为「仅 Steam 退出时、仅 669330、可降级」）。

---

## 1. 背景与目标

### 1.1 问题

Steam 下正式服与测试服（Beta 分支）共用同一安装目录。切换 Beta 时 Steam 就地改写文件，上一分支本地文件被覆盖；再切回去往往需要重新下载数 GB。

### 1.2 目标

1. 在现有 Mod 管理器**设置页**增加「正式服 / 测试服」能力  
2. 用**双完整游戏目录 + Junction**保留两服文件，日常切换避免整包重下  
3. **静默对齐优先**：Steam 完全退出后，工具切换 Junction **并**写入目标 Beta 标记，再拉起 Steam；失败则降级为手选 Beta  
4. 两服各绑定一套 Profile；对齐完成后自动应用对应方案  
5. 与现有 Deploy / 清单模型兼容，且**按分支隔离**托管文件清单  
6. **不破坏**未启用双服时的管理器既有行为  

### 1.3 非目标（本期不做）

- Steam **运行中**热改 Beta / 热切 Junction  
- 承诺解析并改写所有用户的 `localconfig.vdf` / 注册表（acf 尽力 + 降级即可）  
- 差异文件备份、按块去重省盘  
- 一键代为执行测试服内容更新（仍用 Steam；只保证更新写到正确旁路）  
- 非 Steam 版、非 NTFS、跨盘 Junction  
- 自动把 MelonLoader 复制到另一旁路  
- 云同步两服存档 / 战绩  
- 做成游戏内 Melon Mod  

### 1.4 产品形态结论

桌面工具功能（非 Melon Mod）：双完整目录 + Junction；嵌入设置；**静默对齐 Beta（可降级）**；每服绑定 Profile；Steam 配置读写与 Deploy/启动严格隔离。

---

## 2. 防写错服（硬约束）

Steam 会沿着 `...\steamapps\common\Mechabellum` **跟随 Junction 写入**。Junction 与 Beta 标记不一致时，校验/下载会写穿当前旁路。

### 2.1 首次向导顺序（必须）

0. **声明当前服** + **录入测试服 Beta 分支名**（用户从 Steam 属性里看到的名称；可粘贴）。正式服对应「无 Beta / 空 BetaKey」。不硬编码字符串。  
1. **预检**：`GamePath` 有效且位于某库 `steamapps\common`；NTFS；旁路名未占用；**游戏与 Steam 均已退出**；磁盘能再下一份；对 `appmanifest_669330.acf` 有读权限（写权限在静默步骤再测）。Access Denied → 提示提权或迁库。  
2. **解析后再归档 A**（真实目录改名；若已是 Junction 则先解析真路径，禁止只挪联接）。  
3. **不得**建指回 A 的 Junction；`Mechabellum` 必须不存在。  
4. **拉另一服 B**：可选——（推荐）Steam 退出时静默写入「另一服」Beta → 再启动 Steam 下载；失败则提示用户手选 Beta 后下载。Steam 在空位置新建 `Mechabellum`，写 B，动不到 A。  
5. **归档 B**：退出 Steam → `LooksLikeGameRoot` → 改名为另一旁路。  
6. **建联接**到用户选择的当前服；断开 Junction **不得**递归删旁路。  
7. **静默对齐当前服 Beta**（Steam 仍退出）→ 启动 Steam → 等待短校验 / 无异常大下载。  
8. **收尾**：检测 Loader；绑定 Profile；`Enabled=true`；写入 `BetaBranchName` 等；就绪。

中途取消：`WizardStep` + journal 可恢复；禁用 Deploy/启动；禁止 Locator 乱纠正路径。

### 2.2 日常切换顺序（必须，玩家侧尽量一键）

玩家：确认（或允许管理器请求）退出 Steam/游戏 → 点「切换到××服」。

工具：

1. 门禁：游戏与 Steam 未运行；否则提供「请求退出 Steam」（如 `steam://exit`）并等待进程消失，超时则中止。  
2. **备份** `appmanifest_669330.acf`（及将写入的其它约定文件）。  
3. 切换 Junction → 目标旁路；更新 `ActiveBranch`；UI 选中绑定 Profile。  
4. **静默写入**目标 BetaKey（正式服清空/移除 BetaKey；测试服写 `BetaBranchName`）。  
5. 启动 Steam；进入 `AwaitingSteamSettle`（禁用 Deploy/启动）。  
6. 观察：短时校验可接受；若出现异常大下载/长时间更新 → 警告写穿风险，**不自动 Deploy**，引导手选 Beta 或重建。  
7. 结算成功 → 检测 Loader → `ActiveProfileId` + `Deploy` → 清除等待态。  
8. 若步骤 4 失败：回滚 Beta 备份（若已写）；**保留或回滚 Junction 策略见 §4.3**；降级 UI「请手选 Beta」后用户确认再 Deploy。

**禁止**：Steam 运行中改 acf 或切 Junction。

### 2.3 测试服更新约定

仅当 Junction 与 Steam Beta 均指向测试服时用 Steam 更新；写入测试服旁路。正式服同理。

---

## 3. 设置页 UI

| 控件 | 作用 |
|------|------|
| 当前分支 | `正式服` / `测试服` / `未配置` / `配置未完成` / `等待 Steam` |
| 测试服 Beta 分支名 | 可编辑；向导录入；供静默写入 |
| 正式服 / 测试服方案下拉 | 绑定 Profile |
| **切换到正式服** / **切换到测试服** | 一键主路径（内含静默对齐） |
| **开始双服配置向导** / **重建** | 见下 |
| **解除双服配置** | 还原单目录 |
| 说明 | 约两份游戏空间；须退 Steam；将尝试自动对齐 Beta，失败可手选 |

**重建**：先安全解除（当前旁路改回真目录），再跑 §2.1。  
文案走 i18n。不在无关操作里退出 Steam。

---

## 4. 目录与 Junction

### 4.1 路径约定

| 路径 | 含义 |
|------|------|
| `...\Mechabellum` | 对外路径；就绪后为 Junction |
| `...\Mechabellum_official` | 正式服真目录 |
| `...\Mechabellum_beta` | 测试服真目录 |

`GamePath` 始终为 Junction 路径。仅 NTFS 同卷。

### 4.2 切换原子步骤（目录层）

1. 预检旁路 `LooksLikeGameRoot`、Steam/游戏未运行。  
2. 断 Junction（不删旁路内容）。  
3. 建 Junction → 目标。  
4. 校验游戏根。  
5. 更新 `ActiveBranch`；进入与 Beta/Steam 相关的后续步骤（§2.2）。  
6. 目录步骤失败 → 恢复旧 Junction，不写 Beta、不 Deploy。

### 4.3 失败回滚（目录 × Beta 联合）

| 失败点 | 目录 | Beta 配置 | Deploy |
|--------|------|-----------|--------|
| Junction 失败 | 恢复旧联接 | 不改 | 不 |
| Junction 成功、写 Beta 失败 | **保持新 Junction** | 恢复备份；降级手选 | 待用户确认后 |
| 写 Beta 成功、Steam 异常大下载 | 保持新 Junction | 保持新 Beta（或提示手改） | **暂缓** |
| 对齐成功、Deploy 失败 | 保持 | 保持 | 显示未同步 |

原则：避免「Steam 已按新 Beta 启动，目录又被切回旧旁路」的错位。journal 记录每步；启动时可修复空洞联接。

### 4.4 解除双服

断 Junction → 当前旁路改名回 `Mechabellum` → 询问是否删另一旁路（默认不删）→ 当前分支清单拷回 `deploy-manifest.json` → 清分支配置。  
Beta：恢复解除前备份或提示用户在 Steam 中再选分支；不强制写死正式服。

---

## 5. 配置模型与服务拆分

### 5.1 `branch-switch.json`（勿塞进 `AppConfig`）

```text
BranchSwitchConfig
  Enabled
  WizardStep             // ... | AwaitingSteamSettle | Ready
  SteamLinkPath
  OfficialStorePath
  BetaStorePath
  ActiveBranch           // Official | Beta
  OfficialProfileId
  BetaProfileId
  BetaBranchName         // 测试服 Steam 分支名
  ManifestBackupPath     // 最近一次 acf 备份路径（或备份目录约定）
```

- 主程序启动：读取失败则视为未启用双服，**忽略并打日志**，不得抛穿 `OnStartup`。  
- 双服启用后 `GamePath` 必须指向 `SteamLinkPath`。  
- 绑定 Profile 删除：中止 Deploy并提示；目录/Beta 不自动回滚。  
- 允许两服绑定同一 ProfileId。  
- journal 文件锁，避免双实例并发危险步。

### 5.2 Deploy 清单按分支隔离

| 文件 | 用途 |
|------|------|
| `deploy-manifest.official.json` + `.prev.json` | 正式服 |
| `deploy-manifest.beta.json` + `.prev.json` | 测试服 |
| `deploy-manifest.json` + `.prev.json` | 单安装；启用时迁移到当前分支 |

`PathsService` 按 `ActiveBranch` 解析路径。`DeployService` 不感知 Steam Beta。

### 5.3 服务与隔离

| 服务 | 职责 | 禁止 |
|------|------|------|
| `BranchSwitchService` | 向导、Junction、解除、journal、编排切服 | 改 Mod 库 |
| `SteamBetaKeyEditor` | 仅 Steam 退出时备份/写/校验 `appmanifest_669330.acf` | 写其它 AppID；Steam 运行时写入 |
| `ProcessProbe` | 游戏 + steam | — |
| `PathsService` | 分支 manifest 路径 | — |
| `DeployService` | 只 Deploy | 调 Beta 编辑器 |
| `GameLauncher` | 只启动 | 调 Beta 编辑器；切服未完成时由 VM 禁用 |

### 5.4 Profile 与启动

- 设置下拉只改绑定。  
- 切服结算成功后：`ActiveProfileId` + Deploy。  
- 主界面换方案 → 写回当前服绑定。  
- `AwaitingSteamSettle` / 向导中：禁用应用方案、应用并启动、启动。  
- 双服启用时，切服流程内不推荐 ExeOnly 绕过 Steam；设置里可保留 ExeOnly 供高级用户，但切服向导文案声明风险。

---

## 6. MelonLoader 与存档

- Loader 随旁路隔离；缺则提示安装，不回滚目录/Beta。  
- 存档在游戏目录则隔离；在 AppData/Cloud 则可能共享——本期不拆分，仅提示。

---

## 7. 错误态

| 情况 | 行为 |
|------|------|
| Steam/游戏未退出 | 中止；可请求退出并等待 |
| 写 acf Access Denied / 解析失败 | 备份保留；降级手选 Beta |
| Junction 失败 | 恢复旧联接 |
| 旁路损坏 | 中止；重建向导 |
| 异常大下载 | 暂缓 Deploy；强警告 |
| Deploy 失败 / Profile 已删 | 保持目录与 Beta；未同步 |
| 编辑器异常 | 仅双服区块报错；管理器可继续管 Mod |

---

## 8. 测试要点

1. 未启用双服：回归库/方案/Deploy/启动与改前一致。  
2. 启用双服但故意让 acf 写入失败：降级手选；管理器不崩溃；主配置完好。  
3. 静默成功：一键切服后 Beta 与 Junction 一致，无整包重下；对应分支清单 Deploy 正确。  
4. Steam 未退出时写入/切目录必须失败。  
5. 仅允许改 669330 的 manifest；单测覆盖路径守卫。  
6. 向导 Junction 起始、崩溃空洞联接修复、解除/重建。  
7. `AwaitingSteamSettle` 下无法启动/Deploy。  
8. Program Files 无权限时提示清晰。  

---

## 9. 与现有管理器的衔接摘要

| 现有能力 | 衔接 |
|----------|------|
| 主配置 / 启动 | 不依赖 Beta 编辑成功 |
| Deploy | 只吃分支清单；切服编排在 VM/BranchSwitch |
| Locator | 向导中禁止乱纠正 |
| i18n / 设置折叠区 | 新文案与控件 |
| 风险声明 | 说明会备份并可能改 Steam 本地清单（仅本游戏） |

---

## 10. 已确认决策记录

| 决策 | 选择 |
|------|------|
| 方案形态 | 双完整目录 + Junction |
| 产品入口 | 嵌入设置页 |
| Steam Beta | **静默对齐优先；失败降级手选** |
| 写 Steam 配置 | **仅 Steam 退出时；仅 AppID 669330；先备份** |
| Mod 方案 | 每服绑定；结算后 Deploy |
| 主界面换方案 | 写回当前服绑定 |
| 与管理器核心 | 故障隔离；主路径零依赖双服 |

---

## 11. 范围一句话

一键切换正式/测试服：退 Steam → 切 Junction → 静默写 Beta → 拉起 Steam 结算 → 按服 Deploy；失败可手选降级；Steam 配置逻辑与 Mod 部署严格隔离，避免拖垮管理器本体。

---

## 12. 修订纪要

- 自检补漏：Junction 真路径、Awaiting 态、分支 `.prev`、重建、存档提示等。  
- 联合自查后：半自动改为静默对齐优先；新增 §0 红线与 `SteamBetaKeyEditor` 隔离；手选降级保留。
