namespace MechabellumModManager.Models;

public sealed class PlannedCopy
{
    public string SourceAbsolute { get; init; } = "";
    public string DestAbsolute { get; init; } = "";
    public string RelativeGamePath { get; init; } = "";
    public string PackageId { get; init; } = "";
    public string Sha256 { get; init; } = "";
}

public sealed class DeployPlan
{
    public List<string> Deletes { get; init; } = new(); // absolute paths
    public List<PlannedCopy> Copies { get; init; } = new();
    public List<string> ConflictsUnmanaged { get; init; } = new(); // absolute
    public List<string> IntraProfilePathCollisions { get; init; } = new();
    public bool ManifestInvalidDueToGamePath { get; init; }
}
