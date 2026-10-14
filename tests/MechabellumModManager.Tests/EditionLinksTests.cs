using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class EditionLinksTests
{
    const string Mirror = "https://mirror.example.com/m";

    [Fact]
    public void China_edition_with_a_mirror_uses_the_mirrored_pages()
    {
        EditionLinks.ContributeGuide(AppEdition.Cn, Mirror + "/", DownloadRouteKind.MirrorFirst)
            .Should().Be(Mirror + "/MechabellumMods/docs/submit.html");
        EditionLinks.ReleasesPage(AppEdition.Cn, Mirror, DownloadRouteKind.MirrorFirst)
            .Should().Be(Mirror + "/MechabellumModManager/download.html");
    }

    [Theory]
    [InlineData(AppEdition.Global, Mirror, DownloadRouteKind.MirrorFirst)]
    [InlineData(AppEdition.Cn, null, DownloadRouteKind.MirrorFirst)]
    [InlineData(AppEdition.Cn, "", DownloadRouteKind.MirrorFirst)]
    [InlineData(AppEdition.Cn, "http://mirror.example.com/m", DownloadRouteKind.MirrorFirst)]
    [InlineData(AppEdition.Cn, Mirror, DownloadRouteKind.GitHubOnly)]
    public void Otherwise_the_links_stay_on_GitHub(AppEdition edition, string? mirror, DownloadRouteKind route)
    {
        EditionLinks.ContributeGuide(edition, mirror, route).Should().Be(GitHubCommunityLinks.ContributeGuideUrl);
        EditionLinks.ReleasesPage(edition, mirror, route).Should().Be(UpdateChecker.DefaultBrowseUrl);
    }
}
