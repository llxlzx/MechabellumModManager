using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MechabellumModManager.Services;

public sealed record NoticeLine(string TimeText, string Title, string Body);

public sealed class NoticeLoadResult
{
    public bool Failed { get; init; }
    public bool FromCache { get; init; }
    public IReadOnlyList<NoticeLine> Items { get; init; } = [];
}

/// <summary>
/// Loads notice.json when the notice page opens. The domestic mirror is tried first.
/// A failed load keeps the previous cache and does not replace it.
/// </summary>
public sealed class NoticeService
{
    public const int MaxBytes = 1_048_576;
    public const int MaxItems = 30;
    public const string CacheFileName = "notice-cache.json";

    public static readonly Uri GitHubNoticeUri = new(
        "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/notice.json");

    readonly HttpClient _http;

    public string? MirrorBaseUrl { get; set; }

    /// <summary>Domestic installs try the mirror first. A GitHub-only route omits the mirror.</summary>
    public DownloadRouteKind Route { get; set; } = DownloadRouteKind.MirrorFirst;
    public string? DataRoot { get; set; }
    public Func<string>? Language { get; set; }

    public NoticeService(HttpClient? http = null, string? dataRoot = null, string? mirrorBaseUrl = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        DataRoot = dataRoot;
        MirrorBaseUrl = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? null : mirrorBaseUrl.Trim().TrimEnd('/');
    }

    public async Task<NoticeLoadResult> LoadAsync(CancellationToken ct = default)
    {
        foreach (var uri in Candidates())
        {
            ct.ThrowIfCancellationRequested();
            byte[]? bytes;
            try
            {
                bytes = await ReadAsync(uri, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                continue;
            }

            if (bytes is null || bytes.Length > MaxBytes)
                continue;
            if (!TryParse(Encoding.UTF8.GetString(bytes), CurrentLanguage(), out var items))
                continue;
            TryWriteCache(bytes);
            return new NoticeLoadResult { Items = items };
        }

        if (TryReadCache(out var cached) &&
            TryParse(cached, CurrentLanguage(), out var stale))
            return new NoticeLoadResult { Failed = true, FromCache = true, Items = stale };

        return new NoticeLoadResult { Failed = true };
    }

    public static bool TryParse(string json, string language, out IReadOnlyList<NoticeLine> items)
    {
        items = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("items", out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return false;

            var parsed = new List<NoticeLine>();
            foreach (var element in array.EnumerateArray())
            {
                if (parsed.Count >= MaxItems)
                    break;
                if (element.ValueKind != JsonValueKind.Object)
                    continue;
                if (!TryLine(element, language, out var line))
                    continue;
                parsed.Add(line);
            }

            items = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    IEnumerable<Uri> Candidates()
    {
        if (Route != DownloadRouteKind.GitHubOnly &&
            !string.IsNullOrWhiteSpace(MirrorBaseUrl) &&
            Uri.TryCreate(MirrorBaseUrl.Trim().TrimEnd('/') + "/MechabellumMods/notice.json", UriKind.Absolute, out var mirror))
            yield return mirror;
        yield return GitHubNoticeUri;
    }

    async Task<byte[]?> ReadAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
            return null;
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    string CurrentLanguage() =>
        Language?.Invoke() ?? LocalizationService.ResolveConfiguredLanguage(CultureInfo.CurrentUICulture.Name);

    void TryWriteCache(byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(DataRoot))
            return;
        try
        {
            Directory.CreateDirectory(DataRoot);
            var path = Path.Combine(DataRoot, CacheFileName);
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Copy(tmp, path, overwrite: true);
            try { File.Delete(tmp); } catch { /* best effort */ }
        }
        catch
        {
            // A failed cache write still shows the fresh notice.
        }
    }

    bool TryReadCache(out string json)
    {
        json = "";
        if (string.IsNullOrWhiteSpace(DataRoot))
            return false;
        var path = Path.Combine(DataRoot, CacheFileName);
        if (!File.Exists(path))
            return false;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
            return !string.IsNullOrWhiteSpace(json);
        }
        catch
        {
            json = "";
            return false;
        }
    }

    static bool TryLine(JsonElement element, string language, out NoticeLine line)
    {
        line = new NoticeLine("", "", "");
        var title = ReadString(element, "title");
        var body = ReadString(element, "body");
        if (!string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase) &&
            element.TryGetProperty("locales", out var locales) &&
            locales.ValueKind == JsonValueKind.Object)
        {
            foreach (var pair in locales.EnumerateObject())
            {
                if (!string.Equals(pair.Name, language, StringComparison.OrdinalIgnoreCase) ||
                    pair.Value.ValueKind != JsonValueKind.Object)
                    continue;
                var localizedTitle = ReadString(pair.Value, "title");
                var localizedBody = ReadString(pair.Value, "body");
                if (!string.IsNullOrWhiteSpace(localizedTitle))
                    title = localizedTitle;
                if (!string.IsNullOrWhiteSpace(localizedBody))
                    body = localizedBody;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            return false;

        line = new NoticeLine(FormatTime(ReadString(element, "time")), title?.Trim() ?? "", body?.Trim() ?? "");
        return true;
    }

    static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        return value.GetString();
    }

    static string FormatTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        if (DateTimeOffset.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return raw.Trim();
    }
}
