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
    /// <summary>Local override of catalog category (writable enum string); null = follow catalog.</summary>
    public string? CategoryOverride { get; set; }
    /// <summary>Local extra tags merged with catalog tags.</summary>
    public List<string>? ExtraTags { get; set; }
    /// <summary>Runtime-only catalog category from last enrichment.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CatalogCategory { get; set; }
    /// <summary>Runtime-only catalog tags from last enrichment.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string>? CatalogTags { get; set; }
    /// <summary>Runtime-only default catalog name (top-level name) from last enrichment.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? CatalogDisplayName { get; set; }
    /// <summary>Runtime-only catalog locales from last enrichment (for localized display).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, CatalogModLocale>? CatalogLocales { get; set; }
    public List<DeployableFile> Files { get; set; } = new();
    public string PackageDirectory { get; set; } = ""; // absolute under library
}
