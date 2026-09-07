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

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

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

    public string? MirrorBaseUrl { get; set; }
    public string? DataRoot { get; set; }
    public string? LastFetchSource { get; private set; }
    public string? LastDownloadSource { get; private set; }

    public ModCatalogService(HttpClient? http = null, string? mirrorBaseUrl = null, string? dataRoot = null)
    {
        _http = http ?? CreateDefaultClient();
        MirrorBaseUrl = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? null : mirrorBaseUrl.Trim().TrimEnd('/');
        DataRoot = dataRoot;
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

    public static string? PreviewUrl(CatalogMod? mod, string? mirrorBaseUrl = null)
    {
        var urls = GetPreviewCandidateUrls(mod, mirrorBaseUrl);
        return urls.Count == 0 ? null : urls[0];
    }

    public static IReadOnlyList<string> GetPreviewCandidateUrls(CatalogMod? mod, string? mirrorBaseUrl = null)
    {
        if (mod is null || string.IsNullOrWhiteSpace(mod.Preview))
            return Array.Empty<string>();

        try
        {
            var relative = NormalizeCatalogRelativePath(mod.Preview);
            return RemoteFetch.BuildCandidates(
                    mirrorBaseUrl,
                    $"MechabellumMods/{relative}",
                    new Uri(GetRawUrl(relative)))
                .Select(u => u.ToString())
                .ToList();
        }
        catch (ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<Uri> BuildCatalogCandidates() =>
        RemoteFetch.BuildCandidates(MirrorBaseUrl, "MechabellumMods/catalog.json", CatalogUrl);

    public IReadOnlyList<Uri> BuildFileCandidates(string relativePath)
    {
        var relative = NormalizeCatalogRelativePath(relativePath);
        return RemoteFetch.BuildCandidates(
            MirrorBaseUrl,
            $"MechabellumMods/{relative}",
            new Uri(GetRawUrl(relative)));
    }

    public async Task<CatalogRoot> FetchCatalogAsync(CancellationToken ct = default)
    {
        using var fetched = await RemoteFetch.GetAsync(_http, BuildCatalogCandidates(), ct).ConfigureAwait(false);
        LastFetchSource = RemoteFetch.ClassifySource(fetched.Used);
        var json = await fetched.Response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(DataRoot))
            CatalogCache.Write(DataRoot, json);
        return DeserializeCatalog(json);
    }

    public CatalogRoot? TryLoadCachedCatalog()
    {
        if (string.IsNullOrWhiteSpace(DataRoot))
            return null;
        if (!CatalogCache.TryRead(DataRoot, out var json))
            return null;
        try
        {
            LastFetchSource = "cache";
            return DeserializeCatalog(json);
        }
        catch
        {
            return null;
        }
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

        using var fetched = await RemoteFetch.GetAsync(
                _http,
                BuildFileCandidates(mod.File ?? ""),
                HttpCompletionOption.ResponseHeadersRead,
                ct)
            .ConfigureAwait(false);
        LastDownloadSource = RemoteFetch.ClassifySource(fetched.Used);
        var resp = fetched.Response;

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
    /// True when a library package matches the catalog entry by catalog id or file hash.
    /// Filename-only matches are ignored so two mods shipping <c>Mod.dll</c> do not collide.
    /// </summary>
    public static bool IsInLibrary(IEnumerable<ModPackage> packages, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(mod);

        var catalogId = (mod.Id ?? "").Trim();
        foreach (var pkg in packages)
        {
            if (!string.IsNullOrWhiteSpace(catalogId))
            {
                if (string.Equals(pkg.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(pkg.Id, catalogId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (HasMatchingFileHash(pkg, mod))
                return true;
        }

        return false;
    }

    /// <summary>Obsolete filename matcher kept for existing call-site migration.</summary>
    public static bool IsInLibraryByFileName(IEnumerable<ModPackage> packages, string? catalogFile)
    {
        ArgumentNullException.ThrowIfNull(packages);
        return false;
    }

    static bool HasMatchingFileHash(ModPackage pkg, CatalogMod mod)
    {
        var expected = (mod.Sha256 ?? "").Trim();
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        foreach (var file in pkg.Files)
        {
            if (string.Equals(file.Sha256, expected, StringComparison.OrdinalIgnoreCase))
                return true;
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
