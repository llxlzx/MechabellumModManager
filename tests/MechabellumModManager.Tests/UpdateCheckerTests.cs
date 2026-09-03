using FluentAssertions;
using MechabellumModManager.Services;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("v1.2.0", "1.1.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("2.0", "1.9.9", true)]
    public void IsNewer_compares_semver_like(string remote, string local, bool expected)
    {
        UpdateChecker.IsNewer(remote, local).Should().Be(expected);
    }

    [Theory]
    [InlineData("v1.0.0+abc", "1.0.0")]
    [InlineData("1.2.3", "1.2.3")]
    public void NormalizeVersion_strips_prefix_and_metadata(string raw, string expected)
    {
        UpdateChecker.NormalizeVersion(raw).Should().Be(expected);
    }
}
