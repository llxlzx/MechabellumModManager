# 纯净游戏彻底清理（强化卸载）— 设计规格

**日期：** 2026-09-06  
**状态：** 待用户确认 spec  
**优先级铁律：** 绝不误删 Steam / 游戏本体；宁可中止也不猜路径。

## 已确认产品选择

| 项 | 选择 |
|----|------|
| 双服 | 先安全解除双服 → 只保留**正式服**完整仓 → 再清 Melon/Mods；删另一侧仓须多重确认 + 路径白名单 |
| 入口 | **C：** Windows 卸载与管理器内「彻底清理」共用同一核心 |
| 正式服空壳 | **A：** 中止；不删另一侧仓 |
| 实现 | **推荐：** 计划→白名单校验→执行；CLI + Inno + 管理器共用 |

## 目标

卸载/彻底清理后，目标机上只剩「纯净」Mechabellum 游戏本体（Steam 管理的文件），去掉管理器安装的 Melon、Mods、管理器 AppData 等残留。

## 非目标

- 不卸载 .NET 6 / .NET 8（或其它系统运行时）。
- 不删除 `Mechabellum.exe`、`GameAssembly.dll`、`Mechabellum_Data`、Steam `appmanifest`、非白名单路径。
- 不做「扫描整盘 / 按关键字模糊删」。
- 不在正式服不完整时强行删测服仓或物化测服为唯一根（见产品选择 A）。

## 架构

```
[Inno Uninstall] ──┐
                   ├──► MechabellumModManager.exe --pure-game-cleanup [flags]
[管理器 UI 命令] ──┘              │
                                 ▼
                    PureGameCleanupPlanner / Executor
                                 │
                    复用 BranchSwitchService teardown（条件满足时）
```

- **Planner：** 只产出 `CleanupPlan`（有序列表：动作类型 + 绝对路径 + 理由）。
- **Validator：** 每条路径必须通过白名单；失败则整单中止。
- **Executor：** 按计划执行；任一步失败则停止后续危险步骤（删仓），并写日志。

## 阶段顺序

1. **进程门：** 游戏进程未退出 → 中止。Steam 客户端若会影响 junction/移动 → 要求退出（与现有切服一致）。
2. **解析路径：** 只从 `config.json` / `branch-switch.json` / 安装时写入的游戏路径读取；禁止「猜多个 Steam 库乱删」。
3. **双服（若启用或磁盘上存在官方命名仓）：**
   - 正式服仓必须 `LooksLikeGameRoot`（exe + GameAssembly），否则 **中止、不删仓**。
   - 若当前在测服：先切正式服（失败则中止）。
   - 解除双服：复用现有 teardown；删除另一侧仓仅当：
     - 路径通过白名单（见下）；
     - 用户已确认（UI 两步；CLI/Inno 用显式 `--confirm-delete-other-store` 或卸载向导勾选+确认页）。
4. **Melon/Mod 白名单删除**（对最终游戏根；双服解除后通常为物化后的 `Mechabellum`）：
   - 目录（仅游戏根下**精确同名**子目录整树删除）：`MelonLoader`、`Mods`、`Plugins`、`UserLibs`、`UserData`
   - 文件（仅游戏根下精确文件名）：`version.dll`、`winhttp.dll`
5. **管理器数据：** 删除 `%AppData%\MechabellumModManager`（整目录）；若 `dataRoot` 指向便携数据且路径通过「位于管理器安装目录或显式便携根」校验，则一并删除。
6. **安装目录：** 由 Inno 标准卸载删除 `{app}`；管理器内「彻底清理」在成功后退出，并提示可用「卸载程序」删安装目录（或可选启动 `unins000.exe`，Phase1 可不自动调）。

## 路径白名单规则（硬门）

删除前每一路径 `P` 必须满足：

1. `P` 为绝对路径且规范化（拒绝 `..` 逃逸）。
2. **游戏根 `G`：** 存在且 `LooksLikeGameRoot(G)`（或双服解除前对正式服仓同检）。
3. Melon/Mod 项：`P` 必须等于 `G\<ExactName>`（大小写按 OrdinalIgnoreCase），不得是 `G` 本身，不得是 `G` 的父路径。
4. **另一侧仓删除：**
   - 文件夹名必须为 `Mechabellum_beta` 或配置中的 beta 仓名且位于与正式服同一 `common` 父目录；或配置 `betaStorePath` 与磁盘一致并通过「父目录 = common 且 sibling 正式服完整」校验；
   - 禁止删除 `Mechabellum_official` 若它是当前保留侧；
   - 禁止删除路径等于 `G` 或等于 Steam `steamapps` 根。
5. **AppData：** 仅 `%AppData%\MechabellumModManager`（或等价 `Environment.SpecialFolder.ApplicationData` 拼接固定段）。

任何一项不满足 → 该动作从计划中剔除或整单失败（删仓类默认整单失败）。

## 确认 UX

### 管理器内

1. 第一屏：计划摘要（将删路径列表，只读）。
2. 第二屏：若含删仓，额外警告「不可恢复、体积大」；确认后执行。
3. 执行中 Busy；结束后日志 + 是否成功。

### Inno 卸载

1. 卸载说明：将尝试清理游戏内 Melon/Mods 与管理器数据；.NET 不卸。
2. 勾选「清理游戏内 Mod/Melon 残留」（默认：**勾选**）。
3. 若计划含删测服仓：额外确认页或要求勾选「删除另一服游戏副本」。
4. 正式服不完整：显示错误/警告，**跳过删仓与危险步骤**；可选仍删 AppData + 安装目录（管理器自身），游戏布局留给用户。

## CLI

```
MechabellumModManager.exe --pure-game-cleanup
  [--game-path "..."]          # 可选；默认读 config
  [--dry-run]                  # 只打印计划
  [--confirm-delete-other-store]
  [--skip-appdata]
  [--skip-melon]
```

退出码：`0` 成功；`1` 参数/配置；`2` 进程占用；`3` 正式服不完整中止；`4` 校验失败；`5` 执行中失败。

## 测试（必须）

- Planner：白名单通过 / 拒绝 `..\Windows`、拒绝删 `GameAssembly.dll`、拒绝空壳正式服时的删仓。
- Validator：junction / 相对路径 / 错误仓名。
- 不测真实删用户 Steam；用 temp fixture。

## 验收

- [ ] 双开入口行为一致（同一核心）。
- [ ] 正式服空壳时不删测服仓。
- [ ] 清理后游戏根无 Melon 代理 DLL 与白名单目录（若曾存在）。
- [ ] AppData 管理器目录已删（未 `--skip-appdata`）。
- [ ] .NET 仍在；`Mechabellum.exe` / `GameAssembly.dll` 仍在。
- [ ] 单元测试覆盖白名单拒绝用例。

## 风险

| 风险 | 缓解 |
|------|------|
| 误删 Steam 游戏 | 白名单 + 正式服完整门 + 计划预览 |
| 卸载时配置已丢 | 安装时已写 gamePath；缺失则只卸 `{app}` + 尝试默认 AppData，不碰游戏 |
| 文件占用 | 进程门；失败记日志不继续删仓 |
| 用户仍要保留 Melon | Inno 勾选可关；管理器命令可取消 |
