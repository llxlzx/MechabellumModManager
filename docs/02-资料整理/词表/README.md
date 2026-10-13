# Localization (i18n) / 本地化

> **说明 / Notice**  
> 本目录说明管理器 UI 字符串的翻译工作流；键名变更需同步改代码。  
> **This folder documents the UI string translation workflow. Key renames require matching code changes.**

> **⚠️ AI 生成声明 / AI-generated notice**  
> 本说明中英文主要由 AI 辅助整理。  
> **This README was largely AI-assisted.**

机器翻译产物位于 `Resources/Strings.*.resx`，覆盖：

Machine translations ship in `Resources/Strings.*.resx` for:

- `zh-CN`（默认 / 权威源 · default / authoritative source）
- `en`
- `ru`
- `ja`
- `de`

## 语言 / Language

- 中文：[给人工译者](#1-给人工译者-中文)
- English: [For human translators](#1-for-human-translators-english)

---

## 1. 给人工译者 (中文)

1. 编辑或审阅 `source-zh-CN.tsv`（键 + 中文原文）。
2. 按相同键为其他语言提供译文。
3. 维护者合并进对应的 `Strings.<culture>.resx`。

不要在未改代码的情况下改键名。新增 UI 文案需同步加入：

- `source-zh-CN.tsv`
- 每一个 `Strings*.resx`
- `UiStrings` 属性（若在 XAML 中绑定）

---

## 1. For human translators (English)

1. Edit or review `source-zh-CN.tsv` (key + Chinese source text).
2. Provide translations for other languages against the same keys.
3. Maintainers merge into the matching `Strings.<culture>.resx` files.

Do not change keys without a code update. New UI strings must be added to:

- `source-zh-CN.tsv`
- every `Strings*.resx`
- `UiStrings` properties (if bound in XAML)
