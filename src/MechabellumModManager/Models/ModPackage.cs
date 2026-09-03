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
    public string? Summary { get; set; }
    public string? CatalogUpdatedAt { get; set; }
    /// <summary>Relative path in MechabellumMods repo, e.g. mods/show-grid/preview.png.</summary>
    public string? Preview { get; set; }
    public List<DeployableFile> Files { get; set; } = new();
    public string PackageDirectory { get; set; } = ""; // absolute under library
}
