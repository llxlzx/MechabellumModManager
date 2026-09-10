using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
    string Message,
    string? Source = null);

/// <summary>
/// Checks for a newer Setup via latest.json (with API fallback).
/// Does not download or install — UI opens the URL for the user.
/// </summary>
public sealed class UpdateChecker
{
    public const string Owner = "llxlzx";
    public const string Repo = "MechabellumModManager";
    public const string Branch = "master";

    public static readonly Uri LatestJsonUri = new(
        $"https://github.com/{Owner}/{Repo}/releases/latest/download/latest.json");

    /// <summary>
    /// Repo-tree pointer to the current release manifest, so a version is discoverable from the repo
    /// alone — no Release asset listing and no mirror sync required to find it. It only carries the
    /// announcement: the Setup still has to be reachable over https, which is why the release process
    /// pushes this file last, once the download it names is live.
    /// </summary>
    public static readonly Uri RawLatestJsonUri = new(
        $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/release/latest.json");

    public static readonly Uri LatestApiUri = new(
        $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    readonly HttpClient _http;
    readonly Func<string> _localVersionProvider;

    public string? MirrorBaseUrl { get; set; }

    public UpdateChecker(HttpClient? http = null, Func<string>? localVersionProvider = null, string? mirrorBaseUrl = null)
    {
        _http = http ?? CreateDefaultClient();
        _localVersionProvider = localVersionProvider ?? ReadLocalVersion;
        MirrorBaseUrl = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? null : mirrorBaseUrl.Trim().TrimEnd('/');
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
        string? source = null;
        try
        {
            var fetched = await TryFetchLatestJsonAsync(ct).ConfigureAwait(false);
            var manifest = fetched.Manifest;
            source = fetched.Source;
            if (manifest is null)
            {
                manifest = await TryFetchFromApiAsync(ct).ConfigureAwait(false);
                if (manifest is not null)
                    source = RemoteFetch.GithubSource;
            }

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return new UpdateCheckResult(
                    UpdateCheckKind.Failed, local, null, null, null,
                    "无法获取更新信息。已尝试国内镜像与 GitHub。",
                    source);
            }

            var remote = NormalizeVersion(manifest.Version) ?? manifest.Version.Trim();

            // A manifest is remote input and its URL is handed to the shell. Sanitized once, before
            // the branch, so no result carries an unchecked URL: the generic external URL gate also
            // allows mailto:, which has no business being an installer link.
            var setup = ExternalUrlPolicy.IsHttpsUrl(manifest.SetupUrl)
                ? manifest.SetupUrl!.Trim()
                : $"https://github.com/{Owner}/{Repo}/releases/latest";

            if (IsNewer(remote, local))
            {
                var notes = string.IsNullOrWhiteSpace(manifest.Notes) ? "（无更新说明）" : manifest.Notes.Trim();
                return new UpdateCheckResult(
                    UpdateCheckKind.UpdateAvailable, local, remote, notes, setup,
                    $"发现新版本 {remote}（当前 {local}）。",
                    source);
            }

            return new UpdateCheckResult(
                UpdateCheckKind.UpToDate, local, remote, manifest.Notes, setup,
                $"已是最新版本（{local}）。",
                source);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                UpdateCheckKind.Failed, local, null, null, null,
                $"检查更新失败：{ex.Message}");
        }
    }

    public IReadOnlyList<Uri> BuildLatestJsonCandidates()
    {
        var candidates = new List<Uri>(3);
        var mirror = RemoteFetch.TryMirrorUri(MirrorBaseUrl, "MechabellumModManager/latest.json");
        if (mirror is not null)
            candidates.Add(mirror);
        candidates.Add(LatestJsonUri);
        candidates.Add(RawLatestJsonUri);
        return candidates;
    }

    /// <summary>
    /// Reads every manifest source in parallel and keeps the one announcing the newest release. Taking
    /// the first success would let a mirror that has not synced, or a Release that is not published
    /// yet, decide what the player is told.
    /// </summary>
    async Task<(UpdateManifest? Manifest, string? Source)> TryFetchLatestJsonAsync(CancellationToken ct)
    {
        IReadOnlyList<RemoteFetchResult> fetched;
        try
        {
            fetched = await RemoteFetch.GetAllAsync(_http, BuildLatestJsonCandidates(), ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return (null, null);
        }

        var copies = new List<(Uri Used, UpdateManifest Manifest)>(fetched.Count);
        try
        {
            foreach (var result in fetched)
            {
                try
                {
                    await using var stream = await result.Response.Content.ReadAsStreamAsync(ct)
                        .ConfigureAwait(false);
                    var manifest = await JsonSerializer
                        .DeserializeAsync<UpdateManifest>(stream, JsonOptions, ct)
                        .ConfigureAwait(false);
                    // An unparsable version has to be dropped, not merely skipped by IsNewer: that
                    // returns false in both directions, which would let the publishedAt tie-break
                    // hand the decision to a manifest nobody can compare.
                    if (manifest is not null && IsComparableVersion(manifest.Version))
                        copies.Add((result.Used, manifest));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // An unparsable copy loses to whichever source answered with a usable manifest.
                }
            }
        }
        finally
        {
            foreach (var result in fetched)
                result.Dispose();
        }

        if (copies.Count == 0)
            return (null, null);

        var best = 0;
        for (var i = 1; i < copies.Count; i++)
        {
            if (AnnouncesNewerThan(copies[i].Manifest, copies[best].Manifest))
                best = i;
        }

        return (copies[best].Manifest, RemoteFetch.ClassifySource(copies[best].Used));
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> announces a newer release than <paramref name="incumbent"/>.
    /// Equal versions keep the incumbent so the earlier candidate (mirror, then Release, then repo
    /// pointer) wins a tie and domestic players keep the mirrored download. A publishedAt stamp only
    /// breaks a tie when both sides carry one, so a manifest that omits the field cannot lose by it.
    /// </summary>
    static bool AnnouncesNewerThan(UpdateManifest candidate, UpdateManifest incumbent)
    {
        if (IsNewer(candidate.Version, incumbent.Version))
            return true;
        if (IsNewer(incumbent.Version, candidate.Version))
            return false;

        return ParseStamp(candidate.PublishedAt) is { } candidateStamp
               && ParseStamp(incumbent.PublishedAt) is { } incumbentStamp
               && candidateStamp > incumbentStamp;
    }

    static DateTimeOffset? ParseStamp(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    static readonly Regex DottedNumber = new(
        @"^\d+(\.\d+){0,3}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// True when <see cref="IsNewer"/> can meaningfully order this version against another.
    /// Deliberately checks the raw string rather than the normalized one: <see cref="NormalizeVersion"/>
    /// keeps only digits and dots, so "nightly9999" would arrive as a perfectly orderable 9999 and beat
    /// every real release, and "1.2.0-rc.1" as 1.2.0.1 would beat stable 1.2.0.
    /// </summary>
    public static bool IsComparableVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];
        var plus = value.IndexOf('+');
        if (plus >= 0)
            value = value[..plus];

        return DottedNumber.IsMatch(value) && Version.TryParse(Pad(value), out _);
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
