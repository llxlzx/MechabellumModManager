# Catalog Mod 名称 / 简介多语言设计

**Status:** Approved  
**Date:** 2026-09-04  
**Version target:** next manager release after current tip（本规格定稿后实现；catalog 文案可单独在 MechabellumMods 仓库更新，不必与 Setup 同发）

**EN:** Localize catalog mod **name** and **summary** from `catalog.json` locales, following the manager UI language, with fallback to default fields.

---

## 1. 目标 / Goals

管理器界面语言切换时，**Mod 浏览**与**本地库中来自目录的名称/简介**同步显示对应语言文案，避免「UI 英文、内容中文」混杂；翻译由 catalog 维护，刷新目录即可生效，无需重发安装包。

**EN:** When UI language changes, browse (and library items enriched from catalog) show matching name/summary; content lives in Mods repo.

**非目标 / Non-goals**

- 不对作者名、版本号、日期、文件路径做翻译  
- 不做运行时机器翻译  
- 不强制五语齐全（缺译回退默认字段）  
- 不把 mod 文案写入管理器 `Strings*.resx`

---

## 2. 已锁定决策 / Decisions locked

| 决策 | 说明 |
|------|------|
| 存放位置 | `MechabellumMods` 的 `catalog.json`（嵌套 `locales`） |
| 默认底稿 | 现有顶层 `name` / `summary`；缺语言或缺字段时回退至此 |
| 语言键 | 与管理器一致：`zh-CN`、`en`、`de`、`ja`、`ru` |
| 展示范围 | 浏览列表名、详情名、简介；本地库若带目录信息则名称/简介同步 |
| 搜索 / 按名排序 | 只针对**当前显示**的名称与简介（缺译则等于默认底稿） |
| 切语言 | 不重新下载目录；内存重算显示文案并刷新绑定 |
| 标签 / 分类 | 标签 id 仍英文；显示名走既有 UI 本地化（本规格不重复） |

**EN:** Nested `locales` in catalog; fallback to top-level name/summary; search/sort on displayed strings only; library catalog-backed fields refresh on language change.

---

## 3. 数据模型 / Data (`catalog.json`)

### 3.1 条目形状

每个 mod 保留现有字段，并增加可选：

```json
{
  "id": "example-mod",
  "name": "功能测试 MOD",
  "summary": "默认简介（通常为中文或作者母语）",
  "author": "…",
  "version": "1.1.0",
  "updatedAt": "2024-09-02",
  "file": "mods/example.dll",
  "preview": "previews/example.png",
  "type": "MelonMod",
  "category": "OverlayUI",
  "tags": ["hud", "qol"],
  "locales": {
    "en": {
      "name": "Feature Test Mod",
      "summary": "English summary…"
    },
    "de": {
      "name": "Funktionstest-Mod"
    },
    "ja": {
      "name": "機能テスト MOD",
      "summary": "…"
    },
    "ru": {
      "name": "…",
      "summary": "…"
    }
  }
}
```

规则：

- `locales` 可选；缺省时行为与今日完全一致（始终显示顶层 `name`/`summary`）  
- 某一语言下可只提供 `name` 或只提供 `summary`；另一字段回退顶层  
- `zh-CN` 通常等于顶层默认，**可不写入** `locales.zh-CN`；若写入则优先生效  
- 未知语言键忽略；空字符串视为「未提供」并回退  

### 3.2 解析与选用 API（管理器侧）

对当前 UI culture（经 `LocalizationService` 归一后的 `zh-CN` / `en` / …）：

```
ResolveName(mod, culture)    = locales[culture].name    if non-empty else mod.name
ResolveSummary(mod, culture) = locales[culture].summary if non-empty else mod.summary
```

旧版 catalog（无 `locales`）无需迁移即可继续工作。

---

## 4. UI 与绑定 / UI binding

| 表面 | 行为 |
|------|------|
| 浏览列表「名称」列 | 绑定解析后的显示名（非原始 `Mod.Name` 直出） |
| 浏览详情标题 / 简介 | 同上 |
| 浏览搜索框 | 对显示名 + 显示简介做子串匹配（大小写不敏感策略与现有一致） |
| 浏览按名称排序 | 按显示名排序（当前 culture 比较器） |
| 本地库显示名 / 简介 | 若条目已用 catalog 充实：随语言用同一套 Resolve；纯本地导入且无目录对照：保持本地 `DisplayName`/`Summary` |
| 切换语言 | `ApplyUiLanguage` 时通知 catalog / library 相关 VM 刷新 Name/Summary 绑定；重建筛选结果 |

**EN:** ViewModels expose localized display properties; language switch refreshes without re-fetch.

---

## 5. 兼容与校验 / Compatibility

- **向后兼容**：无 `locales` 的 catalog 与旧管理器均可用；新管理器读旧 catalog 无回归  
- **向前兼容**：旧管理器忽略未知字段 `locales`，仍读顶层中文（或默认）`name`/`summary`  
- **CI（Mods 仓库，可选后续）**：若已有 schema 校验，允许 `locales` 对象；不强制五语齐全  
- **投稿指南**：鼓励提供至少 `en`；默认字段保持作者母语  

---

## 6. 内容维护 / Content (MechabellumMods)

- 管理器实现可与 catalog 文案补全并行：先落地解析与 UI，再逐步为现有 mod 填写 `locales`  
- 作者名、专有名词、MelonLoader / Steam 等产品名可不译或按各语言惯例保留  

---

## 7. 验收 / Acceptance

1. UI 为中文且仅有默认字段时，浏览显示与今日一致  
2. catalog 为某 mod 提供 `locales.en` 后，切到 English：该 mod 列表名与简介变为英文；缺 `summary` 的语言回退默认简介  
3. 切回中文：立刻恢复默认（或 `zh-CN`）文案，无需再点「刷新目录」  
4. 英文模式下用英文显示名搜索可命中；用未显示语言的其它译名搜索不要求命中  
5. 已在库且与 catalog 关联的项，语言切换后名称/简介与浏览一致策略  
6. 无 `locales` 的旧 catalog 刷新后无报错、无空白名  

---

## 8. 实现边界 / Implementation boundary

| 仓库 | 工作 |
|------|------|
| 本仓库（管理器） | `CatalogMod` 模型、`Resolve*`、VM 绑定、搜索/排序、语言切换刷新 |
| `llxlzx/MechabellumMods` | 更新 `catalog.json` 写入各 mod 的 `locales`（可分批） |

本规格**不**要求同一次 PR 写完全部 mod 译文；管理器侧以「正确解析 + 回退」为完成标准。
