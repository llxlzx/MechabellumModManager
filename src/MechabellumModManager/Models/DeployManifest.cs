namespace MechabellumModManager.Models;

public sealed class ManifestFileEntry
{
    public string RelativePath { get; set; } = ""; // relative GameRoot
    public string PackageId { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class DeployManifest
{
    public string GamePath { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public List<ManifestFileEntry> Files { get; set; } = new();
}
