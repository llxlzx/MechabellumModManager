using BlackBox.Core;
using FluentAssertions;

public class BlackBoxIncidentGateTests
{
    [Fact]
    public void TryBegin_allows_one_caller()
    {
        var phase = StallPhaseCodes.Armed;
        IncidentGate.TryBegin(ref phase).Should().BeTrue();
        phase.Should().Be(StallPhaseCodes.Dumping);
        IncidentGate.TryBegin(ref phase).Should().BeFalse();
    }

    [Fact]
    public void TryBegin_fails_while_disarmed_and_succeeds_again_after_rearm()
    {
        var phase = StallPhaseCodes.Disarmed;
        IncidentGate.TryBegin(ref phase).Should().BeFalse();
        phase = StallPhaseCodes.Armed;
        IncidentGate.TryBegin(ref phase).Should().BeTrue();
    }

    [Fact]
    public void Three_heartbeats_clear_a_quit_request_and_a_later_hang_can_begin()
    {
        var quit = new QuitRequestState();
        quit.OnQuitRequested();
        quit.IsExiting.Should().BeTrue();
        quit.OnHeartbeatWritten();
        quit.OnHeartbeatWritten();
        quit.IsExiting.Should().BeTrue();
        quit.OnHeartbeatWritten();
        quit.IsExiting.Should().BeFalse();

        var d = StallPolicy.Evaluate(new StallInput(
            true, 20, quit.DefiniteQuit, quit.Requested, quit.HeartbeatsSinceRequest,
            StallPhase.Armed, 0, false));
        d.BeginIncident.Should().BeTrue();
    }

    [Fact]
    public void Definite_quit_stays_exiting()
    {
        var quit = new QuitRequestState();
        quit.OnDefiniteQuit();
        quit.OnHeartbeatWritten();
        quit.OnHeartbeatWritten();
        quit.OnHeartbeatWritten();
        quit.IsExiting.Should().BeTrue();
    }
}
