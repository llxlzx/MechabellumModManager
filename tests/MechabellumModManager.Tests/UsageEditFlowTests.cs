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
