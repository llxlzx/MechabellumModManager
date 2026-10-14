# 简介改为功能与用法 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 玩家在管理器里打开一个 Mod，先看到「功能与用法」，不再先看到管理器版本要求；目录、投稿模板和创作标准同步改成功能与用法优先。

**Architecture:** 管理器新增一个只做显示的剥离函数 `SummaryDisplay.StripManagerFloorPrefix`，浏览项和本地库项的 `Summary` 在按语言取出原文之后再过一遍它；原文、`package.json`、目录缓存都不改。界面两处详情都加「功能与用法」标签，文案改五种语言的资源。目录仓库 MechabellumMods 用一段保留格式的文本替换脚本删掉前缀、补快速标记按键，并同步改投稿模板和创作标准。

**Tech Stack:** .NET 8 WPF、CommunityToolkit.Mvvm、xUnit + FluentAssertions；目录仓库用 Python 3。

**Working branch:** none (working tree only, no commits)

本仓库工作区里 `MainWindow.xaml`、五份 `Strings*.resx`、`MainViewModel.cs` 已经有大量和这次无关的未提交改动（强制下架、安装器预设）。这次的改动落在同几个文件里，提交会把两批混在一起，所以计划不安排提交。MechabellumMods 的改动同样只留在那份克隆的工作区里；推送和同步镜像另行决定。

## Global Constraints

- 规格：`docs/03-程序开发/历史规格/specs/2026-10-14-summary-usage-first-design.md`
- 不新增目录字段；不改 `minManagerVersion`、`sha256`、`size`、`parts`、`file`。
- 剥离只作用于显示和搜索文字；`CatalogLocaleResolver` 不剥；安装时写进 `package.json` 的 `summary` 仍是目录原文（`MainViewModel.cs` 里 `UpdatePackageMetadata(... summary: item.Mod.Summary ...)` 不动）。
- 只剥开头那一句，版本号恰好三段数字（`[0-9]+\.[0-9]+\.[0-9]+`），剥之前先去掉开头空白，句后空白一并去掉；ru 句尾半角 `.` 和全角 `。` 都算。
- 五种语言的开头句：
  - zh：`从管理器安装或更新需要 x.y.z 或更高版本。`
  - en：`Installing or updating through the manager requires x.y.z or newer.`
  - de：`Installation oder Aktualisierung über den Manager erfordert x.y.z oder neuer.`
  - ja：`マネージャーからのインストールと更新には x.y.z 以降が必要です。`
  - ru：`Установка и обновление через менеджер требуют версию x.y.z или новее.`
- 标签：`LabelSummary` 和 `DirectUploadSummary` 都是 zh `功能与用法`、en `How to use`、de `Bedienung`、ja `機能と使い方`、ru `Как пользоваться`。
- 投稿正文那一行固定为：`【功能与用法 / How to use】第一句写能做什么、怎么操作，不要写管理器版本或更新日志。First sentence: what it does and how to use it. No manager version or changelog.`
- `.resx` 保持 UTF-8，不得出现 U+FFFD（`ResourceEncodingGuardTests` 会查）；五份 `.resx` 键集合保持一致（`ResourceKeyParityTests` 会查）。
- 测试命令都在 `D:\gongzuo\钢铁指挥官mod管理器开发` 下用 PowerShell 跑。

---

### Task 0: 记下基线

工作区里有别的未提交改动，先确认改动前哪些测试本来就失败，免得后面把旧失败算到这次头上。

**Files:** 无

- [ ] **Step 1: 构建并跑全量测试**

Run:

```powershell
dotnet build "src\MechabellumModManager\MechabellumModManager.csproj" -c Debug
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --logger "console;verbosity=minimal"
```

Expected: 构建成功。把测试摘要行（通过、失败、跳过的个数）和失败的测试名原样记下来，Task 5 用来对比。构建失败就停下来报告，不要往下做。

---

### Task 1: 显示用剥离函数

**Files:**
- Create: `src/MechabellumModManager/Services/SummaryDisplay.cs`
- Test: `tests/MechabellumModManager.Tests/SummaryDisplayTests.cs`

**Interfaces:**
- Produces: `public static string? SummaryDisplay.StripManagerFloorPrefix(string? summary)`，命名空间 `MechabellumModManager.Services`。`null` 进 `null` 出；没有开头句时原样返回同一个字符串；整段只有开头句时返回 `""`。

- [ ] **Step 1: 写失败的测试**

`tests/MechabellumModManager.Tests/SummaryDisplayTests.cs`：

