# 作者回信 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给 Oddz 和 Minz 各准备一封回信，里面是改过的源码，以及用本机 MelonLoader `net6` 编出来的 DLL。编不过的文件留在源码里，信里写明。

**Architecture:** 从 `_eml_extract` 里的反编译结果复制出三份可编译工程，只改规格点名的行为。引用指向 `D:\Steam\steamapps\common\Mechabellum\MelonLoader\net6` 和 `MelonLoader\Il2CppAssemblies`。目标框架 `net6.0`。成品放在仓库外面。

**Tech Stack:** C# / .NET 6 SDK、MelonLoader 0.7 的 `net6` 引用、ILSpy 已反编译的源码。

**Working branch:** none (working tree only, no commits)

## Global Constraints

- 输出目录是 `D:\gongzuo\作者回信\2026-10-17\`。不提交这个目录，也不提交 `_eml_extract`。
- 不把三份邮箱写进公开目录。
- HideControlTips 的隐藏词条不改。程序集版本改为 `1.0.0.0`。`PatchAll` 失败只记 Melon 日志，模组继续加载。
- 升级助手默认锁定只做整段相等、不分大小写、不做子串：`爬虫`/`Crawler`，`尖牙`/`Fang`。对不上就忽略。两个都认出才把 `default` 写成 ID。只认出一个时不写偏好。本地玩家 `IsDeploying` 为假或 `IsDeployOver` 为真时停掉队列。5 秒超时后中止后续队列的行为不改。
- 网格标尺只在 `IsDeploymentActive()` 为假时隐藏距离环。不改 `views` 前 4 个，不改 C 键，不改偏好说明原文。
- 回信提议 Oddz 用 `mailto:rnkt@pm.me`，Minz 两个模组都用 `mailto:c4ever_zm@hotmail.com`，并请他们确认或换成自己的渠道。

---

### Task 1: 三份工程和 HideControlTips

**Files:**
- Create: `D:\gongzuo\作者回信\2026-10-17\Oddz\HideControlTips\`
- Create: `D:\gongzuo\作者回信\2026-10-17\Oddz\回信.md`
- Source copy from: `_eml_extract\_Mod投稿_Submit_ HideControlTips\HideControlTips-submit\decompiled-HideControlTips\`

**Interfaces:**
- Consumes: 反编译出的 `HideControlTipsMod.OnInitializeMelon`
- Produces: `AssemblyVersion("1.0.0.0")`，以及被 try/catch 包住的 `PatchAll`

- [ ] **Step 1: 复制工程并改 csproj**

`TargetFramework` 改为 `net6.0`。给 `MelonLoader`、`0Harmony`、`Il2CppThirdParty` 加上指向游戏目录的 `HintPath`。`Il2CppThirdParty.dll` 若在 `Il2CppAssemblies` 找不到，就在 `MelonLoader` 目录下按文件名搜索后再写路径。

- [ ] **Step 2: 改两处源码**

`Properties\AssemblyInfo.cs` 的 `AssemblyVersion` 改为 `1.0.0.0`。

`OnInitializeMelon` 里的 `PatchAll` 放进 try/catch。catch 调用 `LoggerInstance.Error`，把异常写进去，然后方法正常返回。

- [ ] **Step 3: 编译**

Run: `dotnet build "D:\gongzuo\作者回信\2026-10-17\Oddz\HideControlTips\HideControlTips.csproj" -c Release`

Expected: 生成 DLL。失败则把错误原文写进 `回信.md`，源码保留。

- [ ] **Step 4: 写回信**

说明隐藏逻辑没改、版本号改了、补丁失败会记日志。提议反馈渠道 `mailto:rnkt@pm.me`，请确认或换成问卷、https 链接或其他说明，并保证中英文玩家都能用。

### Task 2: 升级助手

**Files:**
- Create: `D:\gongzuo\作者回信\2026-10-17\Minz\UpgradeHelper\`
- Source copy from: `_eml_extract\_Mod投稿_Submit_ 升级助手(UpgradeHelper)\decompiled-UpgradeHelper\`

**Interfaces:**
- Consumes: `LockStore.GetLocked`、`UpgradeHelperMod.UpdateVisibility`、`TickQueue`
- Produces: 双语整段匹配；部署结束后 `Abort` 队列

- [ ] **Step 1: 复制工程，csproj 改为 `net6.0`，HintPath 指向 `net6` 与 `Il2CppAssemblies`**

- [ ] **Step 2: 改默认锁定**

`DefaultLockedNames` 改成两对。比较用 `string.Equals(..., StringComparison.OrdinalIgnoreCase)`，名字必须整段相等。卡牌表或当前行对上一对中的任一名字就记下该 ID。这一次的锁定集合包含已认出的 ID。两个都认出才把偏好从 `default` 写成 ID 列表。只认出一个时不写偏好、不把空结果永久缓存。

- [ ] **Step 3: 本地部署结束就停队列**

在现有「离开全局部署阶段就 Abort」之外，当快照里本地玩家不再部署（`IsDeploying` 为假，含已经 `IsDeployOver`）时也 `Abort`。不要改 `BatchUpgradeQueue` 的 5 秒超时。

- [ ] **Step 4: 编译**

Run: `dotnet build` 该 csproj 的 Release。

Expected: DLL，或把错误写入稍后的 `回信.md`。

### Task 3: 网格标尺和 Minz 的信

**Files:**
- Create: `D:\gongzuo\作者回信\2026-10-17\Minz\GridRuler\`
- Create: `D:\gongzuo\作者回信\2026-10-17\Minz\回信.md`
- Source copy from: `_eml_extract\_Mod投稿_Submit_ 网格标尺(GridRuler)\decompiled-GridRuler\`

**Interfaces:**
- Consumes: `GridRulerMod.IsDeploymentActive`、`UpdateDeploymentRings`
- Produces: 非部署阶段调用现有的 `HideRings`

- [ ] **Step 1: 复制工程并改为 `net6.0`**

- [ ] **Step 2: 只加部署阶段判断**

`UpdateDeploymentRings` 在永久网格打开之后、取 `views` 之前：`IsDeploymentActive()` 为假就 `HideRings` 并返回。不改 `GetSelectedFootprint` 的 `i < 4`。不改 `ToggleHotkeyKey` 的默认值 `C`。不改偏好描述那句「只在拖动单位时可见」。

- [ ] **Step 3: 编译**

Run: `dotnet build` 该 csproj 的 Release。

- [ ] **Step 4: 写 Minz 的回信**

两个模组各写改了什么、没改什么。距离环现在是整个部署阶段都可能出现，不是只在拖动时出现。前 4 个 `views` 和全局 C 键请他确认。英文名 `Crawler`、`Fang` 还没对照游戏卡牌表，对不上就不会锁错单位，中文名仍然有效。两个模组提议的反馈渠道都是 `mailto:c4ever_zm@hotmail.com`，请确认或换成自己的渠道。超时 5 秒会中止后面的升级，这点保持原样。
