using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class CatalogRoot
{
    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("mods")]
    public List<CatalogMod> Mods { get; set; } = new();
}

public sealed class CatalogMod
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("preview")]
    public string? Preview { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Optional per-language name/summary. Keys: zh-CN, en, de, ja, ru.
    /// Missing language or empty field falls back to top-level Name/Summary.
    /// </summary>
    [JsonPropertyName("locales")]
    public Dictionary<string, CatalogModLocale>? Locales { get; set; }
}

/// <summary>
/// Fetches Mod catalog and files from the independent MechabellumMods GitHub repo.
/// </summary>
public sealed class ModCatalogService
{
    public const string Owner = "llxlzx";
    public const string Repo = "MechabellumMods";
    public const string Branch = "master";

    public static readonly Uri CatalogUrl = new(
        $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/catalog.json");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    readonly HttpClient _http;

    public ModCatalogService(HttpClient? http = null)
    {
        _http = http ?? CreateDefaultClient();
    }

    public const long MaxDownloadBytes = 80L * 1024 * 1024;

    public static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MechabellumModManager-Catalog/1.0");
        return client;
    }

    /// <summary>
    /// Builds a raw.githubusercontent.com URL under Owner/Repo/Branch. Rejects absolute URLs and path traversal.
    /// </summary>
    public static string GetRawUrl(string relativePath)
    {
        var relative = NormalizeCatalogRelativePath(relativePath);
        return $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/{relative}";
    }

    public static string? TryGetRawUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;
        try
        {
            return GetRawUrl(relativePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static string NormalizeCatalogRelativePath(string? relativePath)
    {
        var relative = (relativePath ?? "").Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(relative))
            throw new ArgumentException("Catalog path is empty.", nameof(relativePath));

        if (relative.Contains("://", StringComparison.Ordinal) ||
            relative.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Catalog path must be relative to the mods repo.", nameof(relativePath));

        relative = relative.TrimStart('/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new ArgumentException("Catalog path is empty.", nameof(relativePath));

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
                throw new ArgumentException("Catalog path must not contain '.' or '..' segments.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    public Uri BuildFileUrl(CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return new Uri(GetRawUrl(mod.File ?? ""));
    }

    public static string? PreviewUrl(CatalogMod? mod)
    {
        if (mod is null || string.IsNullOrWhiteSpace(mod.Preview))
            return null;
        return TryGetRawUrl(mod.Preview);
    }

    public async Task<CatalogRoot> FetchCatalogAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(CatalogUrl, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var root = await JsonSerializer.DeserializeAsync<CatalogRoot>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
        return root ?? new CatalogRoot();
    }

    public static CatalogRoot DeserializeCatalog(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<CatalogRoot>(json, JsonOptions) ?? new CatalogRoot();
    }

    public async Task DownloadModAsync(CatalogMod mod, string destPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (string.IsNullOrWhiteSpace(destPath))
            throw new ArgumentException("Destination path is required.", nameof(destPath));

        var url = BuildFileUrl(mod);
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        if (resp.Content.Headers.ContentLength is long declared && declared > MaxDownloadBytes)
            throw new InvalidOperationException(
                $"Catalog download exceeds size limit ({MaxDownloadBytes} bytes).");

        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = File.Create(destPath);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read <= 0)
                break;
            total += read;
            if (total > MaxDownloadBytes)
            {
                await file.DisposeAsync().ConfigureAwait(false);
                try { File.Delete(destPath); } catch { /* best effort */ }
                throw new InvalidOperationException(
                    $"Catalog download exceeds size limit ({MaxDownloadBytes} bytes).");
            }

            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// True when any local package contains a file whose name equals the catalog entry's file name.
    /// </summary>
    public static bool IsInLibraryByFileName(IEnumerable<ModPackage> packages, string? catalogFile)
    {
        ArgumentNullException.ThrowIfNull(packages);
        var fileName = Path.GetFileName((catalogFile ?? "").Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        foreach (var pkg in packages)
        {
            foreach (var file in pkg.Files)
            {
                var localName = Path.GetFileName((file.RelativePathInPackage ?? "").Replace('\\', '/'));
                if (string.Equals(localName, fileName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public static ModPackageType ParsePackageType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return ModPackageType.MelonMod;

        return type.Trim() switch
        {
            "melon_mod" or "melonMod" or "MelonMod" => ModPackageType.MelonMod,
            "melon_plugin" or "melonPlugin" or "MelonPlugin" => ModPackageType.MelonPlugin,
            "melon_userlibs" or "melonUserLibs" or "MelonUserLibs" => ModPackageType.MelonUserLibs,
            "melon_userdata" or "melonUserData" or "MelonUserData" => ModPackageType.MelonUserData,
            _ => ModPackageType.MelonMod
        };
    }
}
