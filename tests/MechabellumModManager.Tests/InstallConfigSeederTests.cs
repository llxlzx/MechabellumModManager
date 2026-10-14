using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class InstallConfigSeederTests
{
    [Fact]
    public void TryParseArgs_requires_seed_flag_and_game_path()
    {
        InstallConfigSeeder.TryParseArgs(["--game-path", @"D:\g"], out _, out _, out _, out _).Should().BeFalse();
        InstallConfigSeeder.TryParseArgs(
                [InstallConfigSeeder.SeedArg, "--game-path", @"D:\g", "--ui-language", "en"],
                out var path,
                out var lang,
                out var route,
                out _)
            .Should().BeTrue();
        path.Should().Be(@"D:\g");
        lang.Should().Be("en");
        route.Should().BeNull();
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("7", null)]
    [InlineData("github", null)]
    public void TryParseArgs_reads_download_route(string raw, int? expected)
    {
        InstallConfigSeeder.TryParseArgs(
                [InstallConfigSeeder.SeedArg, "--game-path", @"D:\g", "--download-route", raw],
                out _,
                out _,
                out var route,
                out _)
            .Should().BeTrue();
        route.Should().Be(expected);
    }

    [Fact]
    public void TryParseArgs_reads_a_known_edition_only()
    {
        InstallConfigSeeder.TryParseArgs(
                [InstallConfigSeeder.SeedArg, "--game-path", @"D:\g", "--edition", "Global"],
                out _, out _, out _, out var edition)
            .Should().BeTrue();
        edition.Should().Be("global");

        InstallConfigSeeder.TryParseArgs(
                [InstallConfigSeeder.SeedArg, "--game-path", @"D:\g", "--edition", "intl"],
                out _, out _, out _, out var unknown)
            .Should().BeTrue();
        unknown.Should().BeNull();
    }

    [Fact]
    public void Seed_writes_the_edition_into_a_fresh_config_but_keeps_an_existing_one()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-seed-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var common = Path.Combine(root, "common");
        try
        {
            var store = new JsonStore();
            InstallConfigSeeder.Seed(@"D:\g", "en", null, app, common, edition: "global");
            store.LoadOrDefault(Path.Combine(app, "config.json"), () => new AppConfig()).Edition.Should().Be("global");
            store.LoadOrDefault(Path.Combine(common, "install-defaults.json"), () => new InstallDefaults())
                .Edition.Should().Be("global");

            InstallConfigSeeder.Seed(@"D:\g", "en", null, app, common, edition: "cn");
            store.LoadOrDefault(Path.Combine(app, "config.json"), () => new AppConfig()).Edition.Should().Be("global");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Seed_writes_appdata_config_and_programdata_defaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-seed-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var common = Path.Combine(root, "common");
        try
        {
            InstallConfigSeeder.Seed(@"D:\steam\common\Mechabellum", "zh-CN", null, app, common);

            var cfg = new JsonStore().LoadOrDefault(Path.Combine(app, "config.json"), () => new AppConfig());
            cfg.GamePath.Should().Be(@"D:\steam\common\Mechabellum");
            cfg.UiLanguage.Should().Be("zh-CN");
            File.Exists(Path.Combine(app, "profiles", "default.json")).Should().BeTrue();

            var seed = new JsonStore().LoadOrDefault(Path.Combine(common, "install-defaults.json"), () => new InstallDefaults());
            seed.GamePath.Should().Be(@"D:\steam\common\Mechabellum");
            seed.UiLanguage.Should().Be("zh-CN");
            seed.DownloadRoutePreference.Should().BeNull();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Seed_presets_download_route_on_a_fresh_config()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-seed-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var common = Path.Combine(root, "common");
        try
        {
            InstallConfigSeeder.Seed(
                @"D:\steam\common\Mechabellum", "en", (int)DownloadRoutePreference.AlwaysGitHub, app, common);

            new JsonStore().LoadOrDefault(Path.Combine(app, "config.json"), () => new AppConfig())
                .DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
            new JsonStore().LoadOrDefault(Path.Combine(common, "install-defaults.json"), () => new InstallDefaults())
                .DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Seed_keeps_the_route_an_existing_config_already_has()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-seed-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var common = Path.Combine(root, "common");
        try
        {
            var store = new JsonStore();
            Directory.CreateDirectory(app);
            store.Save(Path.Combine(app, "config.json"), new AppConfig
            {
                GamePath = @"D:\old",
                DownloadRoutePreference = (int)DownloadRoutePreference.AlwaysGitHub
            });

            InstallConfigSeeder.Seed(
                @"D:\steam\common\Mechabellum", "zh-CN", (int)DownloadRoutePreference.AlwaysMirror, app, common);

            store.LoadOrDefault(Path.Combine(app, "config.json"), () => new AppConfig())
                .DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
            store.LoadOrDefault(Path.Combine(common, "install-defaults.json"), () => new InstallDefaults())
                .DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
}
