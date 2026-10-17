using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class FeedbackChoiceTests
{
    [Fact]
    public void Acceptable_keeps_labeled_https_and_drops_the_rest()
    {
        var accepted = FeedbackChoices.Acceptable(
        [
            new FeedbackChoice { Label = " QQ 频道 ", Url = " https://pd.qq.com/s/mrm8s2di " },
            new FeedbackChoice { Label = "Discord", Url = "https://discord.com/users/1" },
            new FeedbackChoice { Label = " ", Url = "https://example.com" },
            new FeedbackChoice { Label = "bad", Url = "http://example.com" },
            new FeedbackChoice { Label = "user", Url = "https://user@evil.example/form" },
            null!
        ]);

        accepted.Select(c => c.Label).Should().Equal("QQ 频道", "Discord");
        accepted.Select(c => c.Url).Should().Equal(
            "https://pd.qq.com/s/mrm8s2di",
            "https://discord.com/users/1");
    }

    [Fact]
    public void China_picks_the_qq_channel_and_global_picks_discord()
    {
        var choices = FeedbackChoices.FromText(
            "国内玩家请到 QQ 频道反馈帖：https://pd.qq.com/s/mrm8s2di\n国际玩家请打开巴巴的 Discord：https://discord.com/users/592955314635014155");
        FeedbackChoices.Pick(choices, AppEdition.Cn).Should().Be("https://pd.qq.com/s/mrm8s2di");
        FeedbackChoices.Pick(choices, AppEdition.Global).Should().Be("https://discord.com/users/592955314635014155");
    }

    [Fact]
    public void Acceptable_of_null_is_empty()
    {
        FeedbackChoices.Acceptable(null).Should().BeEmpty();
    }
}
