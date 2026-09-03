using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class GitHubCommunityLinksTests
{
    [Fact]
    public void ContributeGuideUrl_is_stable_repo_root()
    {
        GitHubCommunityLinks.ContributeGuideUrl
            .Should().Be("https://github.com/llxlzx/MechabellumMods");
        GitHubCommunityLinks.ContributeGuideUrl
            .Should().Be(GitHubCommunityLinks.RepositoryUrl);
    }

    [Fact]
    public void BuildReportIssueUrl_contains_template_labels_title_and_body()
    {
        var url = GitHubCommunityLinks.BuildReportIssueUrl(
            modId: "demo-mod",
            modName: "Demo Mod",
            source: "catalog",
            category: ReportCategory.Cheat,
            notes: "notes & detail",
            appVersion: "1.0.0");

        url.Should().Contain("github.com/llxlzx/MechabellumMods");
        url.Should().Contain("/issues/new?");
        url.Should().Contain("template=mod_report.md");
        url.Should().Contain("labels=report");

        url.Should().Contain("title=");
        url.Should().Contain(Uri.EscapeDataString("[Report][cheat] Demo Mod"));

        url.Should().Contain("body=");
        url.Should().Contain(Uri.EscapeDataString("demo-mod"));
        url.Should().Contain(Uri.EscapeDataString("notes & detail"));
    }

    [Fact]
    public void BuildReportIssueUrl_truncates_oversized_notes()
    {
        var notes = new string('x', ReportRequest.MaxNotesLength + 500);
        var url = GitHubCommunityLinks.BuildReportIssueUrl(
            "demo",
            "Demo",
            "library",
            ReportCategory.Other,
            notes,
            "1.0.0");

        url.Length.Should().BeLessThan(8000);
        url.Should().Contain(Uri.EscapeDataString("…(truncated)"));
    }
}
