namespace MechabellumModManager.Models;

public sealed class DeployableFile
{
    public string RelativePathInPackage { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed class ModPackage
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Version { get; set; }
    public string? Author { get; set; }
    public ModPackageType Type { get; set; }
    public bool HighRisk { get; set; }
    public string? RequiredMelonLoaderVersion { get; set; }
    public List<DeployableFile> Files { get; set; } = new();
    public string PackageDirectory { get; set; } = ""; // absolute under library
}
