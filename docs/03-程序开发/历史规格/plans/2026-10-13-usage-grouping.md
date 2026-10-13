# Mod 列表按用途分栏（赛事专用 / 日常化）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mod 浏览和 Mod 库两张列表按 `ModUsage`（赛事专用 Mod / 日常化 Mod / 未判定）分组显示，加「用途」筛选，本地可覆盖，社区目录增加 `usage` 字段。

**Architecture:** `ModUsage` 是独立于 `ModCategory` 的新枚举，生效规则与分类相同（本地覆盖 > 目录 > 未判定），放在 `ModTaxonomy`。两个 `ICollectionView` 各加一条 `PropertyGroupDescription("EffectiveUsage")`，组顺序由分组描述自己的 `SortDescriptions`（按枚举值）决定，所以现有 `ApplySort` 比较器和列头排序都不用改。界面只加 `GroupStyle` 标题模板、筛选下拉和编辑对话框一档。

**Tech Stack:** .NET 8 WPF（`net8.0-windows`），CommunityToolkit.Mvvm，xUnit + FluentAssertions；社区目录侧 Python 3.12 `unittest`。

**Spec:** `docs/03-程序开发/历史规格/specs/2026-10-13-usage-grouping-design.md`

**Working branch:** 管理器仓库 `feat/usage-grouping`；社区目录仓库（`D:\gongzuo\钢铁指挥官\社区目录`）`feat/usage-field`。

## Global Constraints

- 枚举名与声明顺序固定：`Tournament`、`Casual`、`Unrated`。只有 `Tournament`、`Casual` 能写进 `catalog.json` 或 `package.json`。
- 目录字段名 `usage`；本地字段名 `usageOverride`（`package.json` 走现有 camelCase 序列化）。
- 解析区分大小写，先 `Trim()`；数字字符串（如 `"0"`）必须拒绝。
- 中文文案：`赛事专用 Mod`、`日常化 Mod`、`未判定`、`用途`、`跟随目录`。其它语言文案以 Task 2 的脚本为准，与规格「文案」表一致。
- 非法值只写日志，不弹窗、不阻断目录加载。
- 不改 `ApplySort`、不改 `LogicFrameGrade` 列、不加「用途」列、不改投稿邮件模板、不上调版本号、不打包、不发版。
- 旧版管理器读到 `usage` 字段会忽略它（System.Text.Json 默认忽略未知字段），所以目录可以先于管理器发布。
- 两个仓库的工作区里都有用户尚未提交的改动。**每次提交只 `git add` 本任务列出的文件路径，绝不 `git add -A` / `git add .`**。提交前用 `git diff --cached --stat` 确认暂存区只有本任务的文件。
- 不 push、不开 PR、不合并。
- **这是测试版本，不发布到仓库**：两个仓库都只做本地提交。不 `git push`，不运行 `tools/sync-mirror.ps1` / `tools/publish-mods-release.ps1`，不改 `release/latest.json`，不上传 GitHub Release，不打安装包。
- PowerShell 5.1：命令之间用 `;`，不要用 `&&`。

测试命令统一写法（在管理器仓库根目录运行）：

```powershell
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~<类名>"
```

---

### Task 0: 建分支

- [ ] **Step 1: 管理器仓库建分支**

Run（工作目录 `D:\gongzuo\钢铁指挥官mod管理器开发`）：

```powershell
git switch -c feat/usage-grouping; git branch --show-current
```

Expected: `feat/usage-grouping`。工作区里用户原有的未提交改动会跟着留在工作区，不要动它们。

---

### Task 1: `ModUsage` 枚举、解析与筛选纯函数

**Files:**
- Create: `src/MechabellumModManager/Models/ModUsage.cs`
- Modify: `src/MechabellumModManager/Services/ModTaxonomy.cs`（在 `ResolveEffectiveCategory` 之后插入）
- Modify: `src/MechabellumModManager/Services/ModListFilter.cs`（在 `MatchesCategory` 之后插入）
- Test: `tests/MechabellumModManager.Tests/ModTaxonomyTests.cs`、`tests/MechabellumModManager.Tests/ModListFilterTests.cs`

**Interfaces:**
- Produces:
  - `enum MechabellumModManager.Models.ModUsage { Tournament, Casual, Unrated }`
  - `ModTaxonomy.CatalogWritableUsages : IReadOnlyList<ModUsage>` = `[Tournament, Casual]`
  - `ModTaxonomy.TryParseUsage(string? value, out ModUsage usage) : bool`
  - `ModTaxonomy.ResolveEffectiveUsage(string? usageOverride, string? catalogUsage) : ModUsage`
  - `ModTaxonomy.UsageResourceKey(ModUsage usage) : string`（返回 `"UsageTournament"` / `"UsageCasual"` / `"UsageUnrated"`）
  - `ModListFilter.MatchesUsage(ModUsage? filter, ModUsage effective) : bool`

- [ ] **Step 1: 写失败测试**

在 `tests/MechabellumModManager.Tests/ModTaxonomyTests.cs` 类末尾（最后一个 `}` 之前）加：

```csharp
    [Theory]
    [InlineData("Tournament", ModUsage.Tournament)]
    [InlineData("Casual", ModUsage.Casual)]
    [InlineData("  Casual ", ModUsage.Casual)]
    public void TryParseUsage_accepts_catalog_values(string raw, ModUsage expected)
    {
        ModTaxonomy.TryParseUsage(raw, out var usage).Should().BeTrue();
        usage.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unrated")]
    [InlineData("tournament")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Neutral")]
    public void TryParseUsage_rejects_blank_unrated_numeric_and_invalid(string? raw)
    {
        ModTaxonomy.TryParseUsage(raw, out var usage).Should().BeFalse();
        usage.Should().Be(ModUsage.Unrated);
    }

    [Fact]
    public void ResolveEffectiveUsage_override_wins_over_catalog()
    {
        ModTaxonomy.ResolveEffectiveUsage("Casual", "Tournament").Should().Be(ModUsage.Casual);
        ModTaxonomy.ResolveEffectiveUsage(null, "Tournament").Should().Be(ModUsage.Tournament);
        ModTaxonomy.ResolveEffectiveUsage("bogus", "Tournament").Should().Be(ModUsage.Tournament);
        ModTaxonomy.ResolveEffectiveUsage("bogus", "also-bad").Should().Be(ModUsage.Unrated);
        ModTaxonomy.ResolveEffectiveUsage(null, null).Should().Be(ModUsage.Unrated);
    }

    [Fact]
    public void ModUsage_declaration_order_is_group_order()
    {
        Enum.GetValues<ModUsage>().Should().Equal(ModUsage.Tournament, ModUsage.Casual, ModUsage.Unrated);
        ModTaxonomy.CatalogWritableUsages.Should().Equal(ModUsage.Tournament, ModUsage.Casual);
    }

    [Theory]
    [InlineData(ModUsage.Tournament, "UsageTournament")]
    [InlineData(ModUsage.Casual, "UsageCasual")]
    [InlineData(ModUsage.Unrated, "UsageUnrated")]
    public void UsageResourceKey_maps_each_value(ModUsage usage, string key)
    {
        ModTaxonomy.UsageResourceKey(usage).Should().Be(key);
    }
```

在 `tests/MechabellumModManager.Tests/ModListFilterTests.cs` 的 `MatchesCategory_null_filter_means_all` 之后加：

```csharp
    [Fact]
    public void MatchesUsage_null_filter_means_all()
    {
        ModListFilter.MatchesUsage(null, ModUsage.Casual).Should().BeTrue();
        ModListFilter.MatchesUsage(ModUsage.Casual, ModUsage.Casual).Should().BeTrue();
        ModListFilter.MatchesUsage(ModUsage.Casual, ModUsage.Tournament).Should().BeFalse();
        ModListFilter.MatchesUsage(ModUsage.Unrated, ModUsage.Unrated).Should().BeTrue();
    }
```