```csharp
using FluentAssertions;
using MechabellumModManager.Services;

public class SummaryDisplayTests
{
    [Theory]
    [InlineData("从管理器安装或更新需要 1.3.13 或更高版本。 按 G 开关永久地面格线；支持正式服与测试服。", "按 G 开关永久地面格线；支持正式服与测试服。")]
    [InlineData("Installing or updating through the manager requires 1.3.13 or newer. Press G to toggle a permanent ground grid.", "Press G to toggle a permanent ground grid.")]
    [InlineData("Installation oder Aktualisierung über den Manager erfordert 1.3.13 oder neuer. Mit G das Bodengitter umschalten.", "Mit G das Bodengitter umschalten.")]
    [InlineData("マネージャーからのインストールと更新には 1.3.13 以降が必要です。 G キーで切り替え。", "G キーで切り替え。")]
    [InlineData("Установка и обновление через менеджер требуют версию 1.3.13 или новее. Клавиша G включает сетку.", "Клавиша G включает сетку.")]
    [InlineData("Установка и обновление через менеджер требуют версию 1.3.13 или новее。 Клавиша G включает сетку.", "Клавиша G включает сетку.")]
    [InlineData("从管理器安装或更新需要 1.3.15 或更高版本。 悬停单位文字时展开科技说明。", "悬停单位文字时展开科技说明。")]
    [InlineData("  从管理器安装或更新需要 1.3.13 或更高版本。按 G 开关格线。", "按 G 开关格线。")]
    public void Strips_the_leading_manager_version_sentence(string input, string expected) =>
        SummaryDisplay.StripManagerFloorPrefix(input).Should().Be(expected);

    [Fact]
    public void Keeps_game_versions_in_the_rest_of_the_text() =>
        SummaryDisplay.StripManagerFloorPrefix(
                "从管理器安装或更新需要 1.3.13 或更高版本。 数据仅含 2.0.0.0.2305。正式服 2.0.0.0.2320 未登记。")
            .Should().Be("数据仅含 2.0.0.0.2305。正式服 2.0.0.0.2320 未登记。");

    [Fact]
    public void Leaves_the_sentence_when_it_is_not_at_the_start()
    {
        const string text = "按 G 开关格线。从管理器安装或更新需要 1.3.13 或更高版本。";
        SummaryDisplay.StripManagerFloorPrefix(text).Should().Be(text);
    }

    [Theory]
    [InlineData("独立好友叠加面板：关注 / 关注我的人。")]
    [InlineData("")]
    public void Leaves_text_without_the_sentence_unchanged(string text) =>
        SummaryDisplay.StripManagerFloorPrefix(text).Should().Be(text);

    [Fact]
    public void Null_stays_null() =>
        SummaryDisplay.StripManagerFloorPrefix(null).Should().BeNull();

    [Fact]
    public void Sentence_alone_becomes_empty() =>
        SummaryDisplay.StripManagerFloorPrefix("Installing or updating through the manager requires 1.3.13 or newer.")
            .Should().BeEmpty();
}
```

- [ ] **Step 2: 跑测试，确认失败**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SummaryDisplayTests"`

Expected: 编译失败，报 `SummaryDisplay` 不存在。

- [ ] **Step 3: 写实现**

`src/MechabellumModManager/Services/SummaryDisplay.cs`：

```csharp
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

/// <summary>
/// Catalog summaries published before 2026-10-14 open with a manager-version sentence. The browse
/// list already shows minManagerVersion, so that sentence is dropped from the displayed text only.
/// </summary>
public static class SummaryDisplay
{
    static readonly Regex ManagerFloorPrefix = new(
        @"^\s*(?:" +
        @"从管理器安装或更新需要 [0-9]+\.[0-9]+\.[0-9]+ 或更高版本。" +
        @"|Installing or updating through the manager requires [0-9]+\.[0-9]+\.[0-9]+ or newer\." +
        @"|Installation oder Aktualisierung über den Manager erfordert [0-9]+\.[0-9]+\.[0-9]+ oder neuer\." +
        @"|マネージャーからのインストールと更新には [0-9]+\.[0-9]+\.[0-9]+ 以降が必要です。" +
        @"|Установка и обновление через менеджер требуют версию [0-9]+\.[0-9]+\.[0-9]+ или новее[.。]" +
        @")\s*",
        RegexOptions.CultureInvariant);

    public static string? StripManagerFloorPrefix(string? summary)
    {
        if (summary is null)
            return null;
        var match = ManagerFloorPrefix.Match(summary);
        return match.Success ? summary[match.Length..] : summary;
    }
}
```

- [ ] **Step 4: 跑测试，确认通过**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SummaryDisplayTests"`

Expected: 全部 PASS（14 个用例）。

---

