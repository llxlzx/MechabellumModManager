namespace BlackBox.Core;

public static class StallPhaseCodes
{
    public const int Armed = 0;
    public const int Dumping = 1;
    public const int Disarmed = 2;
}

public static class IncidentGate
{
    public static bool TryBegin(ref int phase) =>
        Interlocked.CompareExchange(ref phase, StallPhaseCodes.Dumping, StallPhaseCodes.Armed)
        == StallPhaseCodes.Armed;
}