- [ ] **Step 2: 运行，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ModTaxonomyTests|FullyQualifiedName~ModListFilterTests"`
Expected: 编译失败，`ModUsage` / `TryParseUsage` / `MatchesUsage` 不存在。

- [ ] **Step 3: 实现**

新建 `src/MechabellumModManager/Models/ModUsage.cs`：

```csharp
namespace MechabellumModManager.Models;

/// <summary>Declaration order is the list group order.</summary>
public enum ModUsage
{
    Tournament,
    Casual,
    Unrated
}
```

`src/MechabellumModManager/Services/ModTaxonomy.cs`：在 `ResolveEffectiveCategory` 方法的结束 `}` 之后插入：

```csharp

    public static readonly IReadOnlyList<ModUsage> CatalogWritableUsages =
        [ModUsage.Tournament, ModUsage.Casual];

    /// <summary>
    /// Matches enum names only. Enum.TryParse would also accept "0" or "1".
    /// </summary>
    public static bool TryParseUsage(string? value, out ModUsage usage)
    {
        usage = ModUsage.Unrated;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        foreach (var candidate in CatalogWritableUsages)
        {
            if (string.Equals(candidate.ToString(), trimmed, StringComparison.Ordinal))
            {
                usage = candidate;
                return true;
            }
        }
        return false;
    }

    public static ModUsage ResolveEffectiveUsage(string? usageOverride, string? catalogUsage)
    {
        if (TryParseUsage(usageOverride, out var o)) return o;
        if (TryParseUsage(catalogUsage, out var c)) return c;
        return ModUsage.Unrated;
    }

    public static string UsageResourceKey(ModUsage usage) => usage switch
    {
        ModUsage.Tournament => "UsageTournament",
        ModUsage.Casual => "UsageCasual",
        _ => "UsageUnrated"
    };
```

`src/MechabellumModManager/Services/ModListFilter.cs`：在 `MatchesCategory` 之后插入：

```csharp

    public static bool MatchesUsage(ModUsage? filter, ModUsage effective) =>
        filter is null || filter == effective;
```

- [ ] **Step 4: 运行，确认通过**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ModTaxonomyTests|FullyQualifiedName~ModListFilterTests"`
Expected: 全部 PASS。

- [ ] **Step 5: Commit**

```powershell
git add -- src/MechabellumModManager/Models/ModUsage.cs src/MechabellumModManager/Services/ModTaxonomy.cs src/MechabellumModManager/Services/ModListFilter.cs tests/MechabellumModManager.Tests/ModTaxonomyTests.cs tests/MechabellumModManager.Tests/ModListFilterTests.cs
git diff --cached --stat
git commit -m "feat: add ModUsage with parse, resolve and filter helpers."
```

---

### Task 2: 文案、数据字段、生效用途与非法值日志

