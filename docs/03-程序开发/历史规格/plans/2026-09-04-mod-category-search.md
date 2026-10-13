# Mod Category / Search / Tags / Sort Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a shared filter bar (search / category / tags / sort) on Mod 浏览 and Mod 库, with catalog `category`+`tags`, local `categoryOverride`+`extraTags`, and ship manager **1.0.6**.

**Architecture:** Introduce `ModCategory` + `ModTaxonomy` helpers for parse/effective category+tags. Extend `CatalogMod` / `ModPackage` / `PackageMeta` and MechabellumMods `catalog.json` + `validate_catalog.py`. Drive Browse/Library lists from filtered views (or filtered `ObservableCollection` rebuilds) on `MainViewModel`. Share one filter-bar XAML pattern; add library edit dialog for overrides. Bump version, package `release/v1.0.6`, commit both repos, publish `gh release`.

**Tech Stack:** .NET 8 WPF, CommunityToolkit.Mvvm, existing MVVM (`MainViewModel` / item VMs), System.Text.Json package.json, MechabellumMods `catalog.json` + Python `validate_catalog.py`, Inno Setup installer, xUnit + FluentAssertions tests.

## Global Constraints

- Target manager version: **1.0.6** (after 1.0.5)
- Content category ≠ MelonLoader deploy **Type** column (Type semantics unchanged)
- Filter bar on **both** Mod 浏览 and Mod 库; no sidebar; no multi-select tag cloud (v1)
- Filters are **session-local** only (no cross-launch persistence)
- Filter logic is **AND**: search ∩ category ∩ tag, then sort
- Invalid/missing category → treat as **Uncategorized** + log; never fail whole catalog/library load
- Catalog refresh / enrich **must preserve** local `categoryOverride` and `extraTags`
- `extraTags` merge with catalog tags (concat + dedupe); do not replace catalog tags
- Seed mapping from design spec §9; suggest initial tags (see Task 2)
- i18n: zh (default `Strings.resx`) + en/ja/de/ru + `docs/i18n/source-zh-CN.tsv` + `UiStrings`
- Two repos: manager (`MechabellumModManager`) and catalog (`D:\gongzuo\MechabellumMods`)
- Follow `docs/releasing.md` for package layout and `gh release`
- Spec: `docs/superpowers/specs/2026-09-04-mod-category-search-design.md`

## File map

| Path | Responsibility |
|------|----------------|
| `src/.../Models/ModCategory.cs` | Fixed category enum (+ `Uncategorized` for effective/UI) |
| `src/.../Services/ModTaxonomy.cs` | Parse/validate, effective category/tags, normalize tag lists |
| `src/.../Services/ModCatalogService.cs` | `CatalogMod.Category` / `Tags` JSON fields |
| `src/.../Models/ModPackage.cs` | `CategoryOverride`, `ExtraTags` |
| `src/.../Services/ModLibraryService.cs` | Persist/load overrides in `PackageMeta` / Write/TryLoad |
| `src/.../ViewModels/MainViewModel.cs` | Filter state, filtered views, library edit commands, dual Persist paths |
| `src/.../ViewModels/CatalogModItemViewModel.cs` | Effective category/tags for browse rows |
| `src/.../ViewModels/ModItemViewModel.cs` | Effective category/tags; enrich preserves overrides |
| `src/.../MainWindow.xaml` | Filter bars, Category column, tags in detail |
| `src/.../Dialogs/EditModTaxonomyDialog.xaml(.cs)` | Library override/extraTags editor |
| `src/.../Services/GitHubCommunityLinks.cs` | Submit body `category:` / `tags:` lines |
| `src/.../Resources/Strings*.resx` + `UiStrings.cs` + `docs/i18n/source-zh-CN.tsv` | Labels |
| `MechabellumMods/catalog.json` + `scripts/validate_catalog.py` + `docs/submit.html` + `README.md` | Catalog data + docs |
| `*.csproj` / `installer/*.iss` / `release/v1.0.6/**` | Version + ship |

---

### Task 1: ModCategory + ModTaxonomy + unit tests