### Task 2: 浏览项和本地库项用剥过的简介

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs:50`
- Modify: `src/MechabellumModManager/ViewModels/ModItemViewModel.cs:51-54`
- Test: `tests/MechabellumModManager.Tests/SummaryDisplayWiringTests.cs`

**Interfaces:**
- Consumes: `SummaryDisplay.StripManagerFloorPrefix(string?)`（Task 1）
- Produces: `CatalogModItemViewModel.Summary` 和 `ModItemViewModel.Summary` 返回剥过的文字。`MainViewModel` 的 `FilterCatalogItem` / `FilterLibraryItem` 已经按 `item.Summary` 搜索，不用改。

- [ ] **Step 1: 写失败的测试**

`tests/MechabellumModManager.Tests/SummaryDisplayWiringTests.cs`：

```csharp
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class SummaryDisplayWiringTests
{
    const string ZhPrefixed = "从管理器安装或更新需要 1.3.13 或更高版本。 按 G 开关永久地面格线。";
    const string EnPrefixed = "Installing or updating through the manager requires 1.3.13 or newer. Press G to toggle the grid.";

    static CatalogMod Grid() => new()
    {
        Id = "show-grid",
        Name = "地面格线 MOD",
        File = "mods/show-grid/ShowGrid.dll",
        Summary = ZhPrefixed,
        MinManagerVersion = "1.3.14",
        Locales = new Dictionary<string, CatalogModLocale>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new CatalogModLocale { Name = "Ground Grid Mod", Summary = EnPrefixed }
        }
    };

    [Fact]
    public void Catalog_item_drops_the_version_sentence_but_keeps_the_floor_line()
    {
        LocalizationService.Apply("zh-CN");
        var item = new CatalogModItemViewModel(Grid(), CatalogEntryState.NotInstalled);

        item.Summary.Should().Be("按 G 开关永久地面格线。");
        item.ManagerFloorText.Should().NotBeNullOrWhiteSpace();
        item.Mod.Summary.Should().Be(ZhPrefixed);
    }

    [Fact]
    public void Catalog_item_drops_the_localized_version_sentence()
    {
        LocalizationService.Apply("en");
        try
        {
            new CatalogModItemViewModel(Grid(), CatalogEntryState.NotInstalled)
                .Summary.Should().Be("Press G to toggle the grid.");
        }
        finally
        {
            LocalizationService.Apply("zh-CN");
        }
    }

    [Fact]
    public void Library_item_without_catalog_locales_drops_the_cached_chinese_sentence()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        var item = new ModItemViewModel(vm, new ModPackage
        {
            Id = "show-grid",
            DisplayName = "ShowGrid",
            Type = ModPackageType.MelonMod,
            Summary = ZhPrefixed
        }, isEnabled: false);

        item.Summary.Should().Be("按 G 开关永久地面格线。");
        item.Package.Summary.Should().Be(ZhPrefixed);
    }

    [Fact]
    public void Library_item_with_catalog_locales_drops_the_localized_sentence()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        var item = new ModItemViewModel(vm, new ModPackage
        {
            Id = "show-grid",
            DisplayName = "ShowGrid",
            Type = ModPackageType.MelonMod
        }, isEnabled: false);
        item.ApplyCatalogEnrichment(Grid());

        LocalizationService.Apply("en");
        try
        {
            item.Summary.Should().Be("Press G to toggle the grid.");
        }
        finally
        {
            LocalizationService.Apply("zh-CN");
        }
    }

    [Fact]
    public void Catalog_search_matches_the_displayed_text_not_the_version_sentence()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();
        LocalizationService.Apply("zh-CN");
        vm.ApplyCatalogRoot(new CatalogRoot { Mods = { Grid() } });

        vm.CatalogSearchText = "1.3.13";
        vm.CatalogModsView.Cast<CatalogModItemViewModel>().Should().BeEmpty();

        vm.CatalogSearchText = "永久";
        vm.CatalogModsView.Cast<CatalogModItemViewModel>().Select(i => i.Id).Should().Equal("show-grid");
    }
}
```

- [ ] **Step 2: 跑测试，确认失败**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SummaryDisplayWiringTests"`

Expected: 五个测试全部 FAIL：前四个的 `Summary` 仍以「从管理器安装或更新需要」或「Installing or updating」开头，第五个搜「1.3.13」仍命中 `show-grid`。

- [ ] **Step 3: 改两个视图模型**

`src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs` 第 50 行，把

```csharp
    public string? Summary => CatalogLocaleResolver.ResolveSummary(Mod);
```

改成

```csharp
    public string? Summary => SummaryDisplay.StripManagerFloorPrefix(CatalogLocaleResolver.ResolveSummary(Mod));
```