**Files:**
- Create（临时，用完删除）: `tools/_tmp-usage-keys.ps1`
- Modify: `src/MechabellumModManager/Resources/Strings.resx`、`Strings.en.resx`、`Strings.de.resx`、`Strings.ja.resx`、`Strings.ru.resx`（由脚本写入）
- Modify: `src/MechabellumModManager/ViewModels/UiStrings.cs`（`FilterCategory` 行附近、`CategoryFollowCatalog` 行附近、`CategoryLabel` 方法之后）
- Modify: `src/MechabellumModManager/Models/ModPackage.cs`（`ExtraTags` 之后、`CatalogTags` 之后）
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs`（`CatalogMod.Tags` 之后，约第 147 行）
- Modify: `src/MechabellumModManager/Services/ModLibraryService.cs`（`SavePackageMeta` 约 642 行、`TryLoadPackage` 约 678 行、`PackageMeta` 约 949 行）
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs`
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`（库加载循环约 5046 行、`LogInvalidCatalogCategory` 约 6967 行）
- Test: `tests/MechabellumModManager.Tests/ModUsageDataTests.cs`（新建）

**Interfaces:**
- Consumes: Task 1 的 `ModUsage`、`ModTaxonomy.TryParseUsage`、`ResolveEffectiveUsage`、`UsageResourceKey`。
- Produces:
  - 资源键：`UsageTournament`、`UsageCasual`、`UsageUnrated`、`FilterUsage`、`UsageFollowCatalog`、`LogInvalidUsageOverride`、`LogInvalidCatalogUsage`
  - `UiStrings.FilterUsage : string`、`UiStrings.UsageFollowCatalog : string`、`UiStrings.UsageLabel(ModUsage) : string`
  - `ModPackage.UsageOverride : string?`（持久化）、`ModPackage.CatalogUsage : string?`（`[JsonIgnore]`）
  - `CatalogMod.Usage : string?`（JSON `usage`）
  - `ModItemViewModel.EffectiveUsage : ModUsage`、`CatalogModItemViewModel.EffectiveUsage : ModUsage`

- [ ] **Step 1: 写失败测试**

新建 `tests/MechabellumModManager.Tests/ModUsageDataTests.cs`：

```csharp
using System.Text.Json;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class ModUsageDataTests
{
    [Fact]
    public void WriteAndLoad_preserves_usageOverride()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-usage-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var store = new JsonStore();
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var lib = new ModLibraryService(paths, new AssemblyInspector(), store, profiles);

            var pkgId = "usage-test-aaaaaaaa";
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", pkgId);
            Directory.CreateDirectory(pkgDir);
            File.WriteAllBytes(Path.Combine(pkgDir, "Usage.dll"), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                """
                {
                  "id": "usage-test-aaaaaaaa",
                  "displayName": "Usage Test",
                  "type": "melon_mod",
                  "usageOverride": "Tournament",
                  "files": [ { "relativePathInPackage": "Usage.dll", "sha256": "aabb" } ]
                }
                """);
            store.Save(Path.Combine(paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });

            var loaded = lib.List().Should().ContainSingle().Subject;
            loaded.UsageOverride.Should().Be("Tournament");

            loaded.UsageOverride = "Casual";
            lib.SavePackageMeta(loaded);

            lib.List().Should().ContainSingle().Subject.UsageOverride.Should().Be("Casual");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(pkgDir, "package.json")));
            doc.RootElement.GetProperty("usageOverride").GetString().Should().Be("Casual");
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Catalog_json_usage_is_read_and_invalid_value_shows_unrated()
    {
        var root = JsonSerializer.Deserialize<CatalogRoot>(
            """
            {
              "mods": [
                { "id": "a", "name": "A", "file": "mods/a/A.dll", "usage": "Casual" },
                { "id": "b", "name": "B", "file": "mods/b/B.dll", "usage": "0" },
                { "id": "c", "name": "C", "file": "mods/c/C.dll" }
              ]
            }
            """)!;

        root.Mods.Select(m => m.Usage).Should().Equal("Casual", "0", null);
        root.Mods
            .Select(m => new CatalogModItemViewModel(m, CatalogEntryState.NotInstalled).EffectiveUsage)
            .Should().Equal(ModUsage.Casual, ModUsage.Unrated, ModUsage.Unrated);
    }

    [Fact]
    public void ApplyCatalogEnrichment_keeps_local_usage_override()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        var overridden = new ModItemViewModel(vm, new ModPackage
        {
            Id = "x",
            DisplayName = "X",
            Type = ModPackageType.MelonMod,
            UsageOverride = "Tournament"
        }, isEnabled: false);
        overridden.ApplyCatalogEnrichment(new CatalogMod { Id = "x", Name = "X", Usage = "Casual" });

        overridden.Package.UsageOverride.Should().Be("Tournament");
        overridden.Package.CatalogUsage.Should().Be("Casual");
        overridden.EffectiveUsage.Should().Be(ModUsage.Tournament);

        var following = new ModItemViewModel(vm, new ModPackage
        {
            Id = "y",
            DisplayName = "Y",
            Type = ModPackageType.MelonMod
        }, isEnabled: false);
        following.EffectiveUsage.Should().Be(ModUsage.Unrated);
        following.ApplyCatalogEnrichment(new CatalogMod { Id = "y", Name = "Y", Usage = "Casual" });
        following.EffectiveUsage.Should().Be(ModUsage.Casual);
    }

    [Theory]
    [InlineData(ModUsage.Tournament, "UsageTournament")]
    [InlineData(ModUsage.Casual, "UsageCasual")]
    [InlineData(ModUsage.Unrated, "UsageUnrated")]
    public void UiStrings_UsageLabel_is_localized(ModUsage usage, string key)
    {
        var ui = new UiStrings();
        ui.UsageLabel(usage).Should().Be(LocalizationService.T(key));
        ui.UsageLabel(usage).Should().NotBe(key);
    }

    [Theory]
    [InlineData("FilterUsage")]
    [InlineData("UsageFollowCatalog")]
    [InlineData("LogInvalidUsageOverride")]
    [InlineData("LogInvalidCatalogUsage")]
    public void New_resource_keys_resolve(string key)
    {
        LocalizationService.T(key).Should().NotBe(key).And.NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 2: 运行，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ModUsageDataTests"`
Expected: 编译失败，`UsageOverride` / `CatalogUsage` / `Usage` / `EffectiveUsage` / `UsageLabel` 不存在。

- [ ] **Step 3: 写资源键脚本**

新建 `tools/_tmp-usage-keys.ps1`，内容：

```powershell
$ErrorActionPreference = 'Stop'
$add = Join-Path $PSScriptRoot 'add-resource-key.ps1'

& $add -AnchorKey 'CategoryUncategorized' -NewKey 'UsageTournament' -Values @{
    'zh-CN' = "赛事专用 Mod"; 'en' = "Tournament mods"; 'de' = "Turnier-Mods"; 'ja' = "大会用 Mod"; 'ru' = "Турнирные моды" }
& $add -AnchorKey 'UsageTournament' -NewKey 'UsageCasual' -Values @{
    'zh-CN' = "日常化 Mod"; 'en' = "Casual mods"; 'de' = "Alltags-Mods"; 'ja' = "日常用 Mod"; 'ru' = "Повседневные моды" }
& $add -AnchorKey 'UsageCasual' -NewKey 'UsageUnrated' -Values @{
    'zh-CN' = "未判定"; 'en' = "Not rated"; 'de' = "Nicht bewertet"; 'ja' = "未判定"; 'ru' = "Не оценено" }
& $add -AnchorKey 'FilterCategory' -NewKey 'FilterUsage' -Values @{
    'zh-CN' = "用途"; 'en' = "Usage"; 'de' = "Verwendung"; 'ja' = "用途"; 'ru' = "Назначение" }
& $add -AnchorKey 'CategoryFollowCatalog' -NewKey 'UsageFollowCatalog' -Values @{
    'zh-CN' = "跟随目录"; 'en' = "Follow catalog"; 'de' = "Katalog folgen"; 'ja' = "カタログに従う"; 'ru' = "Как в каталоге" }
& $add -AnchorKey 'LogInvalidCategoryOverride' -NewKey 'LogInvalidUsageOverride' -Values @{
    'zh-CN' = "本地包 '{0}'：无效用途覆盖 '{1}'，按目录/未判定处理。"
    'en' = "Local package '{0}': invalid usage override '{1}'; using catalog / not rated."
    'de' = "Lokales Paket '{0}': ungültige Verwendungs-Überschreibung '{1}'; Katalog / nicht bewertet wird verwendet."
    'ja' = "ローカルパッケージ '{0}'：用途の上書き '{1}' が無効です。カタログ／未判定として扱います。"
    'ru' = "Локальный пакет '{0}': недопустимое переопределение назначения '{1}'; используется каталог / не оценено." }
& $add -AnchorKey 'LogInvalidCatalogCategory' -NewKey 'LogInvalidCatalogUsage' -Values @{
    'zh-CN' = "目录条目 '{0}'：无效用途 '{1}'，按未判定处理。"
    'en' = "Catalog entry '{0}': invalid usage '{1}'; treating as not rated."
    'de' = "Katalogeintrag '{0}': ungültige Verwendung '{1}'; als nicht bewertet behandelt."
    'ja' = "カタログ項目 '{0}'：無効な用途 '{1}'。未判定として処理。"
    'ru' = "Запись каталога '{0}': неверное назначение '{1}'; не оценено." }
```

- [ ] **Step 4: 转成带 BOM 的 UTF-8 后运行，再删除脚本**

Windows PowerShell 5.1 只有在文件带 BOM 时才按 UTF-8 读中文/日文/俄文。

```powershell
$f = 'tools/_tmp-usage-keys.ps1'
$t = Get-Content -Raw -Encoding UTF8 $f
[IO.File]::WriteAllText((Resolve-Path $f), $t, (New-Object Text.UTF8Encoding $true))
powershell -NoProfile -ExecutionPolicy Bypass -File $f
Remove-Item $f
git diff --stat -- src/MechabellumModManager/Resources
```

Expected: 七次运行各打印五行 `updated Strings*.resx`；`git diff --stat` 显示 5 个 resx 各 `7 insertions(+)`。如果某个文件的改动行数远大于 7（整文件换行符被改），停下来报告，不要继续。

再核对一处中文没有变成乱码：

```powershell
Select-String -Path src/MechabellumModManager/Resources/Strings.resx -Pattern 'name="Usage(Tournament|Casual|Unrated)"' -Encoding UTF8
```

Expected: 三行，值分别是 `赛事专用 Mod`、`日常化 Mod`、`未判定`。

- [ ] **Step 5: `UiStrings` 访问器**

`src/MechabellumModManager/ViewModels/UiStrings.cs`：

在 `public string FilterCategory => T("FilterCategory");` 下一行加：

```csharp
    public string FilterUsage => T("FilterUsage");
```

在 `public string CategoryFollowCatalog => T("CategoryFollowCatalog");` 下一行加：

```csharp
    public string UsageFollowCatalog => T("UsageFollowCatalog");
```

在 `CategoryLabel` 方法（以 `_ => T("CategoryUncategorized")` 和 `};` 结尾）之后加：

```csharp

    public string UsageLabel(Models.ModUsage usage) =>
        T(Services.ModTaxonomy.UsageResourceKey(usage));
```

- [ ] **Step 6: 数据字段**

`src/MechabellumModManager/Models/ModPackage.cs`：在 `public List<string>? ExtraTags { get; set; }` 之后加：

```csharp
    /// <summary>Local override of catalog usage (Tournament / Casual); null = follow catalog.</summary>
    public string? UsageOverride { get; set; }
```

在 `public List<string>? CatalogTags { get; set; }` 之后加：

```csharp
    /// <summary>Runtime-only catalog usage from last enrichment.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CatalogUsage { get; set; }
```

`src/MechabellumModManager/Services/ModCatalogService.cs`：在 `CatalogMod` 的 `public List<string>? Tags { get; set; }` 之后加：

```csharp

    /// <summary>Maintainer-set: Tournament or Casual. Missing = not rated.</summary>
    [JsonPropertyName("usage")]
    public string? Usage { get; set; }
```

`src/MechabellumModManager/Services/ModLibraryService.cs`：

- `SavePackageMeta` 里 `ExtraTags = pkg.ExtraTags is null ? null : ModTaxonomy.NormalizeTags(pkg.ExtraTags).ToList(),` 之后加一行 `UsageOverride = pkg.UsageOverride,`
- `TryLoadPackage` 里 `ExtraTags = meta.ExtraTags is null ? null : ModTaxonomy.NormalizeTags(meta.ExtraTags).ToList(),` 之后加一行 `UsageOverride = meta.UsageOverride,`
- `PackageMeta` 类里 `public List<string>? ExtraTags { get; set; }` 之后加一行 `public string? UsageOverride { get; set; }`

- [ ] **Step 7: 两个行 ViewModel 的 `EffectiveUsage`**

`src/MechabellumModManager/ViewModels/ModItemViewModel.cs`：

在 `public IReadOnlyList<string> EffectiveTags =>` 那个属性之后加：

```csharp

    public ModUsage EffectiveUsage =>
        ModTaxonomy.ResolveEffectiveUsage(Package.UsageOverride, Package.CatalogUsage);
```

在 `NotifyDetailChanged()` 里 `OnPropertyChanged(nameof(EffectiveTags));` 之后加：

```csharp
        OnPropertyChanged(nameof(EffectiveUsage));
```

在 `ApplyCatalogEnrichment` 里 `Package.CatalogTags = catalog.Tags is null ? null : new List<string>(catalog.Tags);` 之后加：

```csharp
        Package.CatalogUsage = catalog.Usage;
```

`src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs`：在 `public IReadOnlyList<string> EffectiveTags =>` 属性之后加：

```csharp

    public ModUsage EffectiveUsage => ModTaxonomy.ResolveEffectiveUsage(null, Mod.Usage);
```

- [ ] **Step 8: 非法值日志**

`src/MechabellumModManager/ViewModels/MainViewModel.cs`：

库加载循环里（`foreach (var pkg in library.OrderBy(...))`），在现有 `LogInvalidCategoryOverride` 那个 `if` 块之后、`Mods.Add(...)` 之前加：

```csharp
            if (!string.IsNullOrWhiteSpace(pkg.UsageOverride) &&
                !ModTaxonomy.TryParseUsage(pkg.UsageOverride, out _))
            {
                AppendLog(string.Format(LocalizationService.T("LogInvalidUsageOverride"), pkg.Id, pkg.UsageOverride));
            }
```

`LogInvalidCatalogCategory(CatalogMod mod)` 方法体内现有 `if` 块之后加：

```csharp
        if (!string.IsNullOrWhiteSpace(mod.Usage) &&
            !ModTaxonomy.TryParseUsage(mod.Usage, out _))
        {
            AppendLog(string.Format(LocalizationService.T("LogInvalidCatalogUsage"), mod.Id, mod.Usage));
        }
```

- [ ] **Step 9: 运行，确认通过**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~ModUsageDataTests|FullyQualifiedName~ResourceKeyParityTests|FullyQualifiedName~ResourceEncodingGuardTests|FullyQualifiedName~ModPackageTaxonomyPersistTests"`
Expected: 全部 PASS。

- [ ] **Step 10: Commit**

```powershell
git add -- src/MechabellumModManager/Resources/Strings.resx src/MechabellumModManager/Resources/Strings.en.resx src/MechabellumModManager/Resources/Strings.de.resx src/MechabellumModManager/Resources/Strings.ja.resx src/MechabellumModManager/Resources/Strings.ru.resx src/MechabellumModManager/ViewModels/UiStrings.cs src/MechabellumModManager/Models/ModPackage.cs src/MechabellumModManager/Services/ModCatalogService.cs src/MechabellumModManager/Services/ModLibraryService.cs src/MechabellumModManager/ViewModels/ModItemViewModel.cs src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs src/MechabellumModManager/ViewModels/MainViewModel.cs tests/MechabellumModManager.Tests/ModUsageDataTests.cs
git diff --cached --stat
git commit -m "feat: read catalog usage, persist usage overrides and add usage strings."
```

---

### Task 3: 两个列表 View 按用途分组，加「用途」筛选

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/FilterOptions.cs`（文件末尾）
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`（构造函数约 280 行；属性约 431 行；`[ObservableProperty]` 约 1198/1202 行；`partial void On...Changed` 约 1220 行；`ApplyCatalogRoot` 约 4266 行；`RebuildFilterOptionLabels` 约 5085 行；`ApplySort` 之前约 5222 行；`FilterCatalogItem` / `FilterLibraryItem` 约 5266 行）
- Test: `tests/MechabellumModManager.Tests/UsageGroupingViewTests.cs`（新建）

**Interfaces:**
- Consumes: Task 1 `ModListFilter.MatchesUsage`；Task 2 `EffectiveUsage`、`UiStrings.UsageLabel`、`CatalogMod.Usage`。
- Produces:
  - `sealed class UsageFilterOption(ModUsage? usage, string label)`，属性 `Usage : ModUsage?`、`Label : string`
  - `MainViewModel.UsageFilterOptions : IReadOnlyList<UsageFilterOption>`（两页共用）
  - `MainViewModel.SelectedCatalogUsageFilter : UsageFilterOption?`、`MainViewModel.SelectedLibraryUsageFilter : UsageFilterOption?`
  - `MainViewModel.ApplyCatalogRoot(CatalogRoot root)` 由 `private` 改为 `internal`（测试注入目录用）

- [ ] **Step 1: 写失败测试**

新建 `tests/MechabellumModManager.Tests/UsageGroupingViewTests.cs`：

```csharp
using System.ComponentModel;
using System.Windows.Data;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class UsageGroupingViewTests
{
    static CatalogRoot SampleCatalog() => new()
    {
        Mods =
        {
            new CatalogMod { Id = "u-a", Name = "A casual", File = "mods/u-a/A.dll", Usage = "Casual", UpdatedAt = "2026-10-01" },
            new CatalogMod { Id = "u-b", Name = "B tournament", File = "mods/u-b/B.dll", Usage = "Tournament", UpdatedAt = "2026-01-01" },
            new CatalogMod { Id = "u-c", Name = "C unrated", File = "mods/u-c/C.dll", UpdatedAt = "2026-05-01" },
            new CatalogMod { Id = "u-d", Name = "0 bogus", File = "mods/u-d/D.dll", Usage = "0" }
        }
    };

    /// <summary>One line per group, e.g. "Unrated: 0 bogus, C unrated".</summary>
    static List<string> Groups(ICollectionView view) =>
        view.Groups!.Cast<CollectionViewGroup>()
            .Select(g => $"{(ModUsage)g.Name}: " +
                string.Join(", ", g.Items.Cast<CatalogModItemViewModel>().Select(i => i.Name)))
            .ToList();

    [Fact]
    public void Both_views_group_by_usage_once()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        foreach (var view in new[] { vm.CatalogModsView, vm.LibraryModsView })
        {
            var group = view.GroupDescriptions.Should().ContainSingle().Subject
                .Should().BeOfType<PropertyGroupDescription>().Subject;
            group.PropertyName.Should().Be("EffectiveUsage");
            group.SortDescriptions.Should().Equal(new SortDescription("Name", ListSortDirection.Ascending));
        }
    }

    [Fact]
    public void Catalog_groups_stay_in_usage_order_under_name_sort()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        vm.ApplyCatalogRoot(SampleCatalog());

        Groups(vm.CatalogModsView).Should().Equal(
            "Tournament: B tournament",
            "Casual: A casual",
            "Unrated: 0 bogus, C unrated");
    }

    [Fact]
    public void Catalog_groups_stay_in_usage_order_under_updated_sort()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        vm.ApplyCatalogRoot(SampleCatalog());

        vm.SelectedCatalogSortMode = vm.SortModeOptions.Single(o => o.Mode == ModSortMode.UpdatedAtDesc);

        Groups(vm.CatalogModsView).Should().Equal(
            "Tournament: B tournament",
            "Casual: A casual",
            "Unrated: C unrated, 0 bogus");
    }

    [Fact]
    public void Catalog_groups_stay_in_usage_order_under_column_style_sort()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        vm.ApplyCatalogRoot(SampleCatalog());

        var lcv = (ListCollectionView)vm.CatalogModsView;
        lcv.CustomSort = null;
        lcv.SortDescriptions.Add(new SortDescription(nameof(CatalogModItemViewModel.Name), ListSortDirection.Descending));

        Groups(vm.CatalogModsView).Should().Equal(
            "Tournament: B tournament",
            "Casual: A casual",
            "Unrated: C unrated, 0 bogus");
    }

    [Fact]
    public void Usage_filter_options_list_all_then_each_usage()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.UsageFilterOptions.Select(o => o.Usage).Should().Equal(
            null, ModUsage.Tournament, ModUsage.Casual, ModUsage.Unrated);
        vm.UsageFilterOptions[0].Label.Should().Be(vm.Ui.FilterAll);
        vm.UsageFilterOptions[1].Label.Should().Be(vm.Ui.UsageLabel(ModUsage.Tournament));
        vm.SelectedCatalogUsageFilter.Should().BeSameAs(vm.UsageFilterOptions[0]);
        vm.SelectedLibraryUsageFilter.Should().BeSameAs(vm.UsageFilterOptions[0]);
    }

    [Fact]
    public void Catalog_usage_filter_keeps_only_that_group()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        vm.ApplyCatalogRoot(SampleCatalog());

        vm.SelectedCatalogUsageFilter = vm.UsageFilterOptions.Single(o => o.Usage == ModUsage.Casual);

        Groups(vm.CatalogModsView).Should().Equal("Casual: A casual");
    }

    [Fact]
    public void Library_usage_filter_hides_rows_of_other_usages()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        vm.Mods.Should().ContainSingle(m => m.Package.Id == "cam-aaaaaaaa");

        vm.SelectedLibraryUsageFilter = vm.UsageFilterOptions.Single(o => o.Usage == ModUsage.Tournament);
        vm.LibraryModsView.Cast<ModItemViewModel>().Should().BeEmpty();

        vm.SelectedLibraryUsageFilter = vm.UsageFilterOptions.Single(o => o.Usage == ModUsage.Unrated);
        vm.LibraryModsView.Cast<ModItemViewModel>().Should().ContainSingle(m => m.Package.Id == "cam-aaaaaaaa");
    }
}
```

- [ ] **Step 2: 运行，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UsageGroupingViewTests"`
Expected: 编译失败，`ApplyCatalogRoot` 不可访问，`UsageFilterOptions` / `SelectedCatalogUsageFilter` 不存在。

- [ ] **Step 3: `UsageFilterOption`**

`src/MechabellumModManager/ViewModels/FilterOptions.cs` 末尾加：

```csharp

public sealed class UsageFilterOption
{
    public UsageFilterOption(ModUsage? usage, string label)
    {
        Usage = usage;
        Label = label;
    }

    public ModUsage? Usage { get; }
    public string Label { get; }
    public override string ToString() => Label;
}
```

- [ ] **Step 4: 分组**

`src/MechabellumModManager/ViewModels/MainViewModel.cs` 构造函数，在

```csharp
        LibraryModsView.SortDescriptions.Add(
            new SortDescription(nameof(ModItemViewModel.DisplayName), ListSortDirection.Ascending));
```

之后加：

```csharp
        AddUsageGrouping(CatalogModsView);
        AddUsageGrouping(LibraryModsView);
```

在 `static void ApplySort(ICollectionView view, ModSortMode mode, bool catalog)` 之前加：

```csharp
    /// <summary>
    /// Groups sort themselves by the ModUsage value, so group order does not depend on
    /// CustomSort or on which column header the player clicked.
    /// </summary>
    static void AddUsageGrouping(ICollectionView view)
    {
        var byUsage = new PropertyGroupDescription(nameof(ModItemViewModel.EffectiveUsage));
        byUsage.SortDescriptions.Add(new SortDescription(nameof(CollectionViewGroup.Name), ListSortDirection.Ascending));
        view.GroupDescriptions.Add(byUsage);
    }
```

如果 `CollectionViewGroup` 报找不到类型，在文件顶部加 `using System.Windows.Data;`（`CollectionViewSource` 已经在用，通常已有）。

把 `void ApplyCatalogRoot(CatalogRoot root)` 改为 `internal void ApplyCatalogRoot(CatalogRoot root)`，方法体不变。

- [ ] **Step 5: 筛选状态**

在 `public IReadOnlyList<CategoryFilterOption> LibraryCategoryFilterOptions { get; private set; } = ...;` 之后加：

```csharp
    public IReadOnlyList<UsageFilterOption> UsageFilterOptions { get; private set; } = Array.Empty<UsageFilterOption>();
```

在 `[ObservableProperty] private CategoryFilterOption? _selectedCatalogCategoryFilter;` 之后加：

```csharp
    [ObservableProperty] private UsageFilterOption? _selectedCatalogUsageFilter;
```

在 `[ObservableProperty] private CategoryFilterOption? _selectedLibraryCategoryFilter;` 之后加：

```csharp
    [ObservableProperty] private UsageFilterOption? _selectedLibraryUsageFilter;
```

在 `partial void OnSelectedCatalogCategoryFilterChanged(...)` 方法之后加：

```csharp
    partial void OnSelectedCatalogUsageFilterChanged(UsageFilterOption? value)
    {
        if (!_suppressFilterRefresh) RefreshCatalogView();
    }
```

在 `partial void OnSelectedLibraryCategoryFilterChanged(...)` 方法之后加：

```csharp
    partial void OnSelectedLibraryUsageFilterChanged(UsageFilterOption? value)
    {
        if (!_suppressFilterRefresh) RefreshLibraryView();
    }
```

`RebuildFilterOptionLabels()` 的 `try` 块里，在 `OnPropertyChanged(nameof(LibraryCategoryFilterOptions));` 之后加：

```csharp

            var usageOptions = new List<UsageFilterOption> { new(null, allLabel) };
            foreach (var usage in Enum.GetValues<ModUsage>())
                usageOptions.Add(new UsageFilterOption(usage, Ui.UsageLabel(usage)));
            UsageFilterOptions = usageOptions;
            OnPropertyChanged(nameof(UsageFilterOptions));
            SelectedCatalogUsageFilter = UsageFilterOptions.FirstOrDefault(o =>
                o.Usage == SelectedCatalogUsageFilter?.Usage) ?? UsageFilterOptions[0];
            SelectedLibraryUsageFilter = UsageFilterOptions.FirstOrDefault(o =>
                o.Usage == SelectedLibraryUsageFilter?.Usage) ?? UsageFilterOptions[0];
```

（首次调用时 `SelectedXxxUsageFilter` 为 null，`o.Usage == null` 命中「全部」那一项。）

- [ ] **Step 6: 筛选判断**

`FilterCatalogItem` 里，在 `MatchesCategory(SelectedCatalogCategoryFilter?.Category, ...)` 那个 `if` 之前加：

```csharp
        if (!ModListFilter.MatchesUsage(SelectedCatalogUsageFilter?.Usage, item.EffectiveUsage))
            return false;
```

`FilterLibraryItem` 里，在 `MatchesCategory(SelectedLibraryCategoryFilter?.Category, ...)` 那个 `if` 之前加：

```csharp
        if (!ModListFilter.MatchesUsage(SelectedLibraryUsageFilter?.Usage, item.EffectiveUsage))
            return false;
```

- [ ] **Step 7: 运行，确认通过**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UsageGroupingViewTests|FullyQualifiedName~CatalogTagFilterUiTests|FullyQualifiedName~UiLanguageSwitchTests"`
Expected: 全部 PASS。

如果三个「组顺序」测试里有失败、且失败内容是组顺序不对（而不是编译或空引用），说明 `PropertyGroupDescription.SortDescriptions` 在当前运行时没有生效：**停下来报告**，不要改用别的排序方案。

- [ ] **Step 8: Commit**

```powershell
git add -- src/MechabellumModManager/ViewModels/FilterOptions.cs src/MechabellumModManager/ViewModels/MainViewModel.cs tests/MechabellumModManager.Tests/UsageGroupingViewTests.cs
git diff --cached --stat
git commit -m "feat: group both mod lists by usage and add a usage filter."
```

---

### Task 4: 界面：分组标题、虚拟化、「用途」下拉

**Files:**
- Create: `src/MechabellumModManager/Converters/ModUsageToLabelConverter.cs`
- Modify: `src/MechabellumModManager/Themes/AppTheme.xaml`（根元素 xmlns；`AccentBarStyle` 之后，约 153 行）
- Modify: `src/MechabellumModManager/MainWindow.xaml`（Mod 浏览筛选栏约 1234 行；`CatalogModsGrid` 约 1351 行；Mod 库工具栏约 1638 行；`LibraryModsGrid` 约 1717 行）
- Test: `tests/MechabellumModManager.Tests/UsageGroupingWindowTests.cs`（新建）

**Interfaces:**
- Consumes: Task 1 `ModTaxonomy.UsageResourceKey`；Task 2 `UiStrings.FilterUsage`；Task 3 `UsageFilterOptions`、`SelectedCatalogUsageFilter`、`SelectedLibraryUsageFilter`。
- Produces: 应用级资源 `ModUsageToLabel`（转换器）、`UsageGroupHeaderTemplate`（`DataTemplate`）。

- [ ] **Step 1: 写失败测试**

新建 `tests/MechabellumModManager.Tests/UsageGroupingWindowTests.cs`：

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using MechabellumModManager.Converters;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

[Collection(StaCollection.Name)]
public class UsageGroupingWindowTests
{
    readonly StaTestHost _host;

    public UsageGroupingWindowTests(StaTestHost host) => _host = host;

    [Theory]
    [InlineData("CatalogModsGrid")]
    [InlineData("LibraryModsGrid")]
    public void Mod_grid_has_usage_group_header_and_keeps_virtualizing(string gridName)
    {
        _host.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                var grid = (DataGrid)window.FindName(gridName);
                grid.GroupStyle.Should().ContainSingle();
                grid.GroupStyle[0].HeaderTemplate.Should().BeSameAs(
                    Application.Current.FindResource("UsageGroupHeaderTemplate"));
                VirtualizingPanel.GetIsVirtualizingWhenGrouping(grid).Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        }, TimeSpan.FromSeconds(60));
    }

    [Theory]
    [InlineData(ModUsage.Tournament)]
    [InlineData(ModUsage.Casual)]
    [InlineData(ModUsage.Unrated)]
    public void Converter_returns_localized_label(ModUsage usage)
    {
        var converter = new ModUsageToLabelConverter();
        converter.Convert(usage, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(LocalizationService.T(ModTaxonomy.UsageResourceKey(usage)));
    }

    [Fact]
    public void Converter_returns_empty_for_non_usage()
    {
        new ModUsageToLabelConverter().Convert("x", typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be("");
    }
}
```

- [ ] **Step 2: 运行，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UsageGroupingWindowTests"`
Expected: 编译失败，`ModUsageToLabelConverter` 不存在。

- [ ] **Step 3: 转换器**

新建 `src/MechabellumModManager/Converters/ModUsageToLabelConverter.cs`：

```csharp
using System.Globalization;
using System.Windows.Data;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Converters;

public sealed class ModUsageToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ModUsage usage ? LocalizationService.T(ModTaxonomy.UsageResourceKey(usage)) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
```

- [ ] **Step 4: 主题资源**

`src/MechabellumModManager/Themes/AppTheme.xaml` 根元素 `<ResourceDictionary ...>` 上，在 `xmlns:theme="clr-namespace:MechabellumModManager.Themes"` 之后加：

```xml
                    xmlns:conv="clr-namespace:MechabellumModManager.Converters"
```

在 `<Style x:Key="AccentBarStyle" TargetType="Border">…</Style>` 结束之后加（必须在 `AccentBarStyle` 和 `SectionLabelStyle` 之后，`StaticResource` 只能引用前面定义过的键）：

```xml

            <conv:ModUsageToLabelConverter x:Key="ModUsageToLabel" />

            <!-- DataContext is the CollectionViewGroup: Name is the ModUsage, ItemCount the rows left after filtering. -->
            <DataTemplate x:Key="UsageGroupHeaderTemplate">
                <StackPanel Orientation="Horizontal" Margin="4,12,0,6">
                    <Border Style="{StaticResource AccentBarStyle}"
                            Height="14"
                            Margin="0,0,8,0"
                            VerticalAlignment="Center" />
                    <TextBlock Text="{Binding Name, Converter={StaticResource ModUsageToLabel}}"
                               Style="{StaticResource SectionLabelStyle}"
                               Margin="0"
                               VerticalAlignment="Center" />
                    <TextBlock Text="{Binding ItemCount, StringFormat='({0})'}"
                               Margin="6,0,0,0"
                               VerticalAlignment="Center"
                               FontSize="13"
                               Foreground="{StaticResource TextMutedBrush}" />
                </StackPanel>
            </DataTemplate>
```

- [ ] **Step 5: 两张 DataGrid**

`src/MechabellumModManager/MainWindow.xaml`：

`<DataGrid x:Name="CatalogModsGrid"` 的属性列表里，在 `VirtualizingPanel.VirtualizationMode="Recycling"` 之后加一行：

```xml
                                  VirtualizingPanel.IsVirtualizingWhenGrouping="True"
```

同一个 DataGrid 的 `<DataGrid.Columns>` 之前加：

```xml
                            <DataGrid.GroupStyle>
                                <GroupStyle HeaderTemplate="{StaticResource UsageGroupHeaderTemplate}" />
                            </DataGrid.GroupStyle>
```

`<DataGrid x:Name="LibraryModsGrid"` 的属性列表里，在 `HeadersVisibility="Column"` 之前加一行：

```xml
                                  VirtualizingPanel.IsVirtualizingWhenGrouping="True"
```

同一个 DataGrid 的 `<DataGrid.Columns>` 之前加同样的 `<DataGrid.GroupStyle>` 块。

- [ ] **Step 6: 「用途」下拉**

Mod 浏览筛选栏：在含 `Text="{Binding Ui.FilterCategory}"` 的那个 `<StackPanel Orientation="Horizontal" Margin="0,0,8,0" VerticalAlignment="Center">` 之前插入：

```xml
                        <StackPanel Orientation="Horizontal" Margin="0,0,8,0" VerticalAlignment="Center">
                            <TextBlock Text="{Binding Ui.FilterUsage}"
                                       Style="{StaticResource FilterFieldLabelStyle}" />
                            <ComboBox Width="140" MinHeight="36"
                                      ToolTip="{Binding Ui.FilterUsage}"
                                      ItemsSource="{Binding UsageFilterOptions}"
                                      SelectedItem="{Binding SelectedCatalogUsageFilter}"
                                      ItemTemplate="{StaticResource FilterOptionLabelTemplate}" />
                        </StackPanel>
```

Mod 库工具栏：在 `<Button Content="{Binding Ui.EditModTaxonomy}"` 之前（即包住库搜索框的那个 `<Grid Width="220" …>` 结束的 `</Grid>` 之后）插入：

```xml
                                <StackPanel Orientation="Horizontal" Margin="0,0,8,0" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding Ui.FilterUsage}"
                                               Style="{StaticResource FilterFieldLabelStyle}" />
                                    <ComboBox Width="140" MinHeight="36"
                                              ToolTip="{Binding Ui.FilterUsage}"
                                              ItemsSource="{Binding UsageFilterOptions}"
                                              SelectedItem="{Binding SelectedLibraryUsageFilter}"
                                              ItemTemplate="{StaticResource FilterOptionLabelTemplate}" />
                                </StackPanel>
```

- [ ] **Step 7: 运行，确认通过**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UsageGroupingWindowTests|FullyQualifiedName~MainWindowSmokeTests"`
Expected: 全部 PASS。

- [ ] **Step 8: 手动看一眼**

Run: `dotnet run --project src/MechabellumModManager/MechabellumModManager.csproj`
检查：Mod 浏览刷新目录后，列表出现「未判定 (N)」标题（目录还没写 `usage` 时全部在这一组）；「用途」下拉有 全部 / 赛事专用 Mod / 日常化 Mod / 未判定；Mod 库同样有标题和下拉；点 Mod 库列头排序时标题不消失。关掉程序。把看到的情况写进任务报告；如果标题没显示，停下来报告。

- [ ] **Step 9: Commit**

```powershell
git add -- src/MechabellumModManager/Converters/ModUsageToLabelConverter.cs src/MechabellumModManager/Themes/AppTheme.xaml src/MechabellumModManager/MainWindow.xaml tests/MechabellumModManager.Tests/UsageGroupingWindowTests.cs
git diff --cached --stat
git commit -m "feat: show usage group headers and a usage filter on both mod pages."
```

---

### Task 5: 编辑对话框加「用途」，写回 `package.json`

**Files:**
- Modify: `src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml`（`CategoryCombo` 之后）
- Modify: `src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml.cs`
- Modify: `src/MechabellumModManager/App.xaml.cs`（`PromptEditTaxonomy`，约 447 行）
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs`（字段约 57 行、构造参数约 155 行、`EditLibraryModTaxonomy` 约 3118 行）
- Modify: `tests/MechabellumModManager.Tests/Support/MainViewModelFixture.cs`（`CreateVm` 参数与转发）
- Test: `tests/MechabellumModManager.Tests/UsageEditFlowTests.cs`（新建）

**Interfaces:**
- Consumes: Task 1 `ModTaxonomy.CatalogWritableUsages`、`TryParseUsage`；Task 2 `UiStrings.FilterUsage`、`UsageFollowCatalog`、`UsageLabel`、`ModPackage.UsageOverride`；Task 3 分组。
- Produces: 编辑回调类型改为 `Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)?>`，用于 `MainViewModel` 构造参数 `promptEditTaxonomy`、`App.PromptEditTaxonomy`、`EditModTaxonomyDialog.Result`、`MainViewModelFixture.CreateVm(promptEditTaxonomy:)`。

- [ ] **Step 1: 写失败测试**

新建 `tests/MechabellumModManager.Tests/UsageEditFlowTests.cs`：

```csharp
using System.Text.Json;
using System.Windows.Data;
using FluentAssertions;
using MechabellumModManager.Models;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class UsageEditFlowTests
{
    [Fact]
    public void Edit_command_saves_usage_override_and_moves_row_to_its_group()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(promptEditTaxonomy: _ => (null, Array.Empty<string>(), "Casual"));
        var cam = vm.Mods.Single(m => m.Package.Id == "cam-aaaaaaaa");
        cam.EffectiveUsage.Should().Be(ModUsage.Unrated);
        vm.SelectedLibraryMod = cam;

        vm.EditLibraryModTaxonomyCommand.Execute(null);

        cam.Package.UsageOverride.Should().Be("Casual");
        cam.EffectiveUsage.Should().Be(ModUsage.Casual);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(cam.Package.PackageDirectory, "package.json")));
        doc.RootElement.GetProperty("usageOverride").GetString().Should().Be("Casual");
        vm.LibraryModsView.Groups!.Cast<CollectionViewGroup>()
            .Single(g => g.Items.Contains(cam)).Name.Should().Be(ModUsage.Casual);
    }

    [Fact]
    public void Edit_command_follow_catalog_clears_usage_override()
    {
        using var fx = Fixture.CreateReady();
        var answer = "Tournament";
        var vm = fx.CreateVm(promptEditTaxonomy: _ => (null, Array.Empty<string>(), answer));
        var cam = vm.Mods.Single(m => m.Package.Id == "cam-aaaaaaaa");
        vm.SelectedLibraryMod = cam;
        vm.EditLibraryModTaxonomyCommand.Execute(null);
        cam.EffectiveUsage.Should().Be(ModUsage.Tournament);

        answer = null!;
        vm.EditLibraryModTaxonomyCommand.Execute(null);

        cam.Package.UsageOverride.Should().BeNull();
        cam.EffectiveUsage.Should().Be(ModUsage.Unrated);
    }
}
```

- [ ] **Step 2: 运行，确认失败**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UsageEditFlowTests"`
Expected: 编译失败，`CreateVm` 没有 `promptEditTaxonomy` 参数。

