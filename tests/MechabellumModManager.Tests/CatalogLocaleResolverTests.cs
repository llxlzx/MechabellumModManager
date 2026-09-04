using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class CatalogLocaleResolverTests
{
    static CatalogMod ModWithLocales() => new()
    {
        Id = "feature-test",
        Name = "功能测试 MOD",
        Summary = "默认中文简介",
        Locales = new Dictionary<string, CatalogModLocale>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new CatalogModLocale
            {
                Name = "Feature Test Mod",
                Summary = "English summary"
            },
            ["de"] = new CatalogModLocale
            {
                Name = "Funktionstest-Mod"
                // summary omitted → fallback
            },
            ["ja"] = new CatalogModLocale
            {
                Name = "  ",
                Summary = ""
            }
        }
    };

    [Fact]
    public void ResolveName_falls_back_to_default_when_no_locales()
    {
        var mod = new CatalogMod { Name = "仅默认" };
        CatalogLocaleResolver.ResolveName(mod, "en").Should().Be("仅默认");
    }

    [Fact]
    public void ResolveName_uses_locale_when_present()
    {
        var mod = ModWithLocales();
        CatalogLocaleResolver.ResolveName(mod, "en").Should().Be("Feature Test Mod");
        CatalogLocaleResolver.ResolveName(mod, "de").Should().Be("Funktionstest-Mod");
    }

    [Fact]
    public void ResolveSummary_falls_back_when_locale_omits_summary()
    {
        var mod = ModWithLocales();
        CatalogLocaleResolver.ResolveSummary(mod, "de").Should().Be("默认中文简介");
        CatalogLocaleResolver.ResolveSummary(mod, "en").Should().Be("English summary");
    }

    [Fact]
    public void ResolveName_treats_whitespace_locale_as_missing()
    {
        var mod = ModWithLocales();
        CatalogLocaleResolver.ResolveName(mod, "ja").Should().Be("功能测试 MOD");
    }

    [Fact]
    public void Resolve_uses_current_ui_culture_when_culture_null()
    {
        var mod = ModWithLocales();
        LocalizationService.Apply("en");
        try
        {
            CatalogLocaleResolver.ResolveName(mod).Should().Be("Feature Test Mod");
            CatalogLocaleResolver.ResolveSummary(mod).Should().Be("English summary");
        }
        finally
        {
            LocalizationService.Apply("zh-CN");
        }
    }

    [Fact]
    public void Resolve_overloads_work_for_package_defaults()
    {
        var locales = ModWithLocales().Locales!;
        CatalogLocaleResolver.ResolveName("功能测试 MOD", locales, "en")
            .Should().Be("Feature Test Mod");
        CatalogLocaleResolver.ResolveSummary("默认中文简介", locales, "de")
            .Should().Be("默认中文简介");
    }
}
