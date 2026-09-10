using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Bytes fetched so far against the catalog-declared total. <see cref="TotalBytes"/> is 0 when
/// the catalog predates the "size" field and the server sent no Content-Length.
/// </summary>
public readonly record struct DownloadProgress(long BytesDownloaded, long TotalBytes)
{
    public double? Fraction =>
        TotalBytes > 0 ? Math.Clamp((double)BytesDownloaded / TotalBytes, 0, 1) : null;
}

/// <summary>Whether a catalog entry is missing locally, current, or superseded by a newer catalog copy.</summary>
public enum CatalogEntryState
{
    NotInstalled,
    UpToDate,
    UpdateAvailable
}

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

    /// <summary>
    /// Byte size of <see cref="File"/>. Drives the progress total and lets the downloader
    /// reject an over-long response early, since a resumed or mirror-served response is not
    /// guaranteed to carry Content-Length. 0 means the catalog predates the field.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Absolute https URL of the full-size copy, used when the binary is too large to live in
    /// the git repo (GitHub rejects pushes over 100 MB per file). Falls back to the
    /// raw.githubusercontent.com path built from <see cref="File"/> when absent.
    /// </summary>
    [JsonPropertyName("originUrl")]
    public string? OriginUrl { get; set; }

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

    /// <summary>
    /// Source that answered 200 with an older catalog than the copy that won, i.e. a mirror that
    /// has not finished syncing. Null when every reachable copy was equally fresh. Reset per fetch.
    /// </summary>
    public string? LastStaleSource { get; private set; }

    /// <summary>
    /// The non-mirror catalog URL. Overridable for the same reason loopback http is accepted for
    /// mod downloads: so the real <see cref="CreateDefaultClient"/> path can be exercised end to
    /// end against a local server instead of the live repo.
    /// </summary>
    public Uri CatalogOriginUrl { get; set; } = CatalogUrl;

    public ModCatalogService(HttpClient? http = null, string? mirrorBaseUrl = null, string? dataRoot = null)
    {
        _http = http ?? CreateDefaultClient();
        MirrorBaseUrl = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? null : mirrorBaseUrl.Trim().TrimEnd('/');
        DataRoot = dataRoot;
    }

    /// <summary>
    /// Backstop for catalog entries written before "size" existed. A sized entry is bounded by
    /// its own declared size instead, so this never applies to a current catalog.
    /// </summary>
    public const long AbsoluteMaxDownloadBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>
    /// Longest gap tolerated between two received chunks, and the only liveness guard the
    /// download body has. A total-duration timeout cannot serve that role: it cannot tell
    /// "the link is dead" from "the file is simply large".
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(60);

    const int AttemptsPerCandidate = 3;

    public static HttpClient CreateDefaultClient()
    {
        // Transparent gzip matters for catalog.json, which is one object that grows with the
        // whole catalog and compresses roughly tenfold. It is safe for mod downloads too: when
        // the handler decompresses, it drops Content-Length, and the size checks below already
        // treat an absent length as "unknown" and fall back to counting the bytes written.
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        // HttpClient.Timeout stops applying once the response headers arrive, so it never bounded
        // the mod download body (which reads headers-first) but does bound a buffered catalog
        // read. A finite value there would cap catalog size for no good reason, and leaving the
        // body unguarded is not the alternative: IdleTimeout covers it, per-read.
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
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
        RemoteFetch.BuildCandidates(MirrorBaseUrl, "MechabellumMods/catalog.json", CatalogOriginUrl);

    public IReadOnlyList<Uri> BuildFileCandidates(string relativePath)
    {
        var relative = NormalizeCatalogRelativePath(relativePath);
        return RemoteFetch.BuildCandidates(
            MirrorBaseUrl,
            $"MechabellumMods/{relative}",
            new Uri(GetRawUrl(relative)));
    }

    /// <summary>
    /// Mirror first, then the authoritative copy, then the repo tree. The middle one is
    /// <c>originUrl</c> when the catalog gives one, because a mod over GitHub's 100 MB per-file
    /// push limit cannot live in the repo tree and has to come from a Release asset instead.
    ///
    /// The repo path stays on the list behind it so that a mis-stamped or half-published
    /// <c>originUrl</c> degrades to a wasted request rather than making the mod uninstallable.
    /// For a mod genuinely too large for the repo that last candidate simply 404s.
    /// </summary>
    public IReadOnlyList<Uri> BuildFileCandidates(CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        var relative = NormalizeCatalogRelativePath(mod.File ?? "");
        var rawUrl = new Uri(GetRawUrl(relative));
        var origin = TryParseOriginUrl(mod.OriginUrl);

        var candidates = RemoteFetch
            .BuildCandidates(MirrorBaseUrl, $"MechabellumMods/{relative}", origin ?? rawUrl)
            .ToList();

        if (origin is not null && origin != rawUrl)
            candidates.Add(rawUrl);

        return candidates;
    }

    /// <summary>
    /// Accepts https anywhere, and http only on loopback. Loopback http exists so the download
    /// path can be exercised end to end against a local server; it is not a network exposure.
    /// Integrity never rests on the transport regardless, because sha256 is mandatory.
    /// </summary>
    static Uri? TryParseOriginUrl(string? originUrl)
    {
        if (string.IsNullOrWhiteSpace(originUrl))
            return null;
        if (!Uri.TryCreate(originUrl.Trim(), UriKind.Absolute, out var uri))
            return null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return null;

        if (uri.Scheme == Uri.UriSchemeHttps)
            return uri;
        if (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)
            return uri;
        return null;
    }

    /// <summary>
    /// Fetches every candidate in parallel and keeps the freshest copy. A mirror that is behind on
    /// syncing answers 200 with an old catalog, so taking the first success would hide updates the
    /// origin already publishes — the exact reason the player used to have to refresh the catalog
    /// panel by hand before the library could see them.
    /// </summary>
    public async Task<CatalogRoot> FetchCatalogAsync(CancellationToken ct = default)
    {
        LastStaleSource = null;
        var fetched = await RemoteFetch.GetAllAsync(_http, BuildCatalogCandidates(), ct).ConfigureAwait(false);

        var copies = new List<CatalogCopy>(fetched.Count);
        try
        {
            foreach (var result in fetched)
            {
                try
                {
                    var json = await result.Response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    copies.Add(new CatalogCopy(result.Used, json, DeserializeCatalog(json)));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A truncated copy, a bad declared charset, or malformed JSON loses to whichever
                    // candidate came back readable.
                }
            }
        }
        finally
        {
            foreach (var result in fetched)
                result.Dispose();
        }

        if (copies.Count == 0)
            throw new HttpRequestException("Catalog reached but no candidate returned parsable JSON.");

        var winner = PickFreshestIndex(copies);
        if (winner != 0)
            LastStaleSource = RemoteFetch.ClassifySource(copies[0].Used);

        var chosen = copies[winner];
        LastFetchSource = RemoteFetch.ClassifySource(chosen.Used);
        if (!string.IsNullOrWhiteSpace(DataRoot))
            CatalogCache.Write(DataRoot, chosen.Json);
        return chosen.Root;
    }

    readonly record struct CatalogCopy(Uri Used, string Json, CatalogRoot Root);

    /// <summary>
    /// Index of the freshest copy. Candidates arrive mirror-first and a tie keeps the earlier one, so
    /// an equally fresh mirror still wins and domestic players stay on it.
    /// </summary>
    static int PickFreshestIndex(IReadOnlyList<CatalogCopy> copies)
    {
        var stamps = copies.Select(c => FreshnessStamp(c.Root)).ToArray();

        var best = 0;
        for (var i = 1; i < copies.Count; i++)
        {
            if (stamps[i] is { } candidate && (stamps[best] is null || candidate > stamps[best]))
                best = i;
        }

        return best;
    }

    /// <summary>
    /// Newest stamp anywhere in the catalog. Entry stamps count alongside the root one because a
    /// maintainer who updates a mod but forgets to bump the root field would otherwise leave a stale
    /// mirror looking equally fresh — the exact case this comparison exists for. Also covers catalogs
    /// predating the root field.
    /// </summary>
    static DateTimeOffset? FreshnessStamp(CatalogRoot root)
    {
        var newest = ParseStamp(root.UpdatedAt);
        foreach (var mod in root.Mods)
        {
            if (ParseStamp(mod.UpdatedAt) is { } stamp && (newest is null || stamp > newest))
                newest = stamp;
        }

        return newest;
    }

    static DateTimeOffset? ParseStamp(string? raw) =>
        DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    public CatalogRoot? TryLoadCachedCatalog()
    {
        if (string.IsNullOrWhiteSpace(DataRoot))
            return null;
        if (!CatalogCache.TryRead(DataRoot, out var json))
            return null;
        try
        {
            LastStaleSource = null;
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

    /// <summary>
    /// Downloads a catalog mod to <paramref name="destPath"/>, resuming across attempts and
    /// falling back from the mirror to the authoritative copy. The destination only appears once
    /// the bytes match the catalog's sha256; until then the transfer lives in a sibling
    /// <c>.part</c> file.
    /// </summary>
    public async Task DownloadModAsync(
        CatalogMod mod,
        string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (string.IsNullOrWhiteSpace(destPath))
            throw new ArgumentException("Destination path is required.", nameof(destPath));

        // A mod is a .NET assembly MelonLoader loads into the game. Once mods are large enough to
        // live outside the repo tree, "it came from a github.com host" stops being a meaningful
        // trust signal, so the hash is required from every source without exception.
        var expectedHash = NormalizeHash(mod.Sha256)
            ?? throw new InvalidOperationException(MissingHashMessage);

        var expectedSize = mod.Size > 0 ? mod.Size : 0;
        var ceiling = expectedSize > 0 ? expectedSize : AbsoluteMaxDownloadBytes;

        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var partPath = destPath + ".part";
        var failures = new List<string>();
        string? contentFailure = null;
        Uri? used = null;

        foreach (var uri in BuildFileCandidates(mod))
        {
            var moveOn = false;
            for (var attempt = 1; attempt <= AttemptsPerCandidate && used is null && !moveOn; attempt++)
            {
                if (attempt > 1)
                    await Task.Delay(RetryBackoff(attempt), ct).ConfigureAwait(false);

                try
                {
                    await FetchIntoPartAsync(uri, partPath, expectedSize, ceiling, progress, ct)
                        .ConfigureAwait(false);

                    // Resuming rules out a streaming hash: TransformBlock state cannot survive a
                    // restart, so the completed file is hashed in one pass instead. It happens per
                    // candidate because a mirror that is behind on syncing can serve the previous
                    // build at the identical size (assemblies are 512-byte aligned), which slips
                    // past the size check — and another source may still have the named bytes.
                    var actualHash = await ComputeFileHashAsync(partPath, ct).ConfigureAwait(false);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                    {
                        TryDeleteFile(partPath);
                        contentFailure ??= HashMismatchMessage(expectedHash, actualHash);
                        failures.Add($"{uri.Host}: sha256 mismatch");
                        moveOn = true;
                        continue;
                    }

                    used = uri;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (ContentContractException ex)
                {
                    // This source disagrees with the catalog about the file itself. Retrying it is
                    // pointless, but another source may well be serving the right bytes, and the
                    // partial written so far is untrustworthy.
                    TryDeleteFile(partPath);
                    contentFailure ??= ex.Message;
                    failures.Add($"{uri.Host}: {ex.Message}");
                    moveOn = true;
                }
                catch (PermanentFetchException ex)
                {
                    // A mirror 404 is the expected answer for a mod outside the hot subset, so the
                    // partial stays put for the next candidate to resume.
                    failures.Add($"{uri.Host}: {ex.Message}");
                    moveOn = true;
                }
                catch (Exception ex)
                {
                    failures.Add($"{uri.Host}: attempt {attempt}: {ex.Message}");
                }
            }

            if (used is not null)
                break;
        }

        if (used is null)
        {
            TryDeleteFile(partPath);
            var detail = string.Join("; ", failures);
            throw contentFailure is null
                ? new HttpRequestException("All remote fetch candidates failed: " + detail)
                : new InvalidOperationException(
                    $"下载内容与目录声明不符，已全部拒绝：{contentFailure}（{detail}）。请联系目录维护者核对。");
        }

        LastDownloadSource = RemoteFetch.ClassifySource(used);
        File.Move(partPath, destPath, overwrite: true);
    }

    static string HashMismatchMessage(string expectedHash, string actualHash) =>
        $"下载文件校验失败（目录声明 {expectedHash[..Math.Min(12, expectedHash.Length)]}…，实际 {actualHash[..12]}…）。文件已丢弃，请稍后重试或改用 GitHub 源。";

    /// <summary>
    /// Pulls one candidate into the <c>.part</c> file, resuming from whatever is already there.
    /// Returns only when the part file holds the complete resource.
    /// </summary>
    async Task FetchIntoPartAsync(
        Uri uri,
        string partPath,
        long expectedSize,
        long ceiling,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var existing = 0L;
        var partInfo = new FileInfo(partPath);
        if (partInfo.Exists)
        {
            existing = partInfo.Length;
            if (expectedSize > 0 && existing >= expectedSize)
            {
                // Either already complete or overlong from an earlier bad source. Both are settled
                // by the hash check, and an overlong leftover must not be appended to.
                if (existing > expectedSize)
                {
                    TryDeleteFile(partPath);
                    existing = 0;
                }
                else
                {
                    progress?.Report(new DownloadProgress(existing, expectedSize));
                    return;
                }
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0)
            request.Headers.Range = new RangeHeaderValue(existing, null);

        // One token governs headers and body, with its deadline pushed forward after every chunk.
        // That is an idle timeout: it fires on a stalled link but never on a merely slow one.
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(IdleTimeout);

        using var response = await SendAsync(request, idle.Token, ct).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.NotFound
            or HttpStatusCode.Forbidden
            or HttpStatusCode.Gone)
            throw new PermanentFetchException($"HTTP {(int)response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            TryDeleteFile(partPath);
            throw new TransientFetchException("range rejected; discarded the partial file");
        }

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}");

        // A server free to ignore Range answers 200 with the whole body; the part file then has
        // to be rewritten from zero rather than appended to.
        var resumed = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!resumed)
            existing = 0;

        var declaredLength = response.Content.Headers.ContentLength;
        var total = declaredLength is long length ? existing + length : expectedSize;
        if (declaredLength is long l)
        {
            var announced = existing + l;
            if (expectedSize > 0 && announced != expectedSize)
                throw new ContentContractException(
                    $"declared {announced} bytes but the catalog says {expectedSize}");
            if (announced > ceiling)
                throw new ContentContractException(
                    $"declared {announced} bytes, over the {ceiling} byte limit");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
        await using var file = new FileStream(
            partPath,
            resumed ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var written = existing;
        var reporter = new ProgressThrottle(progress, total);
        reporter.Report(written, force: true);

        var buffer = new byte[81920];
        while (true)
        {
            idle.CancelAfter(IdleTimeout);

            int read;
            try
            {
                read = await stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TransientFetchException(
                    $"no data for {IdleTimeout.TotalSeconds:0} s");
            }

            if (read <= 0)
                break;

            written += read;
            if (written > ceiling)
                throw new ContentContractException(
                    $"body exceeded the {ceiling} byte limit");

            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            reporter.Report(written);
        }

        await file.FlushAsync(ct).ConfigureAwait(false);

        if (expectedSize > 0 && written != expectedSize)
            throw new TransientFetchException(
                $"connection ended at {written} of {expectedSize} bytes");

        reporter.Report(written, force: true);
    }

    async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken idleToken,
        CancellationToken ct)
    {
        try
        {
            return await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, idleToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TransientFetchException(
                $"no response headers within {IdleTimeout.TotalSeconds:0} s");
        }
    }

    static TimeSpan RetryBackoff(int attempt) =>
        TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 2));

    static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Keeps a multi-gigabyte transfer from flooding the UI thread with updates.</summary>
    sealed class ProgressThrottle
    {
        static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

        readonly IProgress<DownloadProgress>? _sink;
        readonly long _total;
        DateTime _lastUtc = DateTime.MinValue;

        public ProgressThrottle(IProgress<DownloadProgress>? sink, long total)
        {
            _sink = sink;
            _total = total;
        }

        public void Report(long done, bool force = false)
        {
            if (_sink is null)
                return;

            var now = DateTime.UtcNow;
            if (!force && now - _lastUtc < Interval)
                return;

            _lastUtc = now;
            _sink.Report(new DownloadProgress(done, _total));
        }
    }

    /// <summary>The source served something the catalog does not describe; other sources may not.</summary>
    sealed class ContentContractException : Exception
    {
        public ContentContractException(string message) : base(message) { }
    }

    /// <summary>This source will not serve the file at all; move on without retrying it.</summary>
    sealed class PermanentFetchException : Exception
    {
        public PermanentFetchException(string message) : base(message) { }
    }

    /// <summary>The transfer broke in a way a retry may well survive.</summary>
    sealed class TransientFetchException : Exception
    {
        public TransientFetchException(string message) : base(message) { }
    }

    internal const string MissingHashMessage =
        "该目录条目缺少 sha256 校验值，无法确认下载的文件是否被篡改，已拒绝下载。请联系目录维护者补充校验值。";

    /// <summary>Lower-case hex digest, or null when the catalog does not declare one.</summary>
    static string? NormalizeHash(string? sha256)
    {
        var value = (sha256 ?? "").Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..].Trim();
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;
    }

    static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// True when a library package matches the catalog entry by catalog id or file hash.
    /// Filename-only matches are ignored so two mods shipping <c>Mod.dll</c> do not collide.
    /// </summary>
    public static bool IsInLibrary(IEnumerable<ModPackage> packages, CatalogMod mod) =>
        GetEntryState(packages, mod) != CatalogEntryState.NotInstalled;

    /// <summary>
    /// Whether the player has this catalog entry, and if so whether their copy is the one the
    /// catalog currently serves.
    /// </summary>
    public static CatalogEntryState GetEntryState(IEnumerable<ModPackage> packages, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(mod);

        var installed = FindInstalled(packages, mod);

        if (installed.Count == 0)
            return CatalogEntryState.NotInstalled;

        // Byte equality is the only judgement that still holds when an author ships new content
        // without bumping "version", so it outranks the metadata comparisons below.
        if (!string.IsNullOrWhiteSpace(mod.Sha256))
        {
            if (installed.Any(pkg => HasMatchingFileHash(pkg, mod)))
                return CatalogEntryState.UpToDate;
            if (installed.Any(HasAnyRecordedHash))
                return CatalogEntryState.UpdateAvailable;
        }

        return installed.Any(pkg => !IsOlderThanCatalog(pkg, mod))
            ? CatalogEntryState.UpToDate
            : CatalogEntryState.UpdateAvailable;
    }

    /// <summary>
    /// The library packages that represent this catalog entry, matched by catalog id or file hash.
    /// An update replaces exactly these.
    /// </summary>
    public static IReadOnlyList<ModPackage> FindInstalled(IEnumerable<ModPackage> packages, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(mod);

        return packages.Where(pkg => Matches(pkg, mod)).ToList();
    }

    /// <summary>
    /// Whether this library package is this catalog entry. The single judgement every caller
    /// must use: matching on filename instead would pair a package with whichever unrelated
    /// entry also ships <c>Mod.dll</c>, and an update would then overwrite the wrong mod.
    /// </summary>
    public static bool Matches(ModPackage pkg, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(pkg);
        ArgumentNullException.ThrowIfNull(mod);

        return MatchesCatalogId(pkg, (mod.Id ?? "").Trim()) || HasMatchingFileHash(pkg, mod);
    }

        static bool MatchesCatalogId(ModPackage pkg, string catalogId)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
            return false;
        return string.Equals(pkg.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pkg.Id, catalogId, StringComparison.OrdinalIgnoreCase);
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

    static bool HasAnyRecordedHash(ModPackage pkg) =>
        pkg.Files.Any(file => !string.IsNullOrWhiteSpace(file.Sha256));

    static bool IsOlderThanCatalog(ModPackage pkg, CatalogMod mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.Version) &&
            UpdateChecker.IsNewer(mod.Version!, pkg.Version ?? ""))
            return true;

        return TryParseCatalogDate(mod.UpdatedAt, out var catalogDate) &&
            TryParseCatalogDate(pkg.CatalogUpdatedAt, out var localDate) &&
            catalogDate > localDate;
    }

    static bool TryParseCatalogDate(string? raw, out DateTimeOffset value) =>
        DateTimeOffset.TryParse(
            (raw ?? "").Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);

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