`src/MechabellumModManager/ViewModels/ModItemViewModel.cs` 第 51–54 行，把

```csharp
    public string? Summary =>
        Package.CatalogLocales is not null
            ? CatalogLocaleResolver.ResolveSummary(Package.Summary, Package.CatalogLocales)
            : Package.Summary;
```

改成

```csharp
    public string? Summary => SummaryDisplay.StripManagerFloorPrefix(
        Package.CatalogLocales is not null
            ? CatalogLocaleResolver.ResolveSummary(Package.Summary, Package.CatalogLocales)
            : Package.Summary);
```

两个文件都已经 `using MechabellumModManager.Services;`，不用加 using。

- [ ] **Step 4: 跑测试，确认通过**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SummaryDisplayWiringTests|FullyQualifiedName~CatalogLocaleResolverTests|FullyQualifiedName~ModUsageDataTests"`

Expected: 全部 PASS。`CatalogLocaleResolverTests` 不变，说明语言解析本身没被剥。

---

### Task 3: 「功能与用法」标签、详情显示和创作标准文案

**Files:**
- Modify: `src/MechabellumModManager/MainWindow.xaml:1520-1524`（浏览详情简介）
- Modify: `src/MechabellumModManager/MainWindow.xaml:2022-2025`（本地库详情简介）
- Modify: `src/MechabellumModManager/Resources/Strings.resx`、`Strings.en.resx`、`Strings.de.resx`、`Strings.ja.resx`、`Strings.ru.resx` 里的 `LabelSummary`、`DirectUploadSummary`、`AuthorCatalogBody`
- Test: `tests/MechabellumModManager.Tests/SummaryCopyResourceTests.cs`
- Test: `tests/MechabellumModManager.Tests/SummaryDetailXamlTests.cs`

**Interfaces:**
- Consumes: `CatalogModItemViewModel.Summary`、`ModItemViewModel.Summary`（Task 2）；`UiStrings.LabelSummary`（已存在，第 247 行）
- Produces: 无新 API。

- [ ] **Step 1: 写失败的测试**

`tests/MechabellumModManager.Tests/SummaryCopyResourceTests.cs`：

```csharp
using System.Xml.Linq;
using FluentAssertions;

public class SummaryCopyResourceTests
{
    [Theory]
    [InlineData("Strings.resx", "功能与用法")]
    [InlineData("Strings.en.resx", "How to use")]
    [InlineData("Strings.de.resx", "Bedienung")]
    [InlineData("Strings.ja.resx", "機能と使い方")]
    [InlineData("Strings.ru.resx", "Как пользоваться")]
    public void Summary_labels_say_how_to_use(string file, string expected)
    {
        var values = ReadValues(file);
        values["LabelSummary"].Should().Be(expected);
        values["DirectUploadSummary"].Should().Be(expected);
    }

    [Theory]
    [InlineData("Strings.resx", "第一句", "更新日志")]
    [InlineData("Strings.en.resx", "first sentence", "changelog")]
    [InlineData("Strings.de.resx", "erste Satz", "Änderungsprotokoll")]
    [InlineData("Strings.ja.resx", "最初の文", "更新履歴")]
    [InlineData("Strings.ru.resx", "Первое предложение", "список изменений")]
    public void Author_standard_puts_usage_first(string file, string usageFirst, string noChangelog)
    {
        var body = ReadValues(file)["AuthorCatalogBody"];
        body.Should().Contain(usageFirst);
        body.Should().Contain(noChangelog);
        body.Should().Contain("minManagerVersion");
        body.Should().NotContain("1.3.13");
    }

    static Dictionary<string, string> ReadValues(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue(because: "the repo root must be reachable from the test binary");
        var doc = XDocument.Load(Path.Combine(dir!, "src", "MechabellumModManager", "Resources", file));
        return doc.Root!
            .Elements("data")
            .Where(e => e.Attribute("name") is not null && e.Attribute("type") is null)
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? "", StringComparer.Ordinal);
    }
}
```

`tests/MechabellumModManager.Tests/SummaryDetailXamlTests.cs`：