- [ ] **Step 3: 回调类型改成三元组**

`src/MechabellumModManager/ViewModels/MainViewModel.cs`：

第 57 行字段改为：

```csharp
    readonly Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)?>? _promptEditTaxonomy;
```

第 155 行构造参数改为：

```csharp
        Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)?>? promptEditTaxonomy = null,
```

`EditLibraryModTaxonomy()` 里 `mod.Package.ExtraTags = result.Value.ExtraTags.ToList();` 之后加：

```csharp
        mod.Package.UsageOverride = result.Value.UsageOverride;
```

（后面已有的 `PersistPackageMeta`、`NotifyDetailChanged`、`RefreshLibraryView` 会负责写盘、通知 `EffectiveUsage` 和重新分组。）

`src/MechabellumModManager/App.xaml.cs`：`PromptEditTaxonomy` 的签名改为：

```csharp
    static (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)? PromptEditTaxonomy(Window owner, ModPackage package)
```

方法体不变。

`tests/MechabellumModManager.Tests/Support/MainViewModelFixture.cs`：`CreateVm` 参数列表末尾，`Func<string, bool>? tryOpenExternal = null` 改为：

```csharp
        Func<string, bool>? tryOpenExternal = null,
        Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)?>? promptEditTaxonomy = null)
```

`new MainViewModel(...)` 调用里，`tryOpenExternal: tryOpenExternal);` 改为：

