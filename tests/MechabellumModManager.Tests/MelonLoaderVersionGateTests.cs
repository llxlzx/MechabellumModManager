using FluentAssertions;
using MechabellumModManager.Services;

public class MelonLoaderVersionGateTests
{
    [Theory]
    [InlineData("0.7.1")]
    [InlineData("0.7.1.0")]
    [InlineData("0.6.9")]
    public void ShouldUpgrade_true_when_below_bundled_minimum(string version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeTrue();
    }

    [Theory]
    [InlineData("0.7.3")]
    [InlineData("0.7.3.0")]
    [InlineData("0.8.0")]
    public void ShouldUpgrade_false_when_at_or_above_bundled_minimum(string version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void ShouldUpgrade_false_when_unreadable(string? version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeFalse();
    }
}
