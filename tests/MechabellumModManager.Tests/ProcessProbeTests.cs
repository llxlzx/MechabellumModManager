using FluentAssertions;
using MechabellumModManager.Services;

public class ProcessProbeTests
{
    [Fact]
    public void IsGameOrSteamRunning_matches_individual_flags()
    {
        var probe = new ProcessProbe();
        // Cannot assert true without running Steam; assert API exists and is consistent:
        (probe.IsGameRunning() || probe.IsSteamRunning()).Should().Be(probe.IsGameOrSteamRunning());
    }
}
