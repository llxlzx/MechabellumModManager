using FluentAssertions;
using MechabellumModManager.Services;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class CatalogTagFilterUiTests
{
    [Fact]
    public void Empty_catalog_selects_all_tag_filter()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.CatalogMods.Should().BeEmpty();
        vm.SelectedCatalogTagFilter.Should().NotBeNull();
        vm.SelectedCatalogTagFilter!.Tag.Should().BeNull();
        vm.SelectedCatalogTagFilter.Label.Should().Be(vm.Ui.FilterAll);
        vm.SelectedLibraryTagFilter.Should().NotBeNull();
        vm.SelectedLibraryTagFilter!.Tag.Should().BeNull();
        vm.SelectedLibraryTagFilter.Label.Should().Be(vm.Ui.FilterAll);
    }

    [Fact]
    public void ColumnTag_is_localized_not_the_key_name()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.Ui.ColumnTag.Should().Be(LocalizationService.T("ColumnTag"));
        vm.Ui.ColumnTag.Should().NotBe("ColumnTag");
        vm.Ui.ColumnTag.Should().NotBeNullOrWhiteSpace();
    }
}