```csharp
using FluentAssertions;

public class SummaryDetailXamlTests
{
    [Theory]
    [InlineData("SelectedCatalogMod")]
    [InlineData("SelectedLibraryMod")]
    public void Detail_summary_is_labelled_and_hidden_when_empty(string selected)
    {
        var xaml = File.ReadAllText(MainWindowPath());
        var visibility = $"Visibility=\"{{Binding {selected}.Summary, Converter={{StaticResource NonEmptyVis}}}}\"";
        var start = xaml.IndexOf(visibility, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, because: $"the {selected} summary line must hide when the text is empty");

        var end = xaml.IndexOf("</TextBlock>", start, StringComparison.Ordinal);
        var block = xaml[start..end];
        block.Should().Contain("<Run Text=\"{Binding Ui.LabelSummary, Mode=OneWay}\" />");
        block.Should().Contain($"<Run Text=\"{{Binding {selected}.Summary, Mode=OneWay}}\" />");
    }

    static string MainWindowPath()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        Directory.Exists(dir).Should().BeTrue(because: "the repo root must be reachable from the test binary");
        return Path.Combine(dir!, "src", "MechabellumModManager", "MainWindow.xaml");
    }
}
```

- [ ] **Step 2: 跑测试，确认失败**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SummaryCopyResourceTests|FullyQualifiedName~SummaryDetailXamlTests"`

Expected: 全部 FAIL。标签仍是「功能」「Summary」等；创作标准里还有 `1.3.13`；两处详情都找不到 `Visibility` 那一行。

- [ ] **Step 3: 改浏览详情**

`src/MechabellumModManager/MainWindow.xaml`，把

```xml
                                        <TextBlock Margin="0,8,0,0"
                                                   Text="{Binding SelectedCatalogMod.Summary}"
                                                   Foreground="{StaticResource TextPrimaryBrush}"
                                                   TextWrapping="Wrap"
                                                   FontSize="13" />
```

换成

```xml
                                        <TextBlock Margin="0,8,0,0"
                                                   Foreground="{StaticResource TextPrimaryBrush}"
                                                   TextWrapping="Wrap"
                                                   FontSize="13"
                                                   Visibility="{Binding SelectedCatalogMod.Summary, Converter={StaticResource NonEmptyVis}}">
                                            <Run Text="{Binding Ui.LabelSummary, Mode=OneWay}" />
                                            <Run Text="：" />
                                            <Run Text="{Binding SelectedCatalogMod.Summary, Mode=OneWay}" />
                                        </TextBlock>
```

- [ ] **Step 4: 改本地库详情**

同一文件，把

```xml
                                                    <TextBlock Margin="0,6,0,0"
                                                               Foreground="{StaticResource TextPrimaryBrush}"
                                                               FontSize="13"
                                                               TextWrapping="Wrap">
                                                        <Run Text="{Binding Ui.LabelSummary, Mode=OneWay}" />
```

换成

```xml
                                                    <TextBlock Margin="0,6,0,0"
                                                               Foreground="{StaticResource TextPrimaryBrush}"
                                                               FontSize="13"
                                                               TextWrapping="Wrap"
                                                               Visibility="{Binding SelectedLibraryMod.Summary, Converter={StaticResource NonEmptyVis}}">
                                                        <Run Text="{Binding Ui.LabelSummary, Mode=OneWay}" />
```

- [ ] **Step 5: 改五份资源文件**

每份文件改三处。用精确替换，不要整份重写，免得碰到工作区里别人的未提交改动。

`Strings.resx`：

- `<data name="LabelSummary" xml:space="preserve"><value>功能</value></data>` → `<data name="LabelSummary" xml:space="preserve"><value>功能与用法</value></data>`
- `<data name="DirectUploadSummary" xml:space="preserve">` 下一行 `<value>简介</value>` → `<value>功能与用法</value>`
- `AuthorCatalogBody` 的 `<value>` 整段换成：`简介第一句写玩家能做什么、用哪个键或怎么操作，可以补正式服还是测试服。不要写管理器版本号、更新日志或实现细节。版本要求写在 minManagerVersion。新 Mod 用 1.3.14，除非确实依赖更新的管理器行为。安装位置默认 MelonMod。按 MelonLoader 0.7.3 来写。`

`Strings.en.resx`：

- `LabelSummary`：`Summary` → `How to use`
- `DirectUploadSummary`：`Summary` → `How to use`
- `AuthorCatalogBody`：`The first sentence says what the player can do and which key or action to use. The live server, the test server, or both may follow. Do not put a manager version, a changelog, or implementation notes in the summary. The version requirement belongs in minManagerVersion. A new mod uses 1.3.14 unless it truly needs a newer manager behavior. The install location defaults to MelonMod. Write against MelonLoader 0.7.3.`

`Strings.de.resx`：

- `LabelSummary`：`Beschreibung` → `Bedienung`
- `DirectUploadSummary`：`Summary` → `Bedienung`
- `AuthorCatalogBody`：`Der erste Satz sagt, was der Spieler tun kann und welche Taste oder Aktion das ist. Live-Server, Testserver oder beide dürfen danach stehen. Keine Manager-Version, kein Änderungsprotokoll und keine Implementierungsnotizen in die Kurzbeschreibung. Die Versionsanforderung steht in minManagerVersion. Ein neuer Mod nutzt 1.3.14, außer er braucht wirklich ein neueres Manager-Verhalten. Der Installationsort ist standardmäßig MelonMod. Schreibe gegen MelonLoader 0.7.3.`

