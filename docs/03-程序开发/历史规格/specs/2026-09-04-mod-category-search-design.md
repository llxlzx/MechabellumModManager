# Mod 分类 / 标签 / 检索 / 排序 设计

**Status:** Approved  
**Date:** 2026-09-04  
**Version target:** next manager release after 1.0.5（本规格仅定稿；实现与版本号上调另开任务，本提交不 bump 版本）

**EN:** Design for mod categories, tags, search, and sort. Approved for implementation in the release after 1.0.5.

---

## 1. 目标 / Goals

在 **Mod 浏览** 与 **Mod 库** 中提供轻量、一致的检索与筛选能力，帮助用户按内容类型快速找到 Mod，而不改变现有 MelonLoader「Type / 部署槽位」语义。

**EN:** Lightweight, consistent filter/search on both Browse and Library; content category ≠ deploy Type.

---

## 2. 已锁定决策 / Decisions locked

| 决策 | 说明 |
|------|------|
| UI 形态 | 两侧页面顶部 **轻量筛选栏（filter bar）**；不做侧边栏，不做「标签云优先」 |
| 能力组合 | **搜索** + **分类** + **可扩展标签** + **排序**（名称 / `updatedAt`） |
| 数据来源 | 目录（catalog）提供默认值；本地允许 `categoryOverride` + `extraTags`（玩家方案 B） |
| Type 列 | MelonLoader Type 列语义不变；部署槽位 ≠ 内容分类 |

**EN:** Filter bar on both views; search + category + extensible tags + sort (name / updatedAt); catalog defaults with local overrides; MelonLoader Type unchanged.

---

## 3. 分类枚举 / Category enum

固定枚举（写入 catalog / 校验时使用）：

| Value | 用途简述 |
|-------|----------|
| `OverlayUI` | 覆盖层 / HUD / 界面展示类 |
| `QoL` | 生活质量 / 操作便利 |
| `Camera` | 镜头 / 视角 |
| `CombatAssist` | 战斗辅助 |
| `Economy` | 经济 / 计算 / 销售相关 |
| `ReplayDebug` | 回放 / 调试工具 |
| `Misc` | 杂项（有意归类但不在上列） |
| （空 / 无效） | 展示与筛选时视为 **`Uncategorized`** |

说明：`Uncategorized` 为 **展示与筛选用伪分类**，不必作为 catalog 必填写入值；缺省或非法 category 时按未分类处理。

**EN:** Enum above; missing/invalid → treat as Uncategorized for UI/filter.

---

## 4. 数据模型 / Data

### 4.1 `catalog.json`（远程/内置目录）

每个 Mod 条目可含：

- `category`：枚举字符串（可选；缺省 → Uncategorized）
- `tags`：`string[]`（可选；缺省 → 空数组）

### 4.2 `package.json`（本地包元数据）

在现有字段上扩展：

- `categoryOverride?`：可选；若存在且合法，覆盖 catalog 的 `category`
- `extraTags?`：可选字符串数组；与 catalog `tags` **合并去重**（本地附加，不替换目录标签）

### 4.3 生效规则 / Effective category & tags

```
effectiveCategory =
  valid(categoryOverride) ? categoryOverride
  : valid(catalog.category) ? catalog.category
  : Uncategorized

effectiveTags =
  unique( concat(catalog.tags ?? [], package.extraTags ?? []) )
```

- **目录刷新 / 同步不得覆盖或清除** 本地的 `categoryOverride` 与 `extraTags`。
- Catalog 更新可改变默认 `category` / `tags`；仅在无 override 时影响生效分类。

**EN:** Overrides win; refresh must preserve local overrides; tags = catalog ∪ extraTags (deduped).

---

## 5. UI / UX

### 5.1 筛选栏（Mod 浏览 + Mod 库 共用交互模型）

从左到右（或等价布局）：

1. **搜索框**：匹配名称、作者、描述等已有文本字段（实现阶段对齐现有搜索字段范围；至少覆盖显示名）
2. **分类下拉**：枚举 + Uncategorized +「全部」
3. **标签下拉**：选项由当前数据动态聚合（catalog + 本地 effectiveTags）；「全部」+ 单选某一 tag（v1 不做多选标签云）
4. **排序**：`name`（显示名，区域感知字符串比较）、`updatedAt`（新→旧或旧→新；默认约定实现时选一侧并在 UI 标明）

筛选为 **AND**：搜索 ∩ 分类 ∩ 标签；再应用排序。

### 5.2 列表与详情

- 列表增加 **Category** 列（显示生效分类；Uncategorized 用本地化文案）
- 详情区展示 **tags**（生效标签列表）
- **Mod 库**：提供编辑入口，设置/清除 `categoryOverride`、增删 `extraTags`（写入本地 `package.json`）

### 5.3 状态持久化

- v1：**会话内**记住当前页筛选状态即可；**不要求**跨启动持久化

**EN:** Shared filter bar; Category column; tags in detail; library edit for overrides; session-local filters only for v1.

---

## 6. 校验 / Validation

| 字段 | 规则 |
|------|------|
| `category` / `categoryOverride` | 若存在，必须属于第 3 节枚举；否则视为未分类并 **写日志** |
| `tags` / `extraTags` | 必须为字符串数组；元素 trim 后忽略空串；非法结构按空数组 + 日志 |
| 未知分类值 | 不阻断加载；UI/筛选按 **Uncategorized**；日志记录原始值 |

**EN:** Invalid category → Uncategorized + log; tags must be string[]; never fail the whole catalog load for bad category/tags alone.

---

## 7. 提交邮件模板 / Email template addition

Mod 提交流程（邮件正文模板）增加两行，便于维护者写入 catalog：

- `category:` ＜枚举值或留空＞
- `tags:` ＜逗号分隔，可空＞

**EN:** Add `category:` and `tags:` lines to the submit email template.

---

## 8. 明确不在范围内 / Out of scope

- 侧边栏导航式分类
- 标签云 / 多选标签为主交互
- 个人标签云端同步
- 改变 MelonLoader **Type**（部署槽位）的含义或列行为
- 本提交不实现功能、不打包、不上调版本号

---

## 9. 初始 catalog 映射建议 / Suggested initial mapping

实现目录数据时可按下列种子映射（可在上线前微调，但不改变枚举本身）：

| Mod id（或包名键） | category |
|--------------------|----------|
| `show-grid` | OverlayUI |
| `damage-rank` | OverlayUI |
| `quick-item` | QoL |
| `undo-plus` | QoL |
| `quick-camera` | Camera |
| `auto-speed` | CombatAssist |
| `sales-calculation` | Economy |
| `replay-tool` | ReplayDebug |

未列出的条目：缺省 Uncategorized，或由维护者标为 `Misc`。

**EN:** Seed mapping for implementers; unlisted → Uncategorized (or Misc if intentionally misc).

---

## 10. 实现边界提醒 / Implementation notes

- 规格已批准；**编码与发版另开任务**。
- 目标版本：1.0.5 **之后**的下一次管理器发布。
- i18n：分类名、Uncategorized、筛选栏标签需进现有 `Strings*.resx`（实现阶段）。

---

## Self-review checklist

- [x] 无 TBD / 开放决策残留
- [x] 与「轻量筛选栏 / 方案 B 本地覆盖 / Type 不变」一致
- [x] Out of scope 与功能边界清晰
- [x] 数据生效规则与「刷新不覆盖 override」写明
