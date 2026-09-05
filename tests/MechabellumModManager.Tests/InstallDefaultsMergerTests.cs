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
    public void TryMerge_null_seed_is_noop()
    {
        var user = new AppConfig();
        InstallDefaultsMerger.TryMerge(user, null, userConfigExisted: false).Should().BeFalse();
        user.GamePath.Should().BeEmpty();
    }
}