```csharp
            tryOpenExternal: tryOpenExternal,
            promptEditTaxonomy: promptEditTaxonomy);
```

- [ ] **Step 4: 对话框**

`src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml`：在 `<ComboBox x:Name="CategoryCombo" … />` 之后插入：

```xml
            <TextBlock x:Name="UsageFieldLabel"
                       Text="用途"
                       Margin="0,12,0,4"
                       FontSize="15"
                       Foreground="{StaticResource TextMutedBrush}" />
            <ComboBox x:Name="UsageCombo"
                      Height="36"
                      SelectedIndex="0" />
```

`src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml.cs`：

构造函数里 `CategoryCombo.SelectedIndex = selectedIndex;` 之后加：

```csharp

        UsageFieldLabel.Text = ui.FilterUsage;
        UsageCombo.Items.Add(new ComboBoxItem
        {
            Content = ui.UsageFollowCatalog,
            Tag = null
        });
        foreach (var usage in ModTaxonomy.CatalogWritableUsages)
        {
            UsageCombo.Items.Add(new ComboBoxItem
            {
                Content = ui.UsageLabel(usage),
                Tag = usage.ToString()
            });
        }
        UsageCombo.SelectedIndex = ModTaxonomy.TryParseUsage(package.UsageOverride, out var usageParsed)
            ? 1 + ModTaxonomy.CatalogWritableUsages.ToList().IndexOf(usageParsed)
            : 0;
```