`Strings.ja.resx`：

- `LabelSummary`：`機能` → `機能と使い方`
- `DirectUploadSummary`：`Summary` → `機能と使い方`
- `AuthorCatalogBody`：`概要の最初の文には、プレイヤーができることと、使うキーまたは操作を書きます。正式サーバーかテストサーバーか両方かは続けて書いてかまいません。マネージャーの版番号、更新履歴、実装の詳細は書かないでください。版の要件は minManagerVersion に書きます。新しい Mod は 1.3.14 です。より新しいマネージャーの動作に本当に依存するときだけ上げます。インストール先は既定で MelonMod です。MelonLoader 0.7.3 向けに書いてください。`

`Strings.ru.resx`：

- `LabelSummary`：`Описание` → `Как пользоваться`
- `DirectUploadSummary`：`Summary` → `Как пользоваться`
- `AuthorCatalogBody`：`Первое предложение — что может игрок и какая клавиша или действие. Дальше можно указать официальный сервер, тестовый или оба. Номер версии менеджера, список изменений и заметки о реализации в описание не пишите. Требование к версии — в minManagerVersion. Новый мод ставит 1.3.14 и повышает её, только если ему правда нужно более новое поведение менеджера. Место установки по умолчанию — MelonMod. Пишите под MelonLoader 0.7.3.`

en、de、ja、ru 的 `DirectUploadSummary` 都写成多行：

```xml
  <data name="DirectUploadSummary" xml:space="preserve">
    <value>Summary</value>
  </data>
```

替换时连 `<data name="DirectUploadSummary"` 那一行一起匹配；`<value>Summary</value>` 在文件里可能不止一处。

- [ ] **Step 6: 跑测试，确认通过**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~SummaryCopyResourceTests|FullyQualifiedName~SummaryDetailXamlTests|FullyQualifiedName~ResourceKeyParityTests|FullyQualifiedName~ResourceEncodingGuardTests|FullyQualifiedName~MainWindowSmokeTests"`

Expected: 全部 PASS。`MainWindowSmokeTests` 通过说明改过的 XAML 能加载。

---

### Task 4: 投稿邮件正文

**Files:**
- Modify: `src/MechabellumModManager/Services/GitHubCommunityLinks.cs:222`
- Test: `tests/MechabellumModManager.Tests/GitHubCommunityLinksTests.cs`

**Interfaces:**
- Produces: `GitHubCommunityLinks.BuildSubmitBody(string?)` 的签名不变，只改其中一行。

- [ ] **Step 1: 写失败的测试**

在 `tests/MechabellumModManager.Tests/GitHubCommunityLinksTests.cs` 里，`BuildSubmitBody_includes_category_and_tags_lines` 之后加：

```csharp
    [Fact]
    public void BuildSubmitBody_asks_for_usage_first()
    {
        var body = GitHubCommunityLinks.BuildSubmitBody("Demo");
        body.Should().Contain(
            "【功能与用法 / How to use】第一句写能做什么、怎么操作，不要写管理器版本或更新日志。" +
            "First sentence: what it does and how to use it. No manager version or changelog.\n");
        body.Should().NotContain("一句话简介");
    }
```

- [ ] **Step 2: 跑测试，确认失败**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~GitHubCommunityLinksTests"`

Expected: `BuildSubmitBody_asks_for_usage_first` FAIL，其余 PASS。

- [ ] **Step 3: 改正文**

`src/MechabellumModManager/Services/GitHubCommunityLinks.cs`，把

```csharp
            "【一句话简介 / Summary】\n" +
```

换成

```csharp
            "【功能与用法 / How to use】第一句写能做什么、怎么操作，不要写管理器版本或更新日志。" +
            "First sentence: what it does and how to use it. No manager version or changelog.\n" +
```

- [ ] **Step 4: 跑测试，确认通过**

Run: `dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --filter "FullyQualifiedName~GitHubCommunityLinksTests|FullyQualifiedName~MailComposeFlowTests"`

Expected: 全部 PASS。

---

### Task 5: 管理器全量验证

**Files:** 无

- [ ] **Step 1: 构建并跑全量测试**

Run:

```powershell
dotnet build "src\MechabellumModManager\MechabellumModManager.csproj" -c Debug
dotnet test "tests\MechabellumModManager.Tests\MechabellumModManager.Tests.csproj" --logger "console;verbosity=minimal"
```

