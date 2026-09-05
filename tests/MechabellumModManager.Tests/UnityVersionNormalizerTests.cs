using FluentAssertions;
using MechabellumModManager.Services;

public class UnityVersionNormalizerTests
{
    [Theory]
    [InlineData("2022.3.62f3", "2022.3.62")]
    [InlineData("2022.3.62", "2022.3.62")]
    [InlineData(" 2022.3.62f1 ", "2022.3.62")]
    public void TryNormalize_strips_suffix(string raw, string expected)
    {
        UnityVersionNormalizer.TryNormalize(raw, out var v).Should().BeTrue();
        v.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("2022.3")]
    public void TryNormalize_rejects_invalid(string? raw)
    {
        UnityVersionNormalizer.TryNormalize(raw, out var v).Should().BeFalse();
        v.Should().BeNull();
    }

    [Fact]
    public void ExpectedZipFileName_matches_Melon_convention()
    {
        UnityVersionNormalizer.ExpectedZipFileName("2022.3.62")
            .Should().Be("UnityDependencies_2022.3.62.zip");
    }
}
