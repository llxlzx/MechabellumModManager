namespace BlackBox.Core;

public enum StallPhase
{
    Armed,
    Dumping,
    Disarmed
}

public readonly record struct StallInput(
    bool HasHeartbeat,
    double HeartbeatAgeSeconds,
    bool DefiniteQuit,
    bool QuitRequested,
    int HeartbeatsSinceQuitRequest,
    StallPhase Phase,
    double HealthySeconds,
    bool DumpFinished);

public readonly record struct StallDecision(
    bool BeginIncident,
    StallPhase NextPhase,
    double NextHealthySeconds);

public static class StallPolicy
{
    public const double HangSeconds = 20;
    public const double HealthyBeatSeconds = 3;
    public const double RearmSeconds = 60;
    public const int QuitCancelHeartbeats = 3;

    public static StallDecision Evaluate(StallInput input)
    {
        var exiting = input.DefiniteQuit
            || (input.QuitRequested && input.HeartbeatsSinceQuitRequest < QuitCancelHeartbeats);

        if (input.Phase == StallPhase.Armed)
        {
            if (!input.HasHeartbeat || exiting || input.HeartbeatAgeSeconds < HangSeconds)
                return new StallDecision(false, StallPhase.Armed, input.HealthySeconds);
            return new StallDecision(true, StallPhase.Dumping, input.HealthySeconds);
        }

        if (input.Phase == StallPhase.Dumping)
        {
            if (!input.DumpFinished)
                return new StallDecision(false, StallPhase.Dumping, input.HealthySeconds);
            return new StallDecision(false, StallPhase.Disarmed, 0);
        }

        if (input.HeartbeatAgeSeconds >= HealthyBeatSeconds)
            return new StallDecision(false, StallPhase.Disarmed, 0);

        var nextHealthy = input.HealthySeconds + 1;
        if (nextHealthy >= RearmSeconds)
            return new StallDecision(false, StallPhase.Armed, 0);
        return new StallDecision(false, StallPhase.Disarmed, nextHealthy);
    }
}