`Result` 属性改为：

```csharp
    public (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)? Result { get; private set; }
```

`Ok_Click` 里 `Result = (categoryOverride, extras);` 改为：

```csharp
        string? usageOverride = null;
        if (UsageCombo.SelectedItem is ComboBoxItem usageItem && usageItem.Tag is string u)
            usageOverride = u;
        Result = (categoryOverride, extras, usageOverride);
```

- [ ] **Step 5: 运行，确认通过**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~UsageEditFlowTests|FullyQualifiedName~ModPackageTaxonomyPersistTests|FullyQualifiedName~MainWindowSmokeTests"`
Expected: 全部 PASS。

- [ ] **Step 6: 全量测试**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj`
Expected: 全部 PASS。若有失败，先在 `master` 上跑同一个测试确认它本来是否就失败；本来就失败的写进报告，不要顺手修。

- [ ] **Step 7: Commit**

```powershell
git add -- src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml src/MechabellumModManager/Dialogs/EditModTaxonomyDialog.xaml.cs src/MechabellumModManager/App.xaml.cs src/MechabellumModManager/ViewModels/MainViewModel.cs tests/MechabellumModManager.Tests/Support/MainViewModelFixture.cs tests/MechabellumModManager.Tests/UsageEditFlowTests.cs
git diff --cached --stat
git commit -m "feat: let players override a mod's usage from the edit dialog."
```

---

### Task 6: 社区目录：`usage` 校验、字段说明、首批标注

工作目录：`D:\gongzuo\钢铁指挥官\社区目录`（独立 git 仓库，`master`，工作区已有用户未提交的 `README.md` 改动和若干未跟踪文件）。

**Files:**
- Modify: `scripts/validate_catalog.py`（`ALLOWED_CATEGORIES` 之后；`check_min_manager_version` 之后；`main()` 里 tags 校验之后，约 462 行）
- Modify: `scripts/test_validate_catalog.py`（`test_min_manager_version_is_optional_and_strict` 之后）
- Modify: `catalog.json`
- Modify: `README.md`（catalog 字段表 `tags` 行之后，约 156 行）——只在满足 Step 7 条件时提交
- Create（临时，用完删除）: `scripts/_tmp_add_usage.py`

**Interfaces:**
- Produces: `validate_catalog.ALLOWED_USAGES = {"Tournament", "Casual"}`、`validate_catalog.check_usage(mod: dict, errors: list[str]) -> None`。

- [ ] **Step 1: 建分支**

```powershell
git switch -c feat/usage-field; git branch --show-current
```

Expected: `feat/usage-field`。

- [ ] **Step 2: 写失败测试**

`scripts/test_validate_catalog.py`：在 `test_min_manager_version_is_optional_and_strict` 方法之后（同一个类里）加：

```python
    def test_usage_is_optional_and_limited(self):
        errors: list[str] = []
        v.check_usage({"id": "a"}, errors)
        v.check_usage({"id": "a", "usage": "Tournament"}, errors)
        v.check_usage({"id": "a", "usage": "Casual"}, errors)
        self.assertEqual(errors, [])
        v.check_usage({"id": "a", "usage": "Unrated"}, errors)
        v.check_usage({"id": "a", "usage": "tournament"}, errors)
        v.check_usage({"id": "a", "usage": 1}, errors)
        self.assertEqual(len(errors), 3)
        self.assertTrue(all("invalid usage" in e for e in errors))
