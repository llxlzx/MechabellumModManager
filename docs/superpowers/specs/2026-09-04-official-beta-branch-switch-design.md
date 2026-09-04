# 正式服 / 测试服双目录一键切换 — 设计规格

> **说明**  
> 本文档描述嵌在 Mechabellum Mod 管理器设置页中的「正式服 / 测试服」半自动切换功能。路径均为示例，请按本机 Steam 库调整。

**日期：** 2026-09-04  
**状态：** 规格待维护者通读确认  
**前置规格：** `docs/superpowers/specs/2026-09-02-mechabellum-mod-manager-design.md`  
**Steam AppID：** `669330`

---

## 1. 背景与目标

### 1.1 问题

Steam 下正式服与测试服（Beta 分支）共用同一安装目录。切换 Beta 时 Steam 就地改写文件，上一分支本地文件被覆盖；再切回去往往需要重新下载数 GB，尽管用户曾经下过。

### 1.2 目标

1. 在现有 Mod 管理器**设置页**增加「正式服 / 测试服」能力  
2. 用**双完整游戏目录 + Junction**保留两服文件，日常切换避免整包重下  
3. **半自动**：工具只安全切换目录联接；Steam Beta 由用户在 Steam 属性中选择并对齐  
4. 两服各绑定一套 Profile；切换确认后自动应用对应方案  
5. 与现有 Deploy / 清单模型兼容，且**按分支隔离**托管文件清单  

### 1.3 非目标（本期不做）

- 自动修改 Steam `appmanifest` / Beta 相关配置或注册表  
- 不退出 Steam 的「热切换」  
- 差异文件备份、按块去重省盘  
- 一键代为执行测试服更新（仍用 Steam；只保证更新写到正确旁路）  
- 非 Steam 版、非 NTFS、跨盘 Junction  
- 自动把 MelonLoader 复制到另一旁路  
- 云同步两服存档 / 战绩  
- 做成游戏内 Melon Mod  

### 1.4 产品形态结论

这不是 MelonLoader Mod，而是管理器内的**桌面工具功能**。推荐实现为：双完整目录 + Junction；嵌入设置页；半自动对齐 Beta；每服绑定 Profile。

---

## 2. 防写错服（硬约束）

Steam 会沿着 `...\steamapps\common\Mechabellum` **跟随 Junction 写入**。若联接仍指向要保留的旁路，却在 Steam 中切换/下载另一 Beta，会**写穿**该旁路。

### 2.1 首次向导顺序（必须）

1. **预检**：`GamePath` 有效；卷为 NTFS；旁路目标名未被无关目录占用；**游戏与 Steam 均已退出**；磁盘策略与所选归档方式一致（本规格默认「改名归档」，不要求额外 1× 整包空闲，但仍需满足 Steam 下载另一服的空间）。  
2. **归档当前服 A**：将真实目录 `Mechabellum` **改名**为旁路（当前为正式服 → `Mechabellum_official`；为测试服 → `Mechabellum_beta`）。  
3. **此时不得**创建指回 A 的 Junction。`...\Mechabellum` **必须不存在**，以便 Steam 在空位置新建另一服。  
4. **拉另一服 B**：提示用户启动 Steam → 属性 → Beta 选另一分支 → 等待完整下载（若库中显示需「安装」而非更新，按 Steam 提示安装到原库位置即可）。Steam 新建 `Mechabellum` 并写入 B，**动不到**已改名的 A。  
5. **归档 B**：下载完成后**退出 Steam** → 将新建的 `Mechabellum` 改名为另一旁路。  
6. **建联接**：按用户选择的「当前要玩的服」创建 Junction：`Mechabellum` → 对应旁路。  
7. **对齐 Beta**：再开 Steam，确认 Beta 与 Junction 目标一致，预期仅校验、不应整包重下。  
8. **收尾**：检测当前服 MelonLoader；绑定两套 Profile；写入分支配置；标记就绪。

中途取消：停在 journal 可描述的最后安全点；设置页显示「配置未完成」与下一步，禁止在路径空洞时显示「已就绪」。

### 2.2 日常切换顺序（必须）

1. 管理器检测：**退出游戏 + 完全退出 Steam**；未退出则**中止**（默认不可强行继续）。  
2. 切换 Junction → 目标旁路。  
3. 提示：启动 Steam → **立刻**将 Beta 设为与目标一致 → 等待校验结束。  
4. 用户确认「我已对齐 Beta」→ 检测游戏根 / Loader → 同步 `ActiveProfileId` 为该服绑定方案并 `Deploy`。

**禁止**的危险顺序：Steam 仍在运行且 Junction 仍指旧旁路时，先改 Steam Beta（会写穿旧副本）。

### 2.3 测试服更新约定

仅在「当前 Junction 已指向测试服旁路，且 Steam Beta 亦为测试服」时允许 Steam 更新。更新写入测试服旁路；正式服旁路不动。正式服同理。

