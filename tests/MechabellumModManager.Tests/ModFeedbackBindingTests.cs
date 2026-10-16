using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using MechabellumModManager.Tests.Support;

public class ModFeedbackBindingTests
{
    [Fact]
    public void Library_feedback_tracks_only_the_current_match()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var vm = fx.CreateVm();
        vm.ApplyCatalogRoot(new CatalogRoot
        {
            Mods =
            [
                new CatalogMod
                {
                    Id = "cam-aaaaaaaa",
                    Name = "Cam",
                    File = "mods/cam/Cam.dll",
                    Feedback = "author@example.com"
                }
            ]
        });
        vm.Mods.Single().ShowModFeedback.Should().BeTrue();

        vm.ApplyCatalogRoot(new CatalogRoot { Mods = [] });
        vm.Mods.Single().ShowModFeedback.Should().BeFalse();
        File.ReadAllText(fx.Library.List().Single().PackageDirectory + "\\package.json")
            .Should().NotContain("feedback");
    }

    [Fact]
    public void Catalog_https_feedback_shows()
    {
        var item = new CatalogModItemViewModel(
            new CatalogMod
            {
                Id = "cam-aaaaaaaa",
                Name = "Cam",
                File = "mods/cam/Cam.dll",
                Feedback = "https://example.com/form"
            },
            CatalogEntryState.NotInstalled);
        item.ShowModFeedback.Should().BeTrue();
    }

    [Fact]
    public void Catalog_http_feedback_is_hidden()
    {
        var item = new CatalogModItemViewModel(
            new CatalogMod
            {
                Id = "cam-aaaaaaaa",
                Name = "Cam",
                File = "mods/cam/Cam.dll",
                Feedback = "http://example.com"
            },
            CatalogEntryState.NotInstalled);
        item.ShowModFeedback.Should().BeFalse();
    }
}
