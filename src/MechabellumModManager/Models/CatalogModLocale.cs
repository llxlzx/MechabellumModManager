using System.Text.Json.Serialization;

namespace MechabellumModManager.Models;

/// <summary>Per-language name, summary, and preview override in catalog.json locales.</summary>
public sealed class CatalogModLocale
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>Catalog-relative preview. Empty falls back to the mod's default preview.</summary>
    [JsonPropertyName("preview")]
    public string? Preview { get; set; }
}
