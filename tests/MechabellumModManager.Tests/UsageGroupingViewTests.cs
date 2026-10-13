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
