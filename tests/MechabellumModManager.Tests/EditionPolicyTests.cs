using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class EditionPolicyTests
{
    [Theory]
    [InlineData("cn", AppEdition.Cn)]
    [InlineData(" CN ", AppEdition.Cn)]
    [InlineData("global", AppEdition.Global)]
    [InlineData("Global", AppEdition.Global)]
    public void Codes_parse_ignoring_case_and_spaces(string raw, AppEdition expected)
    {
        EditionPolicy.TryParse(raw, out var edition).Should().BeTrue();
        edition.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("intl")]
    public void Unknown_codes_do_not_parse(string? raw)
    {
        EditionPolicy.TryParse(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void Codes_round_trip()
    {
        EditionPolicy.Format(AppEdition.Cn).Should().Be("cn");
        EditionPolicy.Format(AppEdition.Global).Should().Be("global");
    }

    [Theory]
    [InlineData(2, "zh-CN", AppEdition.Global)]
    [InlineData(1, "en", AppEdition.Cn)]
    [InlineData(0, "zh-CN", AppEdition.Cn)]
    [InlineData(0, "de", AppEdition.Global)]
    public void An_old_config_is_inferred_from_its_route_then_the_system_language(
        int route, string systemLanguage, AppEdition expected)
    {
        EditionPolicy.Infer(route, systemLanguage).Should().Be(expected);
    }

    [Fact]
    public void Resolve_infers_once_saves_the_code_and_leaves_other_settings_alone()
    {
        var config = new AppConfig { DownloadRoutePreference = 0, UiLanguage = "system" };

        EditionPolicy.Resolve(config, "zh-CN", out var edition).Should().BeTrue();

        edition.Should().Be(AppEdition.Cn);
        config.Edition.Should().Be("cn");
        config.DownloadRoutePreference.Should().Be(0);
        config.UiLanguage.Should().Be("system");
    }

    [Fact]
    public void Resolve_keeps_a_saved_edition_even_when_the_route_disagrees()
    {
        var config = new AppConfig { Edition = "cn", DownloadRoutePreference = 2 };

        EditionPolicy.Resolve(config, "en", out var edition).Should().BeFalse();

        edition.Should().Be(AppEdition.Cn);
        config.DownloadRoutePreference.Should().Be(2);
    }

    [Fact]
    public void Each_edition_has_its_own_defaults()
    {
        EditionPolicy.DefaultRoute(AppEdition.Cn).Should().Be((int)DownloadRoutePreference.AlwaysMirror);
        EditionPolicy.DefaultRoute(AppEdition.Global).Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
        EditionPolicy.LanguageFallback(AppEdition.Cn).Should().Be("zh-CN");
        EditionPolicy.LanguageFallback(AppEdition.Global).Should().Be("en");
        EditionPolicy.PreferredMail(AppEdition.Cn).Should().Be(MailProvider.Qq);
        EditionPolicy.PreferredMail(AppEdition.Global).Should().Be(MailProvider.Gmail);
    }
}
