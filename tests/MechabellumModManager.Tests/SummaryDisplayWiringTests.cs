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
