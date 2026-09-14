using System.Globalization;
using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class DomesticMirrorDefaultsTests
{
    [Theory]
    [InlineData("CN", true)]
    [InlineData("cn", true)]
    [InlineData("US", false)]
    [InlineData("GB", false)]
    [InlineData("TW", false)]
    [InlineData("HK", false)]
    [InlineData("JP", false)]
    [InlineData("DE", false)]
    public void IsMainlandChinaRegion_only_true_for_CN(string regionName, bool expected)
    {
        var region = new RegionInfo(regionName);
        DomesticMirrorDefaults.IsMainlandChinaRegion(region).Should().Be(expected);
    }

    [Fact]
    public void ResolveFactoryMirrorBaseUrl_uses_cos_only_in_mainland_China()
    {
        DomesticMirrorDefaults.ResolveFactoryMirrorBaseUrl(new RegionInfo("CN"))
            .Should().Be(DomesticMirrorDefaults.BaseUrl);

        DomesticMirrorDefaults.ResolveFactoryMirrorBaseUrl(new RegionInfo("US"))
            .Should().Be("");
    }

    [Fact]
    public void IsBuiltInCosUrl_matches_factory_base_ignoring_trailing_slash()
    {
        DomesticMirrorDefaults.IsBuiltInCosUrl(DomesticMirrorDefaults.BaseUrl).Should().BeTrue();
        DomesticMirrorDefaults.IsBuiltInCosUrl(DomesticMirrorDefaults.BaseUrl + "/").Should().BeTrue();
        DomesticMirrorDefaults.IsBuiltInCosUrl("https://custom.example.com/m").Should().BeFalse();
        DomesticMirrorDefaults.IsBuiltInCosUrl("").Should().BeFalse();
    }
}
