namespace MechabellumModManager.Models;

public sealed class CriticalOpMarker
{
    public CriticalOpKind Kind { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public string Detail { get; set; } = "";
    public int Pid { get; set; }
}
