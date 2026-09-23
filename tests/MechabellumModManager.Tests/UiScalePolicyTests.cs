using FluentAssertions;
using MechabellumModManager.Services;

public class UiScalePolicyTests
{
    [Fact]
    public void Manual_steps_match_the_offered_range()
    {
        UiScalePolicy.ExplicitCodes.Should().Equal(
            "0.8", "1", "1.1", "1.25", "1.5", "1.75", "2", "2.25", "2.5");
    }

    [Theory]
    [InlineData(1.0, 768, 1366, 1.0)]
    [InlineData(1.0, 1080, 1920, 1.0)]
    [InlineData(1.0, 1080, 2560, 1.0)]
    [InlineData(1.25, 1080, 1920, 1.0)]
    [InlineData(1.0, 1152, 2048, 1.1)]
    [InlineData(1.25, 1440, 2560, 1.1)]
    [InlineData(1.0, 1440, 2560, 1.25)]
    [InlineData(1.0, 1440, 3440, 1.25)]
    [InlineData(1.5, 2160, 3840, 1.25)]
    [InlineData(1.0, 1600, 2560, 1.5)]
    [InlineData(1.0, 1800, 2880, 1.75)]
    [InlineData(1.0, 2160, 3840, 2.0)]
    [InlineData(2.0, 2160, 3840, 1.0)]
    [InlineData(1.0, 2430, 4320, 2.25)]
    [InlineData(1.0, 2880, 5120, 2.5)]
    [InlineData(1.0, 4320, 7680, 2.5)]
    public void Auto_matches_logical_short_side_to_a_step(
        double dpi, int height, int width, double expected)
    {
        UiScalePolicy.Resolve(null, dpi, height, width).Should().Be(expected);
        UiScalePolicy.Resolve("auto", dpi, height, width).Should().Be(expected);
        UiScalePolicy.Resolve("  ", dpi, height, width).Should().Be(expected);
    }

    [Fact]
    public void Auto_never_picks_the_shrink_step()
    {
        UiScalePolicy.Resolve(null, systemDpiScale: 1, screenHeightPixels: 720, screenWidthPixels: 1280)
            .Should().Be(1);
    }

    [Fact]
    public void Auto_uses_height_when_width_is_unknown()
    {
        UiScalePolicy.Resolve(null, 1, 2160).Should().Be(2);
        UiScalePolicy.Resolve(null, 1, 1440).Should().Be(1.25);
        UiScalePolicy.Resolve(null, 1, 1080).Should().Be(1);
    }

    [Theory]
    [InlineData("0.8", 0.8)]
    [InlineData("0.80", 0.8)]
    [InlineData("1", 1)]
    [InlineData("1.0", 1)]
    [InlineData("1.1", 1.1)]
    [InlineData("1.10", 1.1)]
    [InlineData("1.25", 1.25)]
    [InlineData("1.5", 1.5)]
    [InlineData("1.50", 1.5)]
    [InlineData("1.75", 1.75)]
    [InlineData("2", 2)]
    [InlineData("2.0", 2)]
    [InlineData("2.25", 2.25)]
    [InlineData("2.5", 2.5)]
    [InlineData("2.50", 2.5)]
    public void Explicit_choice_ignores_the_screen(string configured, double expected)
    {
        UiScalePolicy.Resolve(configured, systemDpiScale: 1, screenHeightPixels: 1080, screenWidthPixels: 1920)
            .Should().Be(expected);
        UiScalePolicy.Resolve(configured, systemDpiScale: 2, screenHeightPixels: 2160, screenWidthPixels: 3840)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "auto")]
    [InlineData("", "auto")]
    [InlineData("auto", "auto")]
    [InlineData("9", "auto")]
    [InlineData("150%", "auto")]
    [InlineData("0.8", "0.8")]
    [InlineData("1.1", "1.1")]
    [InlineData("1.5", "1.5")]
    [InlineData(" 2.25 ", "2.25")]
    [InlineData("2.5", "2.5")]
    public void Normalize_keeps_only_the_known_choices(string? configured, string expected)
    {
        UiScalePolicy.Normalize(configured).Should().Be(expected);
    }
}
