using FluentAssertions;
using MechabellumModManager.Services;

public class FeaturedCatalogTests
{
    [Fact]
    public void Lead_is_battle_suite_then_llxmod_then_reinforcement_calculator()
    {
        FeaturedCatalog.Ids.Should().Equal(
            "battle-suite",
            "friend-overlay",
            "replay-reset",
            "sales-calculation",
            "team-beacon");
    }

    [Fact]
    public void Pick_follows_the_curated_order_and_skips_ids_missing_from_the_catalog()
    {
        var mods = new[]
        {
            new { Id = "sales-calculation", Name = "增援" },
            new { Id = "replay-reset", Name = "回放" },
            new { Id = "show-grid", Name = "格线" },
            new { Id = "battle-suite", Name = "BattleSuite" },
        };

        FeaturedCatalog.Pick(mods, m => m.Id)
            .Select(m => m.Id)
            .Should().Equal("battle-suite", "replay-reset", "sales-calculation");
    }
}
