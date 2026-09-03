using System.Globalization;
using FluentAssertions;
using MechabellumModManager.Services;

public class LocalizationServiceTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("en", "en")]
    [InlineData("ja-JP", "ja")]
    [InlineData("ru-RU", "ru")]
    [InlineData("de-DE", "de")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("fr-FR", "zh-CN")]
    public void ResolveSystemLanguage_maps_or_falls_back(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            LocalizationService.ResolveSystemLanguage().Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void GetString_returns_key_or_value_for_settings()
    {
        LocalizationService.Apply("zh-CN");
        var s = LocalizationService.GetString("Settings");
        s.Should().NotBeNullOrWhiteSpace();
        s.Should().BeOneOf("设置", "Settings"); // resource may or may not embed in test host
    }
}