**Files:**
- Create: `src/MechabellumModManager/Models/ModCategory.cs`
- Create: `src/MechabellumModManager/Services/ModTaxonomy.cs`
- Create: `tests/MechabellumModManager.Tests/ModTaxonomyTests.cs`

**Interfaces:**
- Produces:
  - `enum ModCategory { OverlayUI, QoL, Camera, CombatAssist, Economy, ReplayDebug, Misc, Uncategorized }`
  - `ModTaxonomy.TryParseCategory(string? value, out ModCategory category) : bool` — true only for catalog-writable values; blank/null → false; invalid → false
  - `ModTaxonomy.ParseCategoryOrUncategorized(string? value) : ModCategory` — invalid/blank → `Uncategorized`
  - `ModTaxonomy.IsCatalogWritable(ModCategory c) : bool` — false for `Uncategorized`
  - `ModTaxonomy.NormalizeTags(IEnumerable<string>? tags) : IReadOnlyList<string>` — trim, drop empty, Ordinal dedupe preserving first-seen order
  - `ModTaxonomy.ResolveEffectiveCategory(string? categoryOverride, string? catalogCategory) : ModCategory`
  - `ModTaxonomy.ResolveEffectiveTags(IEnumerable<string>? catalogTags, IEnumerable<string>? extraTags) : IReadOnlyList<string>`
  - `ModTaxonomy.AllFilterCategories` — writable enums + Uncategorized (UI “All” is a separate null sentinel in VM)

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ModTaxonomyTests
{
    [Theory]
    [InlineData("OverlayUI", ModCategory.OverlayUI)]
    [InlineData("QoL", ModCategory.QoL)]
    [InlineData("Camera", ModCategory.Camera)]
    [InlineData("CombatAssist", ModCategory.CombatAssist)]
    [InlineData("Economy", ModCategory.Economy)]
    [InlineData("ReplayDebug", ModCategory.ReplayDebug)]
    [InlineData("Misc", ModCategory.Misc)]
    public void TryParseCategory_accepts_catalog_values(string raw, ModCategory expected)
    {
        ModTaxonomy.TryParseCategory(raw, out var cat).Should().BeTrue();
        cat.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Uncategorized")]
    [InlineData("overlayui")]
    [InlineData("Unknown")]
    public void TryParseCategory_rejects_blank_uncategorized_and_invalid(string? raw)
    {
        ModTaxonomy.TryParseCategory(raw, out _).Should().BeFalse();
        ModTaxonomy.ParseCategoryOrUncategorized(raw).Should().Be(ModCategory.Uncategorized);
    }

    [Fact]
    public void ResolveEffectiveCategory_override_wins_over_catalog()
    {
        ModTaxonomy.ResolveEffectiveCategory("Camera", "QoL").Should().Be(ModCategory.Camera);
        ModTaxonomy.ResolveEffectiveCategory(null, "QoL").Should().Be(ModCategory.QoL);
        ModTaxonomy.ResolveEffectiveCategory("bogus", "QoL").Should().Be(ModCategory.QoL);
        ModTaxonomy.ResolveEffectiveCategory("bogus", "also-bad").Should().Be(ModCategory.Uncategorized);
        ModTaxonomy.ResolveEffectiveCategory(null, null).Should().Be(ModCategory.Uncategorized);
    }

    [Fact]
    public void ResolveEffectiveTags_merges_catalog_then_extra_deduped()
    {
        var tags = ModTaxonomy.ResolveEffectiveTags(
            new[] { " hud ", "grid", "hud" },
            new[] { "grid", "hotkey", "  " });
        tags.Should().Equal("hud", "grid", "hotkey");
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~ModTaxonomyTests -c Release`

Expected: FAIL (type/namespace not found)

- [ ] **Step 3: Implement enum + helpers**

`ModCategory.cs`:

```csharp
namespace MechabellumModManager.Models;

public enum ModCategory
{
    OverlayUI,
    QoL,
    Camera,
    CombatAssist,
    Economy,
    ReplayDebug,
    Misc,
    Uncategorized
}
```

`ModTaxonomy.cs`:

```csharp
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public static class ModTaxonomy
{
    public static readonly IReadOnlyList<ModCategory> CatalogWritableCategories =
    [
        ModCategory.OverlayUI, ModCategory.QoL, ModCategory.Camera,
        ModCategory.CombatAssist, ModCategory.Economy, ModCategory.ReplayDebug,
        ModCategory.Misc
    ];

    public static IReadOnlyList<ModCategory> AllFilterCategories { get; } =
        CatalogWritableCategories.Append(ModCategory.Uncategorized).ToArray();

    public static bool IsCatalogWritable(ModCategory c) =>
        c != ModCategory.Uncategorized;

    public static bool TryParseCategory(string? value, out ModCategory category)
    {
        category = ModCategory.Uncategorized;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Enum.TryParse(value.Trim(), ignoreCase: false, out ModCategory parsed))
            return false;
        if (!IsCatalogWritable(parsed)) return false;
        category = parsed;
        return true;
    }

    public static ModCategory ParseCategoryOrUncategorized(string? value) =>
        TryParseCategory(value, out var c) ? c : ModCategory.Uncategorized;

    public static ModCategory ResolveEffectiveCategory(string? categoryOverride, string? catalogCategory)
    {
        if (TryParseCategory(categoryOverride, out var o)) return o;
        if (TryParseCategory(catalogCategory, out var c)) return c;
        return ModCategory.Uncategorized;
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return Array.Empty<string>();
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in tags)
        {
            var s = (t ?? "").Trim();
            if (s.Length == 0) continue;
            if (seen.Add(s)) list.Add(s);
        }
        return list;
    }

    public static IReadOnlyList<string> ResolveEffectiveTags(
        IEnumerable<string>? catalogTags,
        IEnumerable<string>? extraTags) =>
        NormalizeTags((catalogTags ?? Array.Empty<string>()).Concat(extraTags ?? Array.Empty<string>()));
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: same filter as Step 2. Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/MechabellumModManager/Models/ModCategory.cs src/MechabellumModManager/Services/ModTaxonomy.cs tests/MechabellumModManager.Tests/ModTaxonomyTests.cs
git commit -m "feat: add ModCategory taxonomy helpers and tests"
```

---

### Task 2: CatalogMod + validate_catalog.py + seed catalog.json

**Files:**
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs` (`CatalogMod`)
- Modify: `tests/MechabellumModManager.Tests/ModCatalogServiceTests.cs`
- Modify (catalog repo): `D:\gongzuo\MechabellumMods\catalog.json`
- Modify (catalog repo): `D:\gongzuo\MechabellumMods\scripts\validate_catalog.py`

**Interfaces:**
- Consumes: `ModTaxonomy.TryParseCategory`, `NormalizeTags`
- Produces: `CatalogMod.Category : string?`, `CatalogMod.Tags : List<string>?` (JSON `category` / `tags`)
- Validator accepts optional `category` (writable enum string) and `tags` (string array)

**Seed mapping (spec §9) + suggested tags:**

| id | category | tags (suggested) |
|----|----------|------------------|
| show-grid | OverlayUI | grid, hud |
| damage-rank | OverlayUI | hud, damage |
| quick-item | QoL | hotkey, items |
| undo-plus | QoL | hotkey, undo |
| quick-camera | Camera | camera, hotkey |
| auto-speed | CombatAssist | combat, speed |
| sales-calculation | Economy | economy, calc |
| replay-tool | ReplayDebug | replay, debug |

- [ ] **Step 1: Extend CatalogMod + deserialization test**

Add to `CatalogMod`:

```csharp
[JsonPropertyName("category")]
public string? Category { get; set; }

[JsonPropertyName("tags")]
public List<string>? Tags { get; set; }
```

Test (in `ModCatalogServiceTests`):

```csharp
[Fact]
public void DeserializeCatalog_reads_category_and_tags()
{
    var json = """
    {"updatedAt":"t","mods":[{"id":"show-grid","name":"G","file":"mods/show-grid/ShowGrid.dll","category":"OverlayUI","tags":["grid","hud"]}]}
    """;
    var root = ModCatalogService.DeserializeCatalog(json);
    root.Mods[0].Category.Should().Be("OverlayUI");
    root.Mods[0].Tags.Should().Equal("grid", "hud");
}
```

- [ ] **Step 2: Run test — FAIL then implement properties — PASS**

Run: `dotnet test --filter FullyQualifiedName~DeserializeCatalog_reads_category_and_tags -c Release`

- [ ] **Step 3: Seed MechabellumMods `catalog.json`**

For each mod in the table, set `"category"` and `"tags"`. Unlisted future mods: omit category (Uncategorized) or set `Misc` if intentional.

- [ ] **Step 4: Update `validate_catalog.py`**

```python
ALLOWED_CATEGORIES = {
    "OverlayUI", "QoL", "Camera", "CombatAssist",
    "Economy", "ReplayDebug", "Misc",
}

# inside per-mod loop:
cat = mod.get("category")
if cat is not None:
    if not isinstance(cat, str) or cat.strip() not in ALLOWED_CATEGORIES:
        errors.append(f"id={mod.get('id')!r}: invalid category: {cat!r}")

tags = mod.get("tags")
if tags is not None:
    if not isinstance(tags, list) or any(not isinstance(t, str) for t in tags):
        errors.append(f"id={mod.get('id')!r}: tags must be a string array")
```

- [ ] **Step 5: Validate catalog**

Run: `python D:\gongzuo\MechabellumMods\scripts\validate_catalog.py`

Expected: `OK: N mods, ids unique, files present`

- [ ] **Step 6: Commit both repos**

Manager:

```bash
git add src/MechabellumModManager/Services/ModCatalogService.cs tests/MechabellumModManager.Tests/ModCatalogServiceTests.cs
git commit -m "feat: deserialize catalog category and tags"
```

Catalog (`D:\gongzuo\MechabellumMods`):

```bash
git add catalog.json scripts/validate_catalog.py
git commit -m "feat: seed mod categories/tags and validate them"
```

---

### Task 3: ModPackage persist categoryOverride/extraTags; preserve on enrich

**Files:**
- Modify: `src/MechabellumModManager/Models/ModPackage.cs`
- Modify: `src/MechabellumModManager/Services/ModLibraryService.cs` (`PackageMeta`, `WritePackageJson`, `TryLoadPackage`)
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (`PersistPackageMeta`)
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs` (`ApplyCatalogEnrichment`, effective props)
- Create: `tests/MechabellumModManager.Tests/ModPackageTaxonomyPersistTests.cs` (or extend `ModLibraryImportTests`)

**Interfaces:**
- Consumes: `ModTaxonomy.ResolveEffective*`
- Produces:
  - `ModPackage.CategoryOverride : string?`
  - `ModPackage.ExtraTags : List<string>?`
  - Runtime-only (not serialized): `CatalogCategory`, `CatalogTags` set during enrich
  - `ApplyCatalogEnrichment` must **not** clear/overwrite `CategoryOverride` / `ExtraTags`

- [ ] **Step 1: Failing persist/round-trip + enrich-preserve test**

```csharp
[Fact]
public void WriteAndLoad_preserves_categoryOverride_and_extraTags()
{
    // Use temp library PathsService pattern from ModLibraryImportTests:
    // set CategoryOverride = "Camera", ExtraTags = ["mine"], write, reload, assert.
}

[Fact]
public void ApplyCatalogEnrichment_does_not_clear_local_taxonomy()
{
    var pkg = new ModPackage { CategoryOverride = "Camera", ExtraTags = new List<string> { "mine" } };
    var item = /* construct ModItemViewModel with owner stub or call Apply on package fields via VM */;
    // After ApplyCatalogEnrichment(catalog with QoL + tags ["hud"]):
    // CategoryOverride still Camera; ExtraTags still contains mine;
    // CatalogCategory/CatalogTags updated from catalog.
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Extend model + PackageMeta + both write paths**

```csharp
// ModPackage persisted:
public string? CategoryOverride { get; set; }
public List<string>? ExtraTags { get; set; }

// Runtime only — omit from PackageMeta / PersistPackageMeta:
[System.Text.Json.Serialization.JsonIgnore]
public string? CatalogCategory { get; set; }
[System.Text.Json.Serialization.JsonIgnore]
public List<string>? CatalogTags { get; set; }
```

Update `WritePackageJson`, `TryLoadPackage`, `PersistPackageMeta`.

In `ApplyCatalogEnrichment`:

```csharp
Package.CatalogCategory = catalog.Category;
Package.CatalogTags = catalog.Tags is null ? null : new List<string>(catalog.Tags);
// do not assign CategoryOverride / ExtraTags
```

Add effective helpers on `ModItemViewModel` / `CatalogModItemViewModel` using `ModTaxonomy`.

- [ ] **Step 4: Tests PASS**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: persist local categoryOverride and extraTags"
```

---

### Task 4: MainViewModel filter state + filtered views

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs`
- Create: `src/MechabellumModManager/Services/ModListFilter.cs` (optional pure helpers)
- Create: `tests/MechabellumModManager.Tests/ModListFilterTests.cs`

**Interfaces:**
- Session-local props:
  - Catalog: `CatalogSearchText`, `ModCategory? CatalogCategoryFilter` (null = All), `string? CatalogTagFilter`, `ModSortMode CatalogSortMode`
  - Library: parallel `Library*` props
  - `enum ModSortMode { NameAsc, UpdatedAtDesc }` — default `NameAsc` (UI: 名称 / 更新新→旧)
- Prefer `ICollectionView` over source `CatalogMods` / `Mods` via `CollectionViewSource.GetDefaultView`
- `RefreshCatalogView()` / `RefreshLibraryView()` after load/enrich/import/delete
- Dynamic `CatalogAvailableTags` / `LibraryAvailableTags` from effective tags

Search: case-insensitive contains on Name/DisplayName, Author, Summary (and Id).

AND: search ∩ category ∩ tag; then sort (`CurrentCultureIgnoreCase` for name; UpdatedAt best-effort date parse, nulls last, Desc).

- [ ] **Step 1: Write failing matcher tests**

```csharp
public static class ModListFilter
{
    public static bool MatchesSearch(string? search, params string?[] fields);
    public static bool MatchesCategory(ModCategory? filter, ModCategory effective);
    public static bool MatchesTag(string? filterTag, IEnumerable<string> effectiveTags);
}
```

- [ ] **Step 2: FAIL → implement matcher + VM wiring → PASS**

Keep mutating `CatalogMods`/`Mods`; bind DataGrids to views in Task 5.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add catalog and library filter/sort state"
```

---

### Task 5: MainWindow.xaml filter bars + Category column + tags in detail

**Files:**
- Modify: `src/MechabellumModManager/MainWindow.xaml`
- Modify item VMs for `EffectiveCategoryDisplay` / `EffectiveTagsText` if needed
- Modify smoke tests only if they assert column structure

**Interfaces:**
- Consumes VM filter props + effective display strings
- Duplicate shared filter-bar pattern under Browse and Library (no new ResourceDictionary required)

- [ ] **Step 1: Insert filter bars** (Browse above DataGrid; Library between header and DataGrid)

```xml
<DockPanel Margin="0,0,0,8" LastChildFill="True">
  <TextBox Width="160" Margin="0,0,8,0"
           Text="{Binding CatalogSearchText, UpdateSourceTrigger=PropertyChanged}" />
  <ComboBox Width="140" Margin="0,0,8,0"
            ItemsSource="{Binding CatalogCategoryFilterOptions}"
            SelectedItem="{Binding SelectedCatalogCategoryFilter}" />
  <ComboBox Width="120" Margin="0,0,8,0"
            ItemsSource="{Binding CatalogAvailableTagOptions}"
            SelectedItem="{Binding SelectedCatalogTagFilter}" />
  <ComboBox Width="140"
            ItemsSource="{Binding SortModeOptions}"
            SelectedItem="{Binding SelectedCatalogSortMode}" />
</DockPanel>
```

Library binds `Library*` equivalents.

```xml
ItemsSource="{Binding CatalogModsView}"
<!-- library: -->
ItemsSource="{Binding LibraryModsView}"
```

- [ ] **Step 2: Add Category column** on both grids

```xml
<DataGridTextColumn Header="{Binding DataContext.Ui.ColumnCategory, RelativeSource={RelativeSource AncestorType=Window}}"
                    Binding="{Binding EffectiveCategoryDisplay}"
                    Width="100" />
```

- [ ] **Step 3: Detail panels** — show effective tags under summary (Browse + Library), prefixed with `Ui.TagsLabel`

- [ ] **Step 4: Build**

Run: `dotnet build src/MechabellumModManager/MechabellumModManager.csproj -c Release`

Expected: 0 errors. Smoke: search/category/tag/sort; selection still works.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add filter bars, category column, and tags in detail"
```

---

### Task 6: Library edit UI for override / extraTags

**Files:**
- Create: `src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml`
- Create: `src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml.cs`
- Modify: `App.xaml.cs` / `MainWindow.xaml.cs` dialog factory (same pattern as Report/TypePick)
- Modify: `MainViewModel.cs` — `EditLibraryModTaxonomyCommand` + prompt func

**Interfaces:**
- Dialog returns `(string? categoryOverride, IReadOnlyList<string> extraTags)?` — null cancel; empty override clears (follow catalog)
- Combo: writable categories + “跟随目录 / Clear override”
- Extra tags: comma-separated TextBox → `ModTaxonomy.NormalizeTags` on save

- [ ] **Step 1: Implement dialog** mirroring `TypePickDialog` chrome

- [ ] **Step 2: Wire command**

```csharp
[RelayCommand(CanExecute = nameof(CanEditLibraryTaxonomy))]
void EditLibraryModTaxonomy()
{
    var mod = SelectedLibraryMod;
    if (mod is null || mod.IsMissing) return;
    var result = _promptEditTaxonomy?.(mod.Package);
    if (result is null) return;
    mod.Package.CategoryOverride = result.Value.Override;
    mod.Package.ExtraTags = result.Value.ExtraTags.ToList();
    PersistPackageMeta(mod.Package);
    mod.NotifyDetailChanged();
    RefreshLibraryView();
}
```

- [ ] **Step 3: Test that save writes `categoryOverride` into package.json**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: edit library category override and extra tags"
```

---

### Task 7: i18n Strings (zh/en/ja/de/ru + TSV)

**Files:**
- Modify: `docs/i18n/source-zh-CN.tsv`
- Modify: `src/MechabellumModManager/Resources/Strings.resx` (+ `.en` `.ja` `.de` `.ru`)
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs`
- Replace remaining hard-coded filter/edit literals from Tasks 5–6 with `Ui.*`

**Keys (minimum):**

| Key | zh-CN |
|-----|-------|
| FilterSearch | 搜索 |
| FilterCategory | 分类 |
| FilterTag | 标签 |
| FilterSort | 排序 |
| FilterAll | 全部 |
| SortByName | 名称 |
| SortByUpdatedAtDesc | 更新（新→旧） |
| ColumnCategory | 分类 |
| TagsLabel | 标签 |
| CategoryOverlayUI | 界面覆盖 |
| CategoryQoL | 生活质量 |
| CategoryCamera | 镜头 |
| CategoryCombatAssist | 战斗辅助 |
| CategoryEconomy | 经济 |
| CategoryReplayDebug | 回放调试 |
| CategoryMisc | 杂项 |
| CategoryUncategorized | 未分类 |
| EditModTaxonomy | 编辑分类/标签 |
| CategoryFollowCatalog | 跟随目录 |
| ExtraTagsHint | 额外标签（逗号分隔） |

- [ ] **Step 1: Add keys to TSV + all resx + UiStrings**

- [ ] **Step 2: Map `ModCategory` → localized label via UiStrings helper**

- [ ] **Step 3: `dotnet test -c Release` / build PASS**

- [ ] **Step 4: Commit**

```bash
git commit -m "i18n: add category and filter bar strings"
```

---

### Task 8: Email submit body + submit.html / README

**Files:**
- Modify: `src/MechabellumModManager/Services/GitHubCommunityLinks.cs` (`BuildSubmitBody`; optionally update body)
- Modify: `tests/MechabellumModManager.Tests/GitHubCommunityLinksTests.cs`
- Modify: `D:\gongzuo\MechabellumMods\docs\submit.html`
- Modify: `D:\gongzuo\MechabellumMods\README.md` (投稿模板)

**Interfaces:**
- Add bilingual lines consistent with existing template:

```
【分类 / Category】（OverlayUI / QoL / Camera / CombatAssist / Economy / ReplayDebug / Misc，可空）
【标签 / Tags】（逗号分隔，可空）
```

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public void BuildSubmitBody_includes_category_and_tags_lines()
{
    var body = GitHubCommunityLinks.BuildSubmitBody("Demo");
    body.Should().Contain("Category");
    body.Should().Contain("Tags");
}
```

- [ ] **Step 2: Implement body + brief HTML/README updates**

- [ ] **Step 3: Tests PASS; commit both repos**

```bash
git commit -m "feat: add category/tags lines to submit email body"
# in MechabellumMods:
git commit -m "docs: mention category and tags in submit templates"
```

---

### Task 9: Version 1.0.6, tests, package, dual-repo commit, gh release

**Files:**
- Modify: `src/MechabellumModManager/MechabellumModManager.csproj` → `<Version>1.0.6</Version>`
- Modify: `installer/MechabellumModManager.iss` → `#define MyAppVersion "1.0.6"`
- Create: `release/v1.0.6/` per `docs/releasing.md`
- Create: `release/v1.0.6/latest.json`
- Update: `release/README.md` current pointer → v1.0.6

- [ ] **Step 1: Bump csproj + iss**

- [ ] **Step 2: Full tests**

Run: `dotnet test -c Release` — all PASS

- [ ] **Step 3: Build installer + portable**

```powershell
.\installer\build-installer.bat
# requires installer\redist\melonloader\MelonLoader.x64.zip
```

Arrange:

```
release/v1.0.6/
  安装包/MechabellumModManager_Setup_v1.0.6.exe (+ README.txt)
  本体/MechabellumModManager.exe + Assets/ (+ README.txt)
  MechabellumModManager_portable_v1.0.6.zip
  latest.json
```

`latest.json` notes should mention category/search/filter + local overrides + submit template.

- [ ] **Step 4: Commit + push manager**

```bash
git add src/MechabellumModManager/MechabellumModManager.csproj installer/MechabellumModManager.iss release/v1.0.6 release/README.md
git commit -m "release: ship 1.0.6 with mod category search"
git push origin master
```

- [ ] **Step 5: Push MechabellumMods** if catalog commits pending

```bash
cd D:\gongzuo\MechabellumMods
git push origin master
```

- [ ] **Step 6: `gh release create v1.0.6`** with Setup, portable zip, `latest.json`

- [ ] **Step 7: Verify** update check + `validate_catalog.py`

---

## Self-review (plan vs spec)

| Spec section | Task |
|--------------|------|
| §3 Category enum + Uncategorized | Task 1 |
| §4.1 catalog category/tags | Task 2 |
| §4.2–4.3 override/extraTags + enrich preserve | Task 3 |
| §5.1 filter bar AND + sort | Task 4–5 |
| §5.2 Category column, tags detail, library edit | Task 5–6 |
| §5.3 session-only filters | Task 4 (no config persistence) |
| §6 validation/logging | Task 1–2 (+ log invalid category on load) |
| §7 email template | Task 8 |
| §8 out of scope | no Type changes; no tag cloud; no sidebar |
| §9 seed mapping | Task 2 |
| §10 i18n + version after 1.0.5 | Task 7 + Task 9 → **1.0.6** |

**Logging:** On catalog/package load, if category present but `TryParseCategory` fails, log once per id (`AppendLog` / debug) without failing load.

---

## Execution handoff

Plan is ready for **subagent-driven-development** (recommended) or **executing-plans**. Do not implement feature code until execution is explicitly chosen.