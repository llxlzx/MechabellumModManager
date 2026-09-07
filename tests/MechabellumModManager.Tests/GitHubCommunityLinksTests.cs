using System.Globalization;
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

    [Theory]
    [InlineData("zh-CN", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("zh", true)]
    [InlineData("en", false)]
    [InlineData("en-US", false)]
    [InlineData("ja", false)]
    [InlineData("de", false)]
    [InlineData("ru", false)]
    public void PreferDomesticWebMail_follows_current_ui_culture(string culture, bool expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            GitHubCommunityLinks.PreferDomesticWebMail().Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void BuildClipboardPackage_includes_to_subject_and_body()
    {
        var package = GitHubCommunityLinks.BuildClipboardPackage("Subj", "Body line");
        package.Should().Be("To: llxmod@foxmail.com\r\nSubject: Subj\r\n\r\nBody line");
    }

    [Fact]
    public void TruncateForUrl_truncates_long_body()
    {
        var longBody = new string('a', 1500);
        var truncated = GitHubCommunityLinks.TruncateForUrl(longBody, maxBodyChars: 1200);
        truncated.Length.Should().BeLessThan(longBody.Length);
        truncated.Should().EndWith("…(truncated)");
        truncated.Should().StartWith(new string('a', 1200));
    }

    [Fact]
    public void BuildGmailComposeUrl_encodes_and_truncates()
    {
        var subject = "Hello & World";
        var body = "line1\nline2 & more";
        var url = GitHubCommunityLinks.BuildGmailComposeUrl(subject, body);

        url.Should().StartWith("https://mail.google.com/mail/?view=cm&fs=1&tf=1");
        url.Should().Contain("to=" + Uri.EscapeDataString(GitHubCommunityLinks.Inbox));
        url.Should().Contain("su=" + Uri.EscapeDataString(subject));
        url.Should().Contain("body=" + Uri.EscapeDataString(body));
        url.Length.Should().BeLessThan(1800);

        var huge = new string('x', 5000);
        var hugeUrl = GitHubCommunityLinks.BuildGmailComposeUrl("s", huge);
        hugeUrl.Length.Should().BeLessThan(4000);
        hugeUrl.Should().Contain(Uri.EscapeDataString("…(truncated)"));
    }

    [Fact]
    public void BuildSubmitBody_includes_category_and_tags_lines()
    {
        var body = GitHubCommunityLinks.BuildSubmitBody("Demo");
        body.Should().Contain("Category");
        body.Should().Contain("Tags");
        body.Should().Contain("OverlayUI");
    }

    [Fact]
    public void DomesticWebMailUrl_is_qq_web()
    {
        GitHubCommunityLinks.DomesticWebMailUrl.Should().Be("https://wx.mail.qq.com/");
    }

    [Fact]
    public void BuildSubmitCompose_exposes_subject_body_and_mailto()
    {
        var compose = GitHubCommunityLinks.BuildSubmitCompose("Demo Mod");
        compose.Subject.Should().Be("[Mod投稿/Submit] Demo Mod");
        compose.Body.Should().Contain("【类型 / Type】投稿 / Submit（新 Mod）");
        compose.MailtoUrl.Should().StartWith("mailto:llxmod@foxmail.com?");
        compose.MailtoUrl.Should().Contain(Uri.EscapeDataString(compose.Subject));
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

    [Fact]
    public void TryOpenCompose_qq_copies_clipboard_and_opens_qq_url()
    {
        string? copied = null;
        string? opened = null;
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var (openedOk, url) = GitHubCommunityLinks.TryOpenCompose(
                "S",
                "B",
                text => copied = text,
                MailProvider.Qq,
                tryOpen: u => { opened = u; return true; });

            openedOk.Should().BeTrue();
            url.Should().Be(GitHubCommunityLinks.DomesticWebMailUrl);
            opened.Should().Be(GitHubCommunityLinks.DomesticWebMailUrl);
            copied.Should().Contain("To: llxmod@foxmail.com");
            copied.Should().Contain("Subject: S");
            copied.Should().Contain("B");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void TryOpenCompose_gmail_copies_clipboard_and_opens_gmail_url()
    {
        string? copied = null;
        string? opened = null;
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var (openedOk, url) = GitHubCommunityLinks.TryOpenCompose(
                "S",
                "B",
                text => copied = text,
                MailProvider.Gmail,
                tryOpen: u => { opened = u; return true; });

            openedOk.Should().BeTrue();
            url.Should().StartWith("https://mail.google.com/mail/");
            opened.Should().StartWith("https://mail.google.com/mail/");
            copied.Should().Contain("To: llxmod@foxmail.com");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void TryOpenCompose_cancel_copies_clipboard_and_does_not_open()
    {
        string? copied = null;
        var openCalls = 0;
        var (openedOk, url) = GitHubCommunityLinks.TryOpenCompose(
            "S",
            "B",
            text => copied = text,
            provider: null,
            tryOpen: _ => { openCalls++; return true; });

        openedOk.Should().BeFalse();
        url.Should().BeEmpty();
        openCalls.Should().Be(0);
        copied.Should().Contain("To: llxmod@foxmail.com");
        copied.Should().Contain("Subject: S");
        copied.Should().Contain("B");
    }
}
