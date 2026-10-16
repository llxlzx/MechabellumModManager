using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class ModFeedbackMailTests
{
    [Fact]
    public void Subject_uses_the_visible_name_and_omits_an_empty_version()
    {
        var mail = ModFeedbackMail.Build("author@example.com", "网格标尺", "2.0.0");
        mail.To.Should().Be("author@example.com");
        mail.Subject.Should().Be("[Mod 反馈 / Feedback] 网格标尺 2.0.0");
        mail.Body.Should().Be("【Mod】网格标尺\n【版本 / Version】2.0.0\n【问题 / Issue】\n");

        ModFeedbackMail.Build("author@example.com", "网格标尺", "  ").Subject
            .Should().Be("[Mod 反馈 / Feedback] 网格标尺");
    }

    [Fact]
    public void Gmail_and_mailto_address_the_author_and_do_not_mention_the_maintainer()
    {
        var mail = ModFeedbackMail.Build("author@example.com", "Cam", "1");
        mail.GmailUrl.Should().Contain("to=" + Uri.EscapeDataString("author@example.com"));
        mail.GmailUrl.Should().NotContain("llxmod@foxmail.com");
        mail.MailtoUrl.Should().StartWith("mailto:author@example.com?");
        mail.MailtoUrl.Should().NotContain("llxmod");
    }

    [Fact]
    public void Qq_clipboard_contains_the_author_and_the_page_stays_the_qq_home()
    {
        var mail = ModFeedbackMail.Build("author@example.com", "Cam", "1");
        ModFeedbackMail.QqWebMail.Should().Be("https://wx.mail.qq.com/");
        mail.QqClipboard.Should().Contain("To: author@example.com").And.Contain(mail.Subject).And.Contain(mail.Body);
    }
}
