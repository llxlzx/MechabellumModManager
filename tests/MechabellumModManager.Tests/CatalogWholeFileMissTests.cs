using FluentAssertions;
using MechabellumModManager.Services;

public class CatalogWholeFileMissTests
{
    [Fact]
    public void Second_miss_does_not_refresh()
    {
        var budget = new WholeFileMissBudget();
        budget.TryUse().Should().BeTrue();
        budget.TryUse().Should().BeFalse();
    }

    [Fact]
    public void Refreshed_entry_with_parts_and_a_met_floor_downloads_parts()
    {
        var mod = new CatalogMod
        {
            MinManagerVersion = "1.3.14",
            Parts = [new CatalogPart { File = "mods/a/parts/0000", Sha256 = new string('a', 64), Size = 4 }]
        };
        CatalogWholeFileMiss.AfterRefresh(mod, "1.3.14").Should().Be(CatalogMissFollowUp.DownloadParts);
    }

    [Fact]
    public void Refreshed_entry_with_a_higher_floor_is_blocked()
    {
        var mod = new CatalogMod
        {
            MinManagerVersion = "1.3.18",
            Parts = [new CatalogPart { File = "mods/a/parts/0000", Sha256 = new string('a', 64), Size = 4 }]
        };
        CatalogWholeFileMiss.AfterRefresh(mod, "1.3.14").Should().Be(CatalogMissFollowUp.Blocked);
    }

    [Fact]
    public void Refreshed_entry_without_parts_shows_the_original_error()
    {
        CatalogWholeFileMiss.AfterRefresh(new CatalogMod { MinManagerVersion = "1.3.14" }, "1.3.14")
            .Should().Be(CatalogMissFollowUp.ShowError);
        CatalogWholeFileMiss.AfterRefresh(null, "1.3.14").Should().Be(CatalogMissFollowUp.ShowError);
    }

    [Theory]
    [InlineData("host: HTTP 404", true)]
    [InlineData("host: HTTP 403", false)]
    [InlineData("host: attempt 1: timeout", false)]
    public void All_404_detection(string failure, bool all404)
    {
        ModCatalogService.AllCandidatesHttp404([failure]).Should().Be(all404);
        ModCatalogService.AllCandidatesHttp404([]).Should().BeFalse();
    }
}