```

- [ ] **Step 3: 运行，确认失败**

Run（工作目录 `scripts`）：`python -m unittest test_validate_catalog -v`
Expected: `test_usage_is_optional_and_limited` ERROR：`module 'validate_catalog' has no attribute 'check_usage'`。

- [ ] **Step 4: 实现校验**

`scripts/validate_catalog.py`：

`ALLOWED_CATEGORIES = {…}` 结束之后加：

```python
# Maintainer-set. Tournament = no effect on match fairness; Casual = everyday play only.
ALLOWED_USAGES = {"Tournament", "Casual"}
```

`check_min_manager_version` 函数之后加：

```python


def check_usage(mod: dict, errors: list[str]) -> None:
    usage = mod.get("usage")
    if usage is None:
        return
    if not isinstance(usage, str) or usage.strip() not in ALLOWED_USAGES:
        errors.append(f"id={mod.get('id')!r}: invalid usage: {usage!r}")
```

`main()` 里，在

```python
            if not isinstance(tags, list) or any(not isinstance(t, str) for t in tags):
                errors.append(f"id={mod.get('id')!r}: tags must be a string array")
```

之后、`errors.extend(conflict_errors(...))` 之前加（与 `tags = mod.get("tags")` 同一缩进）：

```python

        check_usage(mod, errors)
