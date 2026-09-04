using System.Text.Json.Serialization;

namespace MechabellumModManager.Models;

/// <summary>Per-language name/summary override in catalog.json locales.</summary>
public sealed class CatalogModLocale
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
