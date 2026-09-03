using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class GitHubCommunityLinksTests
{
    [Fact]
    public void ContributeGuideUrl_points_at_email_readme_section()
    {
        GitHubCommunityLinks.ContributeGuideUrl
            .Should().Be("https://github.com/llxlzx/MechabellumMods#投稿与举报--submit--report");
    }

    [Fact]
    public void Inbox_is_foxmail()
    {
        GitHubCommunityLinks.Inbox.Should().Be("llxmod@foxmail.com");
    }

    [Fact]
    public void BuildSubmitMailto_uses_subject_prefix_and_template_body()
    {
        var url = GitHubCommunityLinks.BuildSubmitMailto("Demo Mod");

        url.Should().StartWith("mailto:llxmod@foxmail.com?");
        url.Should().Contain("subject=");
        url.Should().Contain(Uri.EscapeDataString("[Mod投稿/Submit] Demo Mod"));
        url.Should().Contain("body=");
        url.Should().Contain(Uri.EscapeDataString("【类型 / Type】投稿 / Submit（新 Mod）"));
        url.Should().Contain(Uri.EscapeDataString("【Mod 名称 / Name】Demo Mod"));
    }

    [Fact]
    public void BuildUpdateMailto_uses_update_prefix()
    {
        var url = GitHubCommunityLinks.BuildUpdateMailto("Grid");
        url.Should().Contain(Uri.EscapeDataString("[Mod更新/Update] Grid"));
        url.Should().Contain(Uri.EscapeDataString("【类型 / Type】更新 / Update"));
    }

    [Fact]
    public void BuildFeedbackMailto_uses_feedback_prefix()
    {
        var url = GitHubCommunityLinks.BuildFeedbackMailto("UI idea");
        url.Should().Contain(Uri.EscapeDataString("[管理器建议/Feedback] UI idea"));
        url.Should().Contain(Uri.EscapeDataString("【类型 / Type】管理器建议 / Feedback"));
    }

    [Fact]
    public void BuildReportMailto_contains_prefix_category_and_notes()
    {
        var url = GitHubCommunityLinks.BuildReportMailto(
            modId: "demo-mod",
            modName: "Demo Mod",
            source: "catalog",
            category: ReportCategory.Cheat,
            notes: "notes & detail",
            appVersion: "1.0.5");

        url.Should().StartWith("mailto:llxmod@foxmail.com?");
        url.Should().Contain(Uri.EscapeDataString("[Mod举报/Report] Demo Mod"));
        url.Should().Contain(Uri.EscapeDataString("demo-mod"));
        url.Should().Contain(Uri.EscapeDataString("社区目录（Catalog）"));
        url.Should().Contain(Uri.EscapeDataString("作弊相关 / Cheat"));
        url.Should().Contain(Uri.EscapeDataString("notes & detail"));
        url.Should().Contain(Uri.EscapeDataString("1.0.5"));
    }

    [Fact]
    public void BuildReportBody_truncates_oversized_notes()
    {
        var notes = new string('x', ReportRequest.MaxNotesLength + 500);
        var body = GitHubCommunityLinks.BuildReportBody(
            "demo",
            "Demo",
            "library",
            ReportCategory.Other,
            notes,
            "1.0.5");

        body.Should().Contain("…(truncated)");
        body.Should().Contain("本地库（Library）");
        body.Length.Should().BeLessThan(ReportRequest.MaxNotesLength + 400);
    }
}