---

## 3. 设置页 UI

设置中新增可折叠区块「正式服 / 测试服」：

| 控件 | 作用 |
|------|------|
| 当前分支 | `正式服` / `测试服` / `未配置` / `配置未完成` |
| 正式服方案下拉 | 绑定现有 Profile |
| 测试服方案下拉 | 绑定现有 Profile |
| **切换到正式服** / **切换到测试服** | 日常入口；未就绪时禁用 |
| **开始双服配置向导** | 首次或重建 |
| **解除双服配置** | 还原单一 Steam 目录 |
| 说明文案 | 就绪后磁盘约为两份完整游戏；须退出 Steam；Beta 需手动对齐 |

不自动改 Steam 配置、不后台强杀 Steam（只检测并提示退出）、不静默整包复制（向导默认改名归档）。

---

## 4. 目录与 Junction

### 4.1 路径约定

默认均在同一 Steam 库的 `steamapps\common\` 下：

| 路径 | 含义 |
|------|------|
| `...\Mechabellum` | 对外唯一路径：配置完成后为 **Junction** |
| `...\Mechabellum_official` | 正式服完整副本（真实文件夹） |
| `...\Mechabellum_beta` | 测试服完整副本（真实文件夹） |

- 管理器 `GamePath` **始终**指向 `...\Mechabellum`（Junction），不指向旁路真目录。  
- 旁路名固定；若同名目录已被非本功能占用，向导中止。  
- 仅支持 **NTFS 同卷** Junction；否则向导拒绝（不做脆弱的跨盘 symlink 回退）。

### 4.2 切换原子步骤

1. 预检：两旁路存在且含 `Mechabellum.exe`；目标 ≠ 当前；游戏与 Steam 未运行。  
2. 断开当前 `Mechabellum` Junction（旁路真目录不动）。  
3. 创建新 Junction → 目标旁路。  
4. 校验联接后游戏根可识别。  
5. Junction 成功后立即更新 `ActiveBranch`（以目录真相为准）；**Deploy 仍须等**用户确认已对齐 Steam Beta 后再执行。  
6. Junction 步骤失败 → 回滚联接，不改 `ActiveBranch`、不 Deploy。

### 4.3 失败回滚

- Junction 切换失败：按切换前记录重建联接，恢复旧 `ActiveBranch`。  
- Junction 已成功但 Deploy 失败：**不**把目录切回去（避免与用户已对齐的 Steam Beta 错位）；保持新分支，显示未同步。  
- 每次危险操作写 `branch-switch-journal.json`；进程崩溃后下次启动提示「尝试修复联接」。

### 4.4 解除双服配置

1. 确认当前 Junction 目标。  
2. 断开 Junction，将**当前旁路真目录改名回** `Mechabellum`。  
3. 询问是否删除另一旁路（**默认不删**）。  
4. 将**当前分支**的 `deploy-manifest.*.json` 复制回 `deploy-manifest.json`（作为单安装清单），再清除分支配置；分支清单文件可保留或删除，不影响单安装主路径。

不修改 `appmanifest_*.acf`，不碰 Steam `userdata`。

---

## 5. 配置模型与服务拆分

### 5.1 分支配置

推荐使用并列文件（如 `%AppData%\MechabellumModManager\branch-switch.json`），避免与 `AppConfig` 过度耦合：

```text
BranchSwitchConfig
  Enabled
  SteamLinkPath          // 即 GamePath，必须是 ...\Mechabellum
  OfficialStorePath
  BetaStorePath
  ActiveBranch           // Official | Beta
  OfficialProfileId
  BetaProfileId
