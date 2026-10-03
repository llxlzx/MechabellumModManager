using System.Text.Json.Serialization;

namespace MechabellumModManager.Models;

/// <summary>Another catalog mod that must not be installed at the same time.</summary>
public sealed class CatalogConflict
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Default (Simplified Chinese) explanation. Empty falls back to a generic sentence.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Per-language reason overrides. Keys match catalog locales: en, de, ja, ru.</summary>
    [JsonPropertyName("locales")]
    public Dictionary<string, CatalogConflictLocale>? Locales { get; set; }
}

public sealed class CatalogConflictLocale
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
