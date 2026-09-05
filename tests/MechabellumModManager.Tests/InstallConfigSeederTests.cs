using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class InstallConfigSeederTests
{
    [Fact]
    public void TryParseArgs_requires_seed_flag_and_game_path()
    {
        InstallConfigSeeder.TryParseArgs(["--game-path", @"D:\g"], out _, out _).Should().BeFalse();
        InstallConfigSeeder.TryParseArgs(
                [InstallConfigSeeder.SeedArg, "--game-path", @"D:\g", "--ui-language", "en"],
                out var path,
                out var lang)
            .Should().BeTrue();
        path.Should().Be(@"D:\g");
        lang.Should().Be("en");
    }

    [Fact]
    public void Seed_writes_appdata_config_and_programdata_defaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-seed-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var common = Path.Combine(root, "common");
        try
        {
            InstallConfigSeeder.Seed(@"D:\steam\common\Mechabellum", "zh-CN", app, common);

            var cfg = new JsonStore().LoadOrDefault(Path.Combine(app, "config.json"), () => new AppConfig());
            cfg.GamePath.Should().Be(@"D:\steam\common\Mechabellum");
            cfg.UiLanguage.Should().Be("zh-CN");
            File.Exists(Path.Combine(app, "profiles", "default.json")).Should().BeTrue();

            var seed = new JsonStore().LoadOrDefault(Path.Combine(common, "install-defaults.json"), () => new InstallDefaults());
            seed.GamePath.Should().Be(@"D:\steam\common\Mechabellum");
            seed.UiLanguage.Should().Be("zh-CN");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
}