Expected: 构建成功。失败的测试集合和 Task 0 记下的完全相同（或更少）；通过数比 Task 0 多出这次新增的用例。出现 Task 0 没有的失败，就停下来查，不要往下做。

- [ ] **Step 2: 确认改动范围**

Run: `git diff --stat -- src/MechabellumModManager/Services/GitHubCommunityLinks.cs src/MechabellumModManager/ViewModels/CatalogModItemViewModel.cs src/MechabellumModManager/ViewModels/ModItemViewModel.cs tests/MechabellumModManager.Tests/GitHubCommunityLinksTests.cs`

Expected: 只有这四个文件，各自只改了上面写的几行。`MainWindow.xaml`、`Strings*.resx` 里原来就有别的未提交改动，`git diff` 会混在一起显示；看这几处时只核对本计划改的那几行，别的改动不要动。

---

### Task 6: 目录仓库 MechabellumMods

本仓库里没有这个仓库的克隆。这一节在 MechabellumMods 的工作区里改，不提交、不推送、不同步镜像；那几步等用户另行决定。

**Files:**（都在 MechabellumMods 里）
- Modify: `catalog.json`
- Modify: `docs/submit.html`
- Modify: `README.md`
- Modify: `docs/ai-mod-standard.md`

**Interfaces:** 无。管理器不读这几个文件的新结构；`catalog.json` 只是改了 `summary` 的文字和根上的 `updatedAt`。

- [ ] **Step 1: 拿到克隆**

Run:

```powershell
git clone https://github.com/llxlzx/MechabellumMods "D:\gongzuo\MechabellumMods"
cd "D:\gongzuo\MechabellumMods"
git status --short
python scripts/validate_catalog.py
```

Expected: 克隆成功，工作区干净，校验输出 `OK: 22 mods, ids unique, files present, sha256 and size verified`。校验失败或条目数不是 22，就停下来报告；那说明线上目录和这份计划核对时不一样了。

- [ ] **Step 2: 删前缀、补快速标记按键、前进根 `updatedAt`**

在 `D:\gongzuo\MechabellumMods` 下新建临时脚本 `scripts/_tmp_summary_usage_first.py`：

```python
import datetime
import re
from pathlib import Path

path = Path(__file__).resolve().parent.parent / "catalog.json"
text = path.read_bytes().decode("utf-8")

V = r"[0-9]+\.[0-9]+\.[0-9]+"
PREFIX = re.compile(
    r'("summary":\s*")(?:'
    r"从管理器安装或更新需要 " + V + r" 或更高版本。"
    r"|Installing or updating through the manager requires " + V + r" or newer\."
    r"|Installation oder Aktualisierung über den Manager erfordert " + V + r" oder neuer\."
    r"|マネージャーからのインストールと更新には " + V + r" 以降が必要です。"
    r"|Установка и обновление через менеджер требуют версию " + V + r" или новее[.。]"
    r")\s*"
)
text, stripped = PREFIX.subn(r"\1", text)
assert stripped == 80, f"expected 80 prefixed summaries (16 mods x 5 languages), got {stripped}"

QUICK_MARK = {
    "在对局里给地面格子做标记，对局结束时清除。格子尺寸或网格轴对不上时不会乱点。":
        "CTRL+左键标记地面，ALT+左键擦除地面，ALT+右键清除所有标记。在对局里给地面格子做标记，对局结束时清除。格子尺寸或网格轴对不上时不会乱点。",
    "Marks ground cells during a match and clears them when the match ends. It will not pick a cell when the grid size or native axes do not match.":
        "Ctrl+left click marks the ground, Alt+left click erases a mark, Alt+right click clears all marks. Marks ground cells during a match and clears them when the match ends. It will not pick a cell when the grid size or native axes do not match.",
    "Markiert Bodenzellen während eines Matches und entfernt sie am Matchende. Bei abweichender Gittergröße oder nativen Achsen wird keine Zelle gewählt.":
        "Strg+Linksklick markiert den Boden, Alt+Linksklick löscht eine Markierung, Alt+Rechtsklick löscht alle Markierungen. Markiert Bodenzellen während eines Matches und entfernt sie am Matchende. Bei abweichender Gittergröße oder nativen Achsen wird keine Zelle gewählt.",
    "対局中に地面マスへマークし、対局終了時に消します。マス寸法やネイティブ軸が合わないときは選択しません。":
        "Ctrl+左クリックで地面をマーク、Alt+左クリックで消去、Alt+右クリックで全部消去。対局中に地面マスへマークし、対局終了時に消します。マス寸法やネイティブ軸が合わないときは選択しません。",
    "Ставит метки на клетки поля во время матча и снимает их в конце. Если размер сетки или оси не совпадают, клетка не выбирается.":
        "Ctrl+левая кнопка ставит метку на землю, Alt+левая стирает метку, Alt+правая снимает все метки. Ставит метки на клетки поля во время матча и снимает их в конце. Если размер сетки или оси не совпадают, клетка не выбирается.",
}
for old, new in QUICK_MARK.items():
    pattern = re.compile(r'("summary":\s*")' + re.escape(old) + '"')
    text, n = pattern.subn(lambda m: m.group(1) + new + '"', text)
    assert n == 1, f"quick-mark summary not found exactly once: {old[:20]}"

now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
text, n = re.subn(
    r'("updatedAt":\s*")[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]+Z(")',
    lambda m: m.group(1) + now + m.group(2),
    text,
    count=1,
)
assert n == 1, "root updatedAt not found"

path.write_bytes(text.encode("utf-8"))
print(f"stripped {stripped}, quick-mark 5, updatedAt {now}")
```

