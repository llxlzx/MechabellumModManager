using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ModTaxonomyTests
{
    [Theory]
    [InlineData("OverlayUI", ModCategory.OverlayUI)]
    [InlineData("QoL", ModCategory.QoL)]
    [InlineData("Camera", ModCategory.Camera)]
    [InlineData("CombatAssist", ModCategory.CombatAssist)]
    [InlineData("Economy", ModCategory.Economy)]
    [InlineData("ReplayDebug", ModCategory.ReplayDebug)]
    [InlineData("Misc", ModCategory.Misc)]
    public void TryParseCategory_accepts_catalog_values(string raw, ModCategory expected)
    {
        ModTaxonomy.TryParseCategory(raw, out var cat).Should().BeTrue();
        cat.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Uncategorized")]
    [InlineData("overlayui")]
    [InlineData("Unknown")]
    public void TryParseCategory_rejects_blank_uncategorized_and_invalid(string? raw)
    {
        ModTaxonomy.TryParseCategory(raw, out _).Should().BeFalse();
        ModTaxonomy.ParseCategoryOrUncategorized(raw).Should().Be(ModCategory.Uncategorized);
    }

    [Fact]
    public void ResolveEffectiveCategory_override_wins_over_catalog()
    {
        ModTaxonomy.ResolveEffectiveCategory("Camera", "QoL").Should().Be(ModCategory.Camera);
        ModTaxonomy.ResolveEffectiveCategory(null, "QoL").Should().Be(ModCategory.QoL);
        ModTaxonomy.ResolveEffectiveCategory("bogus", "QoL").Should().Be(ModCategory.QoL);
        ModTaxonomy.ResolveEffectiveCategory("bogus", "also-bad").Should().Be(ModCategory.Uncategorized);
        ModTaxonomy.ResolveEffectiveCategory(null, null).Should().Be(ModCategory.Uncategorized);
    }

    [Fact]
    public void ResolveEffectiveTags_merges_catalog_then_extra_deduped()
    {
        var tags = ModTaxonomy.ResolveEffectiveTags(
            new[] { " hud ", "grid", "hud" },
            new[] { "grid", "hotkey", "  " });
        tags.Should().Equal("hud", "grid", "hotkey");
    }
}