```

- 切服成功后同步更新 `AppConfig.GamePath` 与 `AppConfig.ActiveProfileId`，兼容现有启动与 Deploy。  
- 双服启用后：若用户将 `GamePath` 改为旁路真目录 → 拒绝或纠正回 `SteamLinkPath`，并记日志。

### 5.2 Deploy 清单按分支隔离

现有单一 `deploy-manifest.json` 以 `GamePath` 字符串识别安装体。双服共用 Junction 路径后，若仍用一份清单，会把上一服的托管文件集误用于当前旁路（误删 / Dirty 错乱）。

| 文件 | 用途 |
|------|------|
| `deploy-manifest.official.json` | 正式服旁路托管文件 |
| `deploy-manifest.beta.json` | 测试服旁路托管文件 |
| `deploy-manifest.json` | 迁移兼容：启用双服时把旧清单拷到**当时当前分支**对应文件；单安装模式可继续用旧文件名 |

- `DeployPlanner` / Dirty：始终使用**当前 `ActiveBranch` 对应清单**。  
- 清单可另存 `StorePath`（旁路真路径）供校验。

### 5.3 服务

| 服务 | 职责 |
|------|------|
| `BranchSwitchService` | 向导、Junction、解除配置、journal、启动修复 |
| `ProcessProbe` 扩展（或 `SteamProcessProbe`） | 检测 `Mechabellum` 与 `steam` 是否在运行 |
| 现有 `DeployService` / `ProfileService` | API 不变；由 VM 在确认后对绑定 Profile 调用 `Apply` |
| 现有 `MelonLoaderInstaller` / `GameDetector` | 仅检测/提示**当前** `GamePath`；缺 Loader 不自动装到另一旁路 |

### 5.4 Profile 绑定规则

- 设置中两个下拉只改绑定，不立刻 Deploy。  
- 切服流程最后：`ActiveProfileId = 目标服绑定 Id` → 选中该 Profile → `DeployService.Apply`。  
- 主界面手动更换方案时：**同时更新当前服的绑定**，避免绑定与当前方案长期不一致。  
- 「从游戏导入」仍扫当前 `GamePath`；切服后**默认不**自动再扫（避免库膨胀）；可提供可选扫描。

---

## 6. MelonLoader

- Loader、`version.dll` / `winhttp.dll`、`Il2CppAssemblies` 位于游戏目录内，随旁路隔离是预期行为（两服二进制可能不同）。  
- Steam 拉取「另一服」时通常不会保留/生成 MelonLoader；向导与切服后必须检测当前服 Loader，缺则提示用户安装。  
- 管理器安装包装 Loader 时只写入当前 `GamePath`（当前 Junction 目标），不承诺另一旁路同时就绪。

---

## 7. 错误态

| 情况 | 行为 |
|------|------|
| Steam 或游戏未退出 | 中止切换/向导危险步 |
| 非 NTFS / 跨盘 / 旁路名冲突 | 向导拒绝并说明 |
| Junction 失败 | 恢复切服前联接；不改 Profile |
| 目标旁路缺游戏 exe | 中止；标损坏；提供重新配置 |
| Deploy 失败 | 保持新分支目录；显示未同步 |
| 未完成 journal | 启动横幅 +「尝试修复联接」 |
| `GamePath` 被改离联接路径 | 纠正或禁用切换直至修复 |
| 当前服 Loader 不完整 | 允许目录已切换；Deploy 按现有逻辑拒绝直至就绪 |

半自动无法 100% 验证用户是否选对 Beta；以退出 Steam + 固定顺序降低写穿风险，确认步骤以用户声明为主，弱文件校验为辅（可选）。

---

## 8. 测试要点

1. 向导：归档 A 后路径为空 → B 就位 → 归档 B → Junction；两旁路完整且互不覆盖。  
2. 日常切换：Steam 未退出必须失败；退出后 Junction 目标正确。  
3. Deploy：正式服方案文件不出现在测试服旁路（反之亦然）；读写对应 `deploy-manifest.*.json`。  
4. 旧用户：仅有一份 `deploy-manifest.json` 时启用双服，迁移到当前分支清单。  
5. 解除双服：当前旁路还原为真实 `Mechabellum`；默认保留另一旁路；之后行为回到单安装。  
6. 崩溃：在「已断联接、未建新联接」处杀进程 → 启动修复恢复可启动路径。  
7. MelonLoader：仅当前服有 Loader 时，另一服检测为缺 Loader，不误报管理器整体损坏。  
8. `GamePath` 指向旁路真目录时，双服切换不可用或被纠正。

---

## 9. 与现有管理器的衔接摘要

| 现有能力 | 衔接方式 |
|----------|----------|
| `GamePath` / `SteamGameLocator` | 继续指向 Junction 路径 `...\Mechabellum` |
| Profile / 外部库（AppData） | 不随游戏目录；两服共享库、分绑方案 |
| `DeployService` | 按分支清单 Apply；成功切服后调用 |
| `ProcessProbe` | 扩展 Steam 进程检测 |
| 设置页折叠区 | 新增双服 UI，不另起应用 |

---

## 10. 已确认决策记录

| 决策 | 选择 |
|------|------|
| 方案形态 | 双完整目录 + Junction |
| 产品入口 | 嵌入现有 Mod 管理器设置页 |
| Steam Beta | 半自动（用户在 Steam 中选择） |
| Mod 方案 | 每服绑定 Profile，切服后自动应用 |
| 主界面换方案 | 写回当前服绑定 |
| 切服与 Steam | 必须先退出 Steam，再切 Junction，再开 Steam 对齐 Beta |
| 向导拉另一服 | 先归档 A 且路径为空，禁止先联接 A 再下载 |

---

## 11. 范围一句话

设置里提供半自动双目录 Junction 切换，每服隔离 Profile 与 Deploy 清单；通过「先退 Steam、空目录拉另一服 / 先切联接再对齐 Beta」避免 Steam 写穿旁路副本。
