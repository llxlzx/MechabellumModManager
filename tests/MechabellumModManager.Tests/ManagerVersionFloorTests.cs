using FluentAssertions;
using MechabellumModManager.Services;

public class ManagerVersionFloorTests
{
    [Theory]
    [InlineData("1.3.14", "1.3.14", true)]
    [InlineData("1.3.14", "1.3.15", true)]
    [InlineData("1.3.14", "1.4.0", true)]
    [InlineData("1.3.14", "1.3.13", false)]
    [InlineData("1.3.14", "1.3.9", false)]
    public void Compare_major_minor_patch(string required, string local, bool allowed)
    {
        var decision = ManagerVersionFloor.Evaluate(required, local);
        decision.Allowed.Should().Be(allowed);
        decision.Unreadable.Should().BeFalse();
        decision.Required.Should().Be(required);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_floor_allows_download(string? required)
    {
        var decision = ManagerVersionFloor.Evaluate(required, "1.3.14");
        decision.Allowed.Should().BeTrue();
        decision.Unreadable.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.3")]
    [InlineData("1.3.14.1")]
    [InlineData("1.3.14-rc")]
    [InlineData("v1.3.14")]
    [InlineData("1.03.14")]
    public void Unreadable_catalog_value_refuses(string required)
    {
        var decision = ManagerVersionFloor.Evaluate(required, "1.3.14");
        decision.Allowed.Should().BeFalse();
        decision.Unreadable.Should().BeTrue();
    }

    [Theory]
    [InlineData("1.3.14+abc")]
    [InlineData("v1.3.14")]
    [InlineData("1.3.14.0")]
    public void Local_version_uses_the_first_three_segments(string local)
    {
        ManagerVersionFloor.Evaluate("1.3.14", local).Allowed.Should().BeTrue();
        ManagerVersionFloor.CanonicalLocal(local).Should().Be("1.3.14");
    }

    [Fact]
    public void Unreadable_local_version_fails_closed_when_a_floor_is_set()
    {
        var decision = ManagerVersionFloor.Evaluate("1.3.14", "nightly");
        decision.Allowed.Should().BeFalse();
        decision.Unreadable.Should().BeFalse();
    }
}
