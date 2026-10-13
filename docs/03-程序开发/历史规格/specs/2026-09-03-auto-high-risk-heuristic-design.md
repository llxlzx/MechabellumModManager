# 自动高风险标记（名称关键词启发式）设计

日期：2026-09-03  
状态：已批准（待实现）  
关联：`2026-09-02-mechabellum-mod-manager-design.md` §5.1 RiskGate

## 1. 目标

导入与刷新 Mod 列表时，根据**名称类文本**的关键词启发式自动设置 `ModPackage.HighRisk`，减少玩家漏标战斗/作弊向 Mod 的风险。

仍保留列表上的「高风险」手动切换，但按产品选择：**手动结果仅临时有效**；下次打开 / `ReloadMods` / 再次导入后按关键词**重新覆盖**。

## 2. 非目标

- 不保证识别所有作弊或战斗逻辑 Mod
- 不扫描 DLL IL / 字符串常量 / 可疑 API（可留后续版本）
- 不因判定结果拒绝导入或自动删除
- 不改变全局风险横幅文案；不提供作弊功能

## 3. 判定规则

### 3.1 输入文本（拼接后做一次匹配）

对每个包取下列字段（空则跳过），用空格连接成一份 haystack：

| 来源 | 字段 |
|------|------|
| 元数据 | `DisplayName`、`Id`、`Author`（若有） |
| 路径 | 包目录名（`PackageDirectory` 最后一段） |
| 文件 | 包内主部署文件的文件名（无扩展名也参与；含扩展名一并匹配） |

作者字段：沿用现有 `package.json` / `AssemblyInspector.MelonAuthor` 已写入的值；没有则忽略。

### 3.2 匹配方式

- 不区分大小写的**子串**包含（`OrdinalIgnoreCase`）
- 任一关键词命中 → `HighRisk = true`
- 全部未命中 → `HighRisk = false`
- 返回可选的「命中词」供日志使用（取第一个命中即可）

### 3.3 关键词表（起步，集中常量，便于后续增补）

**英文：**  
`cheat`, `hack`, `unlock`, `damage`, `economy`, `godmode`, `trainer`, `aimbot`, `esp`, `infinite`, `unlimited`, `winhack`, `alwayswin`

**中文：**  
`作弊`, `外挂`, `解锁`, `伤害`, `经济`, `无敌`, `秒杀`, `修改器`, `战斗`, `数值`, `必胜`

**刻意不收录：** 裸词 `win`（易误伤 Window / Winning 等 QoL 名）。

UserLibs / UserData 包同样参与判定（名称仍可能暴露意图）；类型本身不单独抬高风险。

## 4. 何时重算

| 时机 | 行为 |
|------|------|
| 导入 DLL / Zip / 文件夹成功并落盘 `package.json` 前 | 写入自动 `HighRisk` |
| `MainViewModel.ReloadMods`（启动、换方案、导入后刷新等） | 对库中每个包重算并**写回** `package.json`（若与旧值不同） |
| `ToggleHighRisk` | 仅改内存 + 立即写盘；下一次 `ReloadMods` 会被关键词覆盖 |

启用勾选路径不变：`RiskGate.CanEnable(package.HighRisk, …)`。

## 5. 组件

### 5.1 `RiskHeuristic`（新）

```csharp
public sealed class RiskHeuristicResult
{
    public bool HighRisk { get; init; }
    public string? MatchedKeyword { get; init; }
}

public sealed class RiskHeuristic
{
    public RiskHeuristicResult Evaluate(ModPackage package);
    // 或 Evaluate(displayName, id, author, directoryName, fileNames)
}
```

纯函数、无 IO；关键词表为 `static readonly` 数组。

### 5.2 接入点

- `ModLibraryService`：导入完成构建 `ModPackage` 后调用；可选 `ApplyAndPersist` 辅助
- `MainViewModel.ReloadMods`：列出包后对每个包 `Evaluate`，`HighRisk` 变化则更新元数据并 `AppendLog`（节流：仅变化时记一条）

### 5.3 UI

- 列文案仍为「高风险」/「—」
- 不新增独立「自动/手动」状态列（本版无持久化来源枚举）
- 导入或重算变化时日志示例：`QuickDamage 因关键词「damage」自动标为高风险`

## 6. 测试

- `RiskHeuristicTests`：命中英文、命中中文、大小写、未命中 QoL 名（如 `QuickCamera`、`BetterWindow`）、裸 `win` 不因 `Window` 误报
- 导入/列表路径：可用轻量测试或现有 Fixture 断言导入带 `cheat` 名的包 `HighRisk == true`
- 现有 `RiskGateTests` / 高风险确认路径保持通过

## 7. 风险与文案

- README / 风险横幅可补一句：「高风险标记为名称启发式，可能误报或漏报，不替代自行判断。」
- 原设计「不保证从 dll 自动识别作弊」仍然成立；本功能仅名称启发式。

## 8. 验收

1. 导入名含 `damage` / `作弊` 的包 → 自动高风险，启用需确认  
2. `QuickCamera` 类 QoL 名 → 不高风险  
3. 手动关掉高风险后触发刷新 → 若名称仍命中则再次变为高风险  
4. 单元测试覆盖关键词表核心样例  
