using BlackBox.Core;
using FluentAssertions;

public class BlackBoxStallPolicyTests
{
    static StallInput Sample(
        bool hasHeartbeat = true,
        double age = 0,
        bool definiteQuit = false,
        bool quitRequested = false,
        int heartbeatsSinceQuit = 0,
        StallPhase phase = StallPhase.Armed,
        double healthy = 0,
        bool dumpFinished = false) =>
        new(hasHeartbeat, age, definiteQuit, quitRequested, heartbeatsSinceQuit, phase, healthy, dumpFinished);

    [Fact]
    public void Never_heartbeat_does_not_hang()
    {
        var d = StallPolicy.Evaluate(Sample(hasHeartbeat: false, age: 100));
        d.BeginIncident.Should().BeFalse();
        d.NextPhase.Should().Be(StallPhase.Armed);
    }

    [Fact]
    public void Nineteen_seconds_does_not_hang()
    {
        var d = StallPolicy.Evaluate(Sample(age: 19));
        d.BeginIncident.Should().BeFalse();
        d.NextPhase.Should().Be(StallPhase.Armed);
    }

    [Fact]
    public void Twenty_seconds_armed_not_quitting_begins()
    {
        var d = StallPolicy.Evaluate(Sample(age: 20));
        d.BeginIncident.Should().BeTrue();
        d.NextPhase.Should().Be(StallPhase.Dumping);
    }

    [Fact]
    public void Definite_quit_does_not_begin()
    {
        var d = StallPolicy.Evaluate(Sample(age: 20, definiteQuit: true));
        d.BeginIncident.Should().BeFalse();
    }

    [Fact]
    public void Quit_request_before_three_heartbeats_does_not_begin()
    {
        var d = StallPolicy.Evaluate(Sample(age: 20, quitRequested: true, heartbeatsSinceQuit: 2));
        d.BeginIncident.Should().BeFalse();
    }

    [Fact]
    public void Dumping_does_not_begin_again_until_finished_then_disarms()
    {
        var waiting = StallPolicy.Evaluate(Sample(age: 30, phase: StallPhase.Dumping, dumpFinished: false));
        waiting.BeginIncident.Should().BeFalse();
        waiting.NextPhase.Should().Be(StallPhase.Dumping);

        var done = StallPolicy.Evaluate(Sample(age: 30, phase: StallPhase.Dumping, dumpFinished: true));
        done.BeginIncident.Should().BeFalse();
        done.NextPhase.Should().Be(StallPhase.Disarmed);
        done.NextHealthySeconds.Should().Be(0);
    }

    [Fact]
    public void Disarmed_rearms_only_from_policy_healthy_timer()
    {
        double healthy = 0;
        var phase = StallPhase.Disarmed;
        for (var i = 0; i < 59; i++)
        {
            var d = StallPolicy.Evaluate(Sample(age: 1, phase: phase, healthy: healthy));
            d.BeginIncident.Should().BeFalse();
            d.NextPhase.Should().Be(StallPhase.Disarmed);
            healthy = d.NextHealthySeconds;
            phase = d.NextPhase;
        }

        var armed = StallPolicy.Evaluate(Sample(age: 1, phase: phase, healthy: healthy));
        armed.BeginIncident.Should().BeFalse();
        armed.NextPhase.Should().Be(StallPhase.Armed);
    }

    [Fact]
    public void Healthy_timer_resets_when_age_reaches_three_seconds()
    {
        var d = StallPolicy.Evaluate(Sample(age: 3, phase: StallPhase.Disarmed, healthy: 40));
        d.NextPhase.Should().Be(StallPhase.Disarmed);
        d.NextHealthySeconds.Should().Be(0);
        d.BeginIncident.Should().BeFalse();
    }
}
