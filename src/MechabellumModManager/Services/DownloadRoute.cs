using System.IO;
using System.Net.Http;

namespace MechabellumModManager.Services;

/// <summary>Which hosts a download or update check may contact.</summary>
public enum DownloadRouteKind
{
    /// <summary>Domestic mirror first. GitHub is used only when the mirror does not answer.</summary>
    MirrorFirst,

    /// <summary>GitHub only. The domestic mirror is omitted so it is not billed.</summary>
    GitHubOnly
}

/// <summary>Player override stored on <see cref="Models.AppConfig"/>.</summary>
public enum DownloadRoutePreference
{
    Auto = 0,
    AlwaysMirror = 1,
    AlwaysGitHub = 2
}

public sealed class DownloadRouteOption
{
    public DownloadRouteOption(int value, string label)
    {
        Value = value;
        Label = label;
    }

    public int Value { get; }
    public string Label { get; }
}

/// <summary>
/// Picks a route before any mirror body is downloaded.
/// A confident non-mainland answer is remembered for a day. A timeout, a failure, or an
/// unreadable answer stays on the mirror and is not remembered, so a blip cannot lock
/// mainland players onto GitHub.
/// </summary>
public static class DownloadRoutePolicy
{
    public const string CacheFileName = "download-route.txt";
    public static readonly TimeSpan RememberNonMainlandFor = TimeSpan.FromHours(24);
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    public static readonly Uri ProbeUri = new("https://www.cloudflare.com/cdn-cgi/trace");

    public static bool TryParseCountry(string? body, out string country)
    {
        country = "";
        if (string.IsNullOrEmpty(body))
            return false;

        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("loc=", StringComparison.OrdinalIgnoreCase))
                continue;
            country = line[4..].Trim();
            return country.Length == 2 && country.All(char.IsLetter);
        }

        return false;
    }

    public static bool IsMainlandChina(string country) =>
        string.Equals(country, "CN", StringComparison.OrdinalIgnoreCase);

    public static string CachePath(string dataRoot) =>
        Path.Combine(dataRoot, CacheFileName);

    public static bool TryReadNonMainland(string? dataRoot, DateTimeOffset now, out string country)
    {
        country = "";
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;

        var path = CachePath(dataRoot);
        if (!File.Exists(path))
            return false;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            return false;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2 || !TryParseCountry("loc=" + lines[0], out country))
            return false;
        if (IsMainlandChina(country))
            return false;
        if (!DateTimeOffset.TryParse(lines[1], out var at))
            return false;
        var age = now - at;
        return age >= TimeSpan.Zero && age < RememberNonMainlandFor;
    }

    public static void RememberNonMainland(string dataRoot, string country, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(dataRoot) || IsMainlandChina(country))
            return;
        if (!TryParseCountry("loc=" + country, out var parsed))
            return;

        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(
            CachePath(dataRoot),
            parsed.ToUpperInvariant() + "\n" + now.ToString("o"));
    }

    public static DownloadRouteKind ResolveAuto(string? dataRoot)
    {
        using var http = new HttpClient { Timeout = ProbeTimeout + TimeSpan.FromSeconds(1) };
        return ResolveAsync(DownloadRoutePreference.Auto, dataRoot, http, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task<DownloadRouteKind> ResolveAsync(
        DownloadRoutePreference preference,
        string? dataRoot,
        HttpClient http,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(http);

        if (preference == DownloadRoutePreference.AlwaysMirror)
            return DownloadRouteKind.MirrorFirst;
        if (preference == DownloadRoutePreference.AlwaysGitHub)
            return DownloadRouteKind.GitHubOnly;

        if (TryReadNonMainland(dataRoot, DateTimeOffset.UtcNow, out _))
            return DownloadRouteKind.GitHubOnly;

        string? body = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, ProbeUri);
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return DownloadRouteKind.MirrorFirst;
            body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            return DownloadRouteKind.MirrorFirst;
        }

        if (!TryParseCountry(body, out var country) || IsMainlandChina(country))
            return DownloadRouteKind.MirrorFirst;

        if (!string.IsNullOrWhiteSpace(dataRoot))
            RememberNonMainland(dataRoot, country, DateTimeOffset.UtcNow);
        return DownloadRouteKind.GitHubOnly;
    }
}
