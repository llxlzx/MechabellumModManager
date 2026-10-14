using System.Globalization;
using System.Text.Json.Serialization;
using MechabellumModManager.Services;

namespace MechabellumModManager.Models;

/// <summary>
/// Signed bytes and their signature. <see cref="Payload"/> is base64 of the exact UTF-8 JSON that
/// was signed, so no JSON canonicalisation is needed to verify it.
/// </summary>
public sealed class CatalogSignedBlob
{
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}

/// <summary>
/// A mod the maintainer has taken down. Library packages that match it are removed from the
/// player's machine. Only ever read from a verified <see cref="CatalogSignedBlob"/> payload.
/// </summary>
public sealed class CatalogRemoval
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Hashes of every version that was ever published, so renamed or re-imported copies still match.</summary>
    [JsonPropertyName("sha256")]
    public List<string>? Sha256 { get; set; }

    /// <summary>Default (Simplified Chinese) explanation shown to the player.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("since")]
    public string? Since { get; set; }

    /// <summary>Per-language reason overrides. Keys match catalog locales: en, de, ja, ru.</summary>
    [JsonPropertyName("locales")]
    public Dictionary<string, CatalogRemovalLocale>? Locales { get; set; }

    public string? ResolveReason(string? culture = null)
    {
        if (Locales is { Count: > 0 })
        {
            var key = LocalizationService.ResolveConfiguredLanguage(
                string.IsNullOrWhiteSpace(culture) ? CultureInfo.CurrentUICulture.Name : culture);
            foreach (var pair in Locales)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(pair.Value?.Reason))
                    return pair.Value.Reason.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();
    }
}

public sealed class CatalogRemovalLocale
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
