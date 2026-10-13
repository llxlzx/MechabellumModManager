# Catalog Mod Locales Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve catalog mod `name`/`summary` from optional `locales` by UI language, with fallback to top-level fields; refresh browse + catalog-enriched library on language change.

**Architecture:** Extend `CatalogMod` with `locales`; add `CatalogLocaleResolver`; ViewModels expose resolved Name/Summary; search/sort use those; library packages keep default strings + runtime `CatalogLocales`.

**Tech Stack:** .NET 8 WPF, System.Text.Json, xUnit, FluentAssertions

## Global Constraints

- Fallback: non-empty `locales[culture].name|summary` else top-level `name`/`summary`
- Culture keys: `zh-CN`, `en`, `de`, `ja`, `ru` (via `LocalizationService`)
- No machine translation; no Strings.resx for mod copy
- Search/sort on currently displayed strings only
- Old catalogs without `locales` keep working

---

### Task 1: Model + Resolver (TDD)

**Files:**
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs` (`CatalogMod` + locale types)
- Create: `src/MechabellumModManager/Services/CatalogLocaleResolver.cs`
- Test: `tests/MechabellumModManager.Tests/CatalogLocaleResolverTests.cs`
- Modify: `tests/MechabellumModManager.Tests/ModCatalogServiceTests.cs`

**Interfaces:**
- Produces: `CatalogModLocale { Name?, Summary? }`, `CatalogMod.Locales: Dictionary<string, CatalogModLocale>?`
- Produces: `CatalogLocaleResolver.ResolveName(CatalogMod, string? culture = null)`, `ResolveSummary(...)`, overloads for `(defaultName, locales, culture)`

- [ ] Write failing resolver + deserialize tests
- [ ] Implement model + resolver
- [ ] Tests green

### Task 2: ViewModels + search/sort + language refresh

**Files:**
- Modify: `Models/ModPackage.cs` (runtime `CatalogLocales`)
- Modify: `ViewModels/CatalogModItemViewModel.cs`, `ModItemViewModel.cs`, `MainViewModel.cs`
- Ensure add-to-library persists **default** `Mod.Name`/`Mod.Summary`, not localized display

- [ ] Catalog/Library VMs resolve display Name/Summary
- [ ] Enrichment stores locales; Notify on language change
- [ ] Search/sort already bind to VM props — verify
- [ ] Build + unit tests pass

### Task 3: Optional sample locales in tests only

- [ ] Extend sample JSON in tests with `locales.en`; no requirement to edit remote MechabellumMods in this plan
