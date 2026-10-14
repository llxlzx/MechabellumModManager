using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class InstallDefaultsMergerTests
{
    [Fact]
    public void TryMerge_fills_blank_gamePath_and_language_when_config_missing()
    {
        var user = new AppConfig();
        var seed = new InstallDefaults { GamePath = @"D:\steam\common\Mechabellum", UiLanguage = "en" };

        InstallDefaultsMerger.TryMerge(user, seed, userConfigExisted: false).Should().BeTrue();

        user.GamePath.Should().Be(@"D:\steam\common\Mechabellum");
        user.UiLanguage.Should().Be("en");
    }

    [Fact]
    public void TryMerge_does_not_overwrite_existing_gamePath_or_language_file()
    {
        var user = new AppConfig
        {
            GamePath = @"D:\games\Mechabellum",
            UiLanguage = "system"
        };
        var seed = new InstallDefaults { GamePath = @"D:\other", UiLanguage = "de" };

        InstallDefaultsMerger.TryMerge(user, seed, userConfigExisted: true).Should().BeFalse();

        user.GamePath.Should().Be(@"D:\games\Mechabellum");
        user.UiLanguage.Should().Be("system");
    }

    [Fact]
    public void TryMerge_fills_empty_gamePath_even_when_config_existed()
    {
        var user = new AppConfig { GamePath = "", UiLanguage = "ja" };
        var seed = new InstallDefaults { GamePath = @"E:\Mechabellum", UiLanguage = "en" };

        InstallDefaultsMerger.TryMerge(user, seed, userConfigExisted: true).Should().BeTrue();

        user.GamePath.Should().Be(@"E:\Mechabellum");
        user.UiLanguage.Should().Be("ja");
    }

    [Fact]
    public void TryMerge_applies_seed_download_route_when_config_missing()
    {
        var user = new AppConfig();
        var seed = new InstallDefaults { DownloadRoutePreference = (int)DownloadRoutePreference.AlwaysGitHub };

        InstallDefaultsMerger.TryMerge(user, seed, userConfigExisted: false).Should().BeTrue();

        user.DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
    }

    [Fact]
    public void TryMerge_keeps_existing_download_route()
    {
        var user = new AppConfig { DownloadRoutePreference = (int)DownloadRoutePreference.Auto };
        var seed = new InstallDefaults { DownloadRoutePreference = (int)DownloadRoutePreference.AlwaysMirror };

        InstallDefaultsMerger.TryMerge(user, seed, userConfigExisted: true).Should().BeFalse();

        user.DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.Auto);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void TryMerge_ignores_unknown_seed_download_route(int route)
    {
        var user = new AppConfig();
        var seed = new InstallDefaults { DownloadRoutePreference = route };

        InstallDefaultsMerger.TryMerge(user, seed, userConfigExisted: false).Should().BeFalse();

        user.DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.Auto);
    }

    [Fact]
    public void TryMerge_applies_seed_edition_only_to_a_new_config()
    {
        var fresh = new AppConfig();
        InstallDefaultsMerger.TryMerge(fresh, new InstallDefaults { Edition = "global" }, userConfigExisted: false)
            .Should().BeTrue();
        fresh.Edition.Should().Be("global");

        var existing = new AppConfig { Edition = "cn" };
        InstallDefaultsMerger.TryMerge(existing, new InstallDefaults { Edition = "global" }, userConfigExisted: true)
            .Should().BeFalse();
        existing.Edition.Should().Be("cn");
    }

    [Fact]
    public void TryMerge_ignores_an_unknown_seed_edition()
    {
        var user = new AppConfig();

        InstallDefaultsMerger.TryMerge(user, new InstallDefaults { Edition = "intl" }, userConfigExisted: false)
            .Should().BeFalse();
        user.Edition.Should().BeNull();
    }

    [Fact]
    public void TryMerge_null_seed_is_noop()
    {
        var user = new AppConfig();
        InstallDefaultsMerger.TryMerge(user, null, userConfigExisted: false).Should().BeFalse();
        user.GamePath.Should().BeEmpty();
    }
}
