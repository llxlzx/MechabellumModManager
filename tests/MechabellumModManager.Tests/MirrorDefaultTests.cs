using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public class MirrorDefaultTests
{
    [Fact]
    public void Null_config_applies_built_in_cos_default()
    {
        using var fx = MainViewModelFixture.CreateReady();
        // Simulate upgrade / first install: property never written.
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly,
            MirrorBaseUrl = null
        });

        var catalog = new ModCatalogService();
        var vm = fx.CreateVm(catalog: catalog);

        vm.MirrorBaseUrl.Should().Be(DomesticMirrorDefaults.BaseUrl);
        catalog.MirrorBaseUrl.Should().Be(DomesticMirrorDefaults.BaseUrl);
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig())
            .MirrorBaseUrl.Should().Be(DomesticMirrorDefaults.BaseUrl);
    }

    [Fact]
    public void Empty_string_disables_mirror_without_refill()
    {
        using var fx = MainViewModelFixture.CreateReady();
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly,
            MirrorBaseUrl = ""
        });

        var catalog = new ModCatalogService();
        var vm = fx.CreateVm(catalog: catalog);

        vm.MirrorBaseUrl.Should().Be("");
        catalog.MirrorBaseUrl.Should().BeNull();
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig())
            .MirrorBaseUrl.Should().Be("");
    }

    [Fact]
    public void Custom_https_url_is_preserved()
    {
        using var fx = MainViewModelFixture.CreateReady();
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly,
            MirrorBaseUrl = "https://custom.example.com/m"
        });

        var catalog = new ModCatalogService();
        var vm = fx.CreateVm(catalog: catalog);

        vm.MirrorBaseUrl.Should().Be("https://custom.example.com/m");
        catalog.MirrorBaseUrl.Should().Be("https://custom.example.com/m");
    }

    [Fact]
    public void Clearing_the_field_persists_empty_string_not_null()
    {
        using var fx = MainViewModelFixture.CreateReady();
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly,
            MirrorBaseUrl = DomesticMirrorDefaults.BaseUrl
        });

        var catalog = new ModCatalogService();
        var vm = fx.CreateVm(catalog: catalog);

        vm.MirrorBaseUrl = "   ";

        catalog.MirrorBaseUrl.Should().BeNull();
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig())
            .MirrorBaseUrl.Should().Be("");
    }
}
