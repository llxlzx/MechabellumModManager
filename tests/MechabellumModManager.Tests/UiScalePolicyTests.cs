using FluentAssertions;
using MechabellumModManager.Services;

public class UiScalePolicyTests
{
    [Theory]
    [InlineData(1.0, 768, 1366, 1.0)]
    [InlineData(1.0, 1080, 1920, 1.0)]
    [InlineData(1.0, 1080, 2560, 1.0)]
    [InlineData(1.0, 1439, 2560, 1.0)]
    [InlineData(1.0, 1440, 2560, 1.25)]
    [InlineData(1.0, 1440, 3440, 1.25)]
    [InlineData(1.0, 1600, 2560, 1.25)]
    [InlineData(1.0, 1799, 2880, 1.25)]
    [InlineData(1.0, 1800, 2880, 1.5)]
    [InlineData(1.0, 2160, 3840, 1.5)]
    [InlineData(1.0, 3840, 2160, 1.5)]
    [InlineData(1.0, 2880, 5120, 1.75)]
    [InlineData(1.0, 4320, 7680, 2.0)]
    [InlineData(1.25, 2160, 3840, 1.0)]
    [InlineData(1.5, 1440, 2560, 1.0)]
    [InlineData(2.0, 2160, 3840, 1.0)]
    public void Auto_follows_the_short_side_unless_windows_already_scaled(
        double dpi, int height, int width, double expected)
    {
        UiScalePolicy.Resolve(null, dpi, height, width).Should().Be(expected);
        UiScalePolicy.Resolve("auto", dpi, height, width).Should().Be(expected);
        UiScalePolicy.Resolve("  ", dpi, height, width).Should().Be(expected);
    }

    [Fact]
    public void Auto_uses_height_when_width_is_unknown()
    {
        UiScalePolicy.Resolve(null, 1, 2160).Should().Be(1.5);
        UiScalePolicy.Resolve(null, 1, 1440).Should().Be(1.25);
        UiScalePolicy.Resolve(null, 1, 1080).Should().Be(1);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1.0", 1)]
    [InlineData("1.25", 1.25)]
    [InlineData("1.5", 1.5)]
    [InlineData("1.50", 1.5)]
    [InlineData("1.75", 1.75)]
    [InlineData("2", 2)]
    [InlineData("2.0", 2)]
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
    [InlineData("1.5", "1.5")]
    [InlineData(" 2 ", "2")]
    public void Normalize_keeps_only_the_known_choices(string? configured, string expected)
    {
        UiScalePolicy.Normalize(configured).Should().Be(expected);
    }
}
