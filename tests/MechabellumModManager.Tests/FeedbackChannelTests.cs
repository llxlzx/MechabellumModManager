using FluentAssertions;
using MechabellumModManager.Services;

public class FeedbackChannelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_is_hidden(string? raw)
    {
        FeedbackChannel.Classify(raw).Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Theory]
    [InlineData("http://example.com/form")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("vbscript:msgbox")]
    public void Non_https_schemes_are_hidden(string raw)
    {
        FeedbackChannel.Classify(raw).Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Fact]
    public void Https_with_userinfo_is_hidden()
    {
        FeedbackChannel.Classify("https://user@evil.example/form").Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Fact]
    public void Plain_email_and_mailto_open_mail_to_that_address_only()
    {
        FeedbackChannel.Classify("Player@Example.com").Should().BeEquivalentTo(new FeedbackDecision(
            FeedbackKind.Mail, "Player@Example.com", "Player@Example.com"));
        FeedbackChannel.Classify("mailto:player@example.com?subject=old").Should().BeEquivalentTo(
            new FeedbackDecision(FeedbackKind.Mail, "player@example.com", "mailto:player@example.com?subject=old"));
    }

    [Fact]
    public void Broken_mailto_is_hidden()
    {
        FeedbackChannel.Classify("mailto:not-an-email").Kind.Should().Be(FeedbackKind.Hidden);
        FeedbackChannel.Classify("mailto:a@b.com,c@d.com").Kind.Should().Be(FeedbackKind.Hidden);
    }

    [Fact]
    public void Acceptable_https_opens_that_url()
    {
        const string url = "https://example.com/form";
        FeedbackChannel.Classify(url).Should().BeEquivalentTo(new FeedbackDecision(FeedbackKind.OpenHttps, url, url));
    }

    [Theory]
    [InlineData("QQ 12345")]
    [InlineData("example.com/form")]
    [InlineData("邮箱是 a@b.com")]
    public void Other_text_is_copied_whole(string raw)
    {
        var decision = FeedbackChannel.Classify(raw);
        decision.Kind.Should().Be(FeedbackKind.Copy);
        decision.Target.Should().Be(raw);
        decision.Original.Should().Be(raw);
    }
}
