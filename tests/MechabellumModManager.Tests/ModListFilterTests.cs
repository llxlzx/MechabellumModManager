using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ModListFilterTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("hud", true)]
    [InlineData("HUD", true)]
    [InlineData("xyz", false)]
    public void MatchesSearch_case_insensitive_contains(string? search, bool expected)
    {
        ModListFilter.MatchesSearch(search, "Cam HUD", "author", "summary", "id").Should().Be(expected);
    }

    [Fact]
    public void MatchesCategory_null_filter_means_all()
    {
        ModListFilter.MatchesCategory(null, ModCategory.QoL).Should().BeTrue();
        ModListFilter.MatchesCategory(ModCategory.QoL, ModCategory.QoL).Should().BeTrue();
        ModListFilter.MatchesCategory(ModCategory.QoL, ModCategory.Camera).Should().BeFalse();
    }

    [Fact]
    public void MatchesTag_null_or_blank_means_all()
    {
        var tags = new[] { "hud", "grid" };
        ModListFilter.MatchesTag(null, tags).Should().BeTrue();
        ModListFilter.MatchesTag("", tags).Should().BeTrue();
        ModListFilter.MatchesTag("hud", tags).Should().BeTrue();
        ModListFilter.MatchesTag("hotkey", tags).Should().BeFalse();
    }

    [Fact]
    public void CompareUpdatedAtDesc_newer_first_nulls_last()
    {
        ModListFilter.CompareUpdatedAtDesc("2026-09-02", "2026-08-31").Should().BeLessThan(0);
        ModListFilter.CompareUpdatedAtDesc(null, "2026-08-31").Should().BeGreaterThan(0);
        ModListFilter.CompareUpdatedAtDesc("2026-08-31", null).Should().BeLessThan(0);
        ModListFilter.CompareUpdatedAtDesc(null, null).Should().Be(0);
    }
}
