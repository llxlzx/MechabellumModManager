using System.Text.Json.Serialization;

namespace MechabellumModManager.Models;

public sealed class RedistManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("melonTag")]
    public string? MelonTag { get; set; }

    [JsonPropertyName("artifacts")]
    public List<RedistArtifact> Artifacts { get; set; } = [];
}

public sealed class RedistArtifact
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>Relative path under installer-redist / MechabellumRedist.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("originUrl")]
    public string? OriginUrl { get; set; }
}
