using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechabellumModManager.Services;

public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("setupUrl")]
    public string SetupUrl { get; set; } = "";

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }
}

public enum UpdateCheckKind
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckKind Kind,
    string LocalVersion,
    string? RemoteVersion,
    string? Notes,
    string? SetupUrl,
    string Message);

/// <summary>
/// Checks GitHub Releases for a newer Setup via latest.json (with API fallback).
/// Does not download or install — UI opens the URL for the user.
/// </summary>
public sealed class UpdateChecker
{
    public const string Owner = "llxlzx";
    public const string Repo = "MechabellumModManager";

    public static readonly Uri LatestJsonUri = new(
        $"https://github.com/{Owner}/{Repo}/releases/latest/download/latest.json");

    public static readonly Uri LatestApiUri = new(
        $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    readonly HttpClient _http;
    readonly Func<string> _localVersionProvider;

    public UpdateChecker(HttpClient? http = null, Func<string>? localVersionProvider = null)
    {
        _http = http ?? CreateDefaultClient();
        _localVersionProvider = localVersionProvider ?? ReadLocalVersion;
    }

    public static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MechabellumModManager-UpdateCheck/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public static string ReadLocalVersion()
    {
        var asm = typeof(UpdateChecker).Assembly;
        var info = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip any "+gitsha" suffix from Source Link / InformationalVersion
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var local = NormalizeVersion(_localVersionProvider()) ?? "0.0.0";
        try
        {
            var manifest = await TryFetchLatestJsonAsync(ct).ConfigureAwait(false)
                           ?? await TryFetchFromApiAsync(ct).ConfigureAwait(false);

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return new UpdateCheckResult(
                    UpdateCheckKind.Failed, local, null, null, null,
                    "无法获取更新信息。请检查网络，或稍后打开 GitHub Releases 页面。");
            }

            var remote = NormalizeVersion(manifest.Version) ?? manifest.Version.Trim();
            if (IsNewer(remote, local))
            {
                var setup = string.IsNullOrWhiteSpace(manifest.SetupUrl)
                    ? $"https://github.com/{Owner}/{Repo}/releases/latest"
                    : manifest.SetupUrl.Trim();
                var notes = string.IsNullOrWhiteSpace(manifest.Notes) ? "（无更新说明）" : manifest.Notes.Trim();
                return new UpdateCheckResult(
                    UpdateCheckKind.UpdateAvailable, local, remote, notes, setup,
                    $"发现新版本 {remote}（当前 {local}）。");
            }

            return new UpdateCheckResult(
                UpdateCheckKind.UpToDate, local, remote, manifest.Notes, manifest.SetupUrl,
                $"已是最新版本（{local}）。");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                UpdateCheckKind.Failed, local, null, null, null,
                $"检查更新失败：{ex.Message}");
        }
    }

    async Task<UpdateManifest?> TryFetchLatestJsonAsync(CancellationToken ct)
    {
        using var resp = await _http.GetAsync(LatestJsonUri, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, ct).ConfigureAwait(false);
    }

    async Task<UpdateManifest?> TryFetchFromApiAsync(CancellationToken ct)
    {
        using var resp = await _http.GetAsync(LatestApiUri, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
        var html = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
        string? setupUrl = html;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is null) continue;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains("Setup", StringComparison.OrdinalIgnoreCase) &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    setupUrl = url.GetString() ?? setupUrl;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(tag)) return null;
        return new UpdateManifest
        {
            Version = tag.TrimStart('v', 'V'),
            Notes = body ?? "",
            SetupUrl = setupUrl ?? $"https://github.com/{Owner}/{Repo}/releases/latest"
        };
    }

    /// <summary>True if remote is strictly newer than local.</summary>
    public static bool IsNewer(string remote, string local)
    {
        var r = NormalizeVersion(remote);
        var l = NormalizeVersion(local);
        if (r is null || l is null) return false;
        if (!Version.TryParse(Pad(r), out var rv)) return false;
        if (!Version.TryParse(Pad(l), out var lv)) return false;
        return rv > lv;
    }

    public static string? NormalizeVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s[1..];
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        // Keep only digits and dots for TryParse friendliness
        var cleaned = new string(s.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned.Trim('.');
    }

    static string Pad(string v)
    {
        // Version.TryParse needs at least Major.Minor
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0] + ".0";
        return v;
    }
}