Run:

```powershell
python scripts/_tmp_summary_usage_first.py
Remove-Item scripts/_tmp_summary_usage_first.py
git diff --stat -- catalog.json
```

Expected: 打印 `stripped 80, quick-mark 5, updatedAt <当前时间>`。`git diff --stat` 显示 `catalog.json | 162 +++++-----`，也就是 81 行改动：80 行简介加根上的 `updatedAt`。快速标记那五行本来就在这 80 行里。行数明显不是 81（说明格式被改了），就 `git checkout -- catalog.json` 还原，停下来报告。

- [ ] **Step 3: 核对结果**

Run:

```powershell
$env:PYTHONIOENCODING = "utf-8"
python -c "import json,re;d=json.load(open('catalog.json',encoding='utf-8'));P=re.compile(r'^\s*(从管理器安装或更新需要|Installing or updating through the manager|Installation oder Aktualisierung über den Manager|マネージャーからのインストールと更新には|Установка и обновление через менеджер)');s=[(m['id'],k,v) for m in d['mods'] for k,v in [('zh',m.get('summary'))]+[(l,(o or {}).get('summary')) for l,o in (m.get('locales') or {}).items()] if isinstance(v,str)];print('left',[x[:2] for x in s if P.match(x[2])]);q=next(m for m in d['mods'] if m['id']=='quick-mark');print(q['summary']);print(q['locales']['en']['summary'][:40]);print(sorted({m['id'] for m in d['mods'] if not m.get('minManagerVersion')}))"
python scripts/validate_catalog.py
```

Expected: 第一行 `left []`；第二行是 `CTRL+左键标记地面，ALT+左键擦除地面，ALT+右键清除所有标记。在对局里…`；第三行以 `Ctrl+left click marks the ground` 开头；第四行 `[]`（每条都有 `minManagerVersion`）。校验输出 `OK: 22 mods, ...`。

- [ ] **Step 4: 投稿模板和字段说明**

`docs/submit.html` 和 `README.md` 各有一行，原文完全相同：

```
【一句话简介 / Summary】
```

两处都换成：

```
【功能与用法 / How to use】第一句写能做什么、怎么操作，不要写管理器版本或更新日志。First sentence: what it does and how to use it. No manager version or changelog.
```

`README.md` 字段表里这一行：

```
| `summary` | Short description | `Toggle grid with G` |
```

换成：

```
| `summary` | What it does and how to use it (first sentence). No manager version or changelog | `Toggle grid with G` |
```

- [ ] **Step 5: 创作标准**

`docs/ai-mod-standard.md`「先问完再写」一节，把这一条：

```
- **简介。** `summary` 只写三件事：玩家能做什么、用哪个键、正式服 / 测试服 / 两者。目录没有单独的游戏端字段。不超过 400 字。不要写管理器版本。
```

换成：

```
- **简介。** `summary` 第一句写玩家能做什么、用哪个键或怎么操作，后面可以补正式服 / 测试服 / 两者。目录没有单独的游戏端字段。不超过 400 字。不要写管理器版本、更新日志或实现细节；更新内容写进投稿邮件的「更新说明」。
```

- [ ] **Step 6: 确认目录仓库的改动范围**

Run:

```powershell
git diff --stat
Select-String -Path docs/submit.html, README.md -Pattern "一句话简介"
```

Expected: `git diff --stat` 只列出 `catalog.json`、`docs/submit.html`、`README.md`、`docs/ai-mod-standard.md` 四个文件；`Select-String` 没有输出。改动留在工作区，不提交、不推送。