```

- [ ] **Step 5: 运行，确认通过**

Run（工作目录 `scripts`）：`python -m unittest test_validate_catalog -v`
Expected: 全部 OK。

- [ ] **Step 6: 首批标注 `catalog.json`**

新建 `scripts/_tmp_add_usage.py`：

```python
import json
from pathlib import Path

TOURNAMENT = {
    "show-grid", "quick-camera", "quick-mark", "replay-tool", "replay-reset",
    "friend-overlay", "cut-guide", "leaderboard-fix", "auto-speed",
}
CASUAL = {"opponent-economy", "predict-position"}

path = Path(__file__).resolve().parent.parent / "catalog.json"
raw = path.read_text(encoding="utf-8")
newline = "\r\n" if "\r\n" in raw else "\n"
data = json.loads(raw)

seen = set()
for i, mod in enumerate(data["mods"]):
    usage = "Tournament" if mod["id"] in TOURNAMENT else "Casual" if mod["id"] in CASUAL else None
    if usage is None:
        continue
    seen.add(mod["id"])
    rebuilt = {}
    for key, value in mod.items():
        if key == "usage":
            continue
        rebuilt[key] = value
        if key == "tags":
            rebuilt["usage"] = usage
    rebuilt.setdefault("usage", usage)
    data["mods"][i] = rebuilt

missing = (TOURNAMENT | CASUAL) - seen
if missing:
    raise SystemExit(f"ids not in catalog: {sorted(missing)}")

data["updatedAt"] = "2026-10-13T12:00:00Z"
text = json.dumps(data, ensure_ascii=False, indent=4)
if raw.endswith("\n"):
    text += "\n"
with open(path, "w", encoding="utf-8", newline=newline) as f:
    f.write(text)
print(f"tagged {len(seen)} mods")
```

Run（仓库根目录）：

```powershell
python scripts/_tmp_add_usage.py; Remove-Item scripts/_tmp_add_usage.py; git diff --stat -- catalog.json; python scripts/validate_catalog.py
```

Expected：
- `tagged 11 mods`
- `catalog.json | 13 +++++++++++--`（11 行 `usage` + 1 行 `updatedAt` 插入，1 行旧 `updatedAt` 删除）
- `OK: 20 mods, ids unique, files present, sha256 and size verified`

如果 diff 行数明显多于 13（说明 `json.dumps` 改了原有格式），执行 `git checkout -- catalog.json` 还原，停下来报告。

- [ ] **Step 7: README 字段表**

在 catalog 字段表 `| \`tags\` | Optional string array | \`["grid","hud"]\` |` 这一行之后加：

```markdown
| `usage` | Optional, set by maintainers only. `Tournament` = no effect on match fairness, allowed in tournaments (赛事专用); `Casual` = affects fairness, everyday play only (日常化). Missing = not rated | `Tournament` / `Casual` |
```

先看 `git diff -- README.md`：除了这一行之外如果还有别的改动（用户原有的），**不要提交 README.md**，留在工作区，在任务报告里说明。

- [ ] **Step 8: Commit**

```powershell
git add -- scripts/validate_catalog.py scripts/test_validate_catalog.py catalog.json
git diff --cached --stat
git commit -m "catalog: add maintainer-set usage field and tag the first mods."
```

只有当 Step 7 确认 `README.md` 里只有本次那一行时，再单独提交：

```powershell
git add -- README.md
git commit -m "docs: describe the usage catalog field."
```

---

## 交付后（不在本计划内）

- 社区目录推送与镜像同步（`tools/sync-mirror.ps1`）、管理器版本号上调与发版，按 `docs/05-版本分析/releasing.md` 另行进行，需要单独确认。
- 9 个待定 Mod（quick-item、undo-plus、damage-rank、show-dps、sales-calculation、missile-direction、battle-suite、team-beacon、unit-ledger）由维护者判定后再写 `usage`。
