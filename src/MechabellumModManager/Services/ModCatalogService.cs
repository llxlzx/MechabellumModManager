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

/// <summary>
/// One slice of a mod that is too large for a single git blob. The assembled file is still
/// <see cref="CatalogMod.File"/>; these pieces are downloaded and concatenated in order.
/// </summary>
public sealed class CatalogPart
{
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// One file of a mod that stays a separate assembly on disk. <see cref="Path"/> is where it
/// sits under the Melon slot (for example <c>CUTGuide.Assets/CUTGuide.Text.dll</c>).
/// <see cref="File"/> is the catalog-repo path. These are not concatenated.
/// </summary>
public sealed class CatalogBundleFile
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
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

    /// <summary>
    /// Ordered slices of <see cref="File"/>, each small enough to live in the git repo.
    /// When present, the client downloads these and concatenates them. It does not request
    /// <see cref="File"/> itself. Older clients ignore the field and still download the whole file.
    /// </summary>
    [JsonPropertyName("parts")]
    public List<CatalogPart>? Parts { get; set; }

    /// <summary>
    /// Separate files deployed next to <see cref="File"/>. Each one must stay small enough for
    /// git. Older managers ignore the field and still request <see cref="File"/> itself.
    /// </summary>
    [JsonPropertyName("bundle")]
    public List<CatalogBundleFile>? Bundle { get; set; }

    [JsonPropertyName("preview")]
    public string? Preview { get; set; }

    /// <summary>SHA-256 of <see cref="Preview"/>. Absent on catalogs published before the field.</summary>
    [JsonPropertyName("previewSha256")]
    public string? PreviewSha256 { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Other catalog mods that must not be in the library at the same time.
    /// A one-sided entry still blocks both directions.
    /// </summary>
    [JsonPropertyName("conflicts")]
    public List<CatalogConflict>? Conflicts { get; set; }

    /// <summary>
    /// Optional per-language name/summary. Keys: zh-CN, en, de, ja, ru.
    /// Missing language or empty field falls back to top-level Name/Summary.
    /// </summary>
    [JsonPropertyName("locales")]
    public Dictionary<string, CatalogModLocale>? Locales { get; set; }

    /// <summary>
    /// Minimum manager version that may download this mod. Missing or blank means no floor.
    /// Compared as major.minor.patch. Older shipped managers ignore the field.
    /// </summary>
    [JsonPropertyName("minManagerVersion")]
    public string? MinManagerVersion { get; set; }
}

/// <summary>
/// Every whole-file candidate returned HTTP 404. The caller may refresh the catalog once.
/// Part downloads never throw this.
/// </summary>
public sealed class CatalogFileMissingException : HttpRequestException
{
    public CatalogFileMissingException(string message) : base(message) { }
}

public enum CatalogFetchKind { HotSkip, WarmNotModified, ColdApplied }

public sealed record CatalogFetchResult(CatalogFetchKind Kind, CatalogRoot? Root, string? Detail);

/// <summary>
/// Fetches Mod catalog and files from the independent MechabellumMods GitHub repo.
/// </summary>
public sealed partial class ModCatalogService
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

    /// <summary>Domestic installs try the mirror first. A GitHub-only route omits the mirror.</summary>
    public DownloadRouteKind Route { get; set; } = DownloadRouteKind.MirrorFirst;
    public string? DataRoot { get; set; }
    public string? LastFetchSource { get; private set; }
    public string? LastDownloadSource { get; private set; }

    /// <summary>
    /// Source that answered 200 with an older catalog than the copy that won, i.e. a mirror that
    /// has not finished syncing. Null when every reachable copy was equally fresh. Reset per fetch.
    /// </summary>
    public string? LastStaleSource { get; private set; }

    public TimeSpan HotCacheTtl { get; set; } = TimeSpan.FromMinutes(15);
    public DateTimeOffset? LastCatalogAppliedUtc { get; private set; }
    public string? LastCatalogEtag { get; private set; }

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
    /// GitHub rejects a git push over 100 MiB per file. A single blob over this cannot come from
    /// the repo. Split it into <see cref="CatalogPart"/> entries instead of sending players to the mirror.
    /// </summary>
    public const long GitRepoMaxBytes = 100L * 1024 * 1024;

    /// <summary>
    /// How long a catalog already written to disk is trusted across process restarts.
    /// Inside this window a launch does not contact GitHub or the mirror.
    /// </summary>
    public static readonly TimeSpan DiskQuietPeriod = TimeSpan.FromHours(6);

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

    public static string? PreviewUrl(
        CatalogMod? mod,
        string? mirrorBaseUrl = null,
        DownloadRouteKind route = DownloadRouteKind.MirrorFirst)
    {
        var urls = GetPreviewCandidateUrls(mod, mirrorBaseUrl, route);
        return urls.Count == 0 ? null : urls[0];
    }

    public static IReadOnlyList<string> GetPreviewCandidateUrls(
        CatalogMod? mod,
        string? mirrorBaseUrl = null,
        DownloadRouteKind route = DownloadRouteKind.MirrorFirst)
    {
        var preview = mod is null ? null : CatalogLocaleResolver.ResolvePreview(mod);
        if (string.IsNullOrWhiteSpace(preview))
            return Array.Empty<string>();

        try
        {
            var relative = NormalizeCatalogRelativePath(preview);
            return RemoteFetch.BuildCandidates(
                    mirrorBaseUrl,
                    $"MechabellumMods/{relative}",
                    new Uri(GetRawUrl(relative)),
                    route)
                .Select(u => u.ToString())
                .ToList();
        }
        catch (ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<Uri> BuildCatalogCandidates() =>
        RemoteFetch.BuildCandidates(
            MirrorBaseUrl,
            "MechabellumMods/catalog.json",
            CatalogOriginUrl,
            Route);

    public IReadOnlyList<Uri> BuildFileCandidates(string relativePath)
    {
        var relative = NormalizeCatalogRelativePath(relativePath);
        return RemoteFetch.BuildCandidates(
            MirrorBaseUrl,
            $"MechabellumMods/{relative}",
            new Uri(GetRawUrl(relative)),
            Route);
    }

    /// <summary>
    /// Mirror first when <see cref="Route"/> says so, then a release <c>originUrl</c>, then the
    /// repo path. A file over <see cref="GitRepoMaxBytes"/> cannot live in git, so the raw repo
    /// path is skipped unless nothing else is left. A GitHub-only route never lists the mirror.
    /// Parts use <see cref="BuildPartCandidates"/> and stay under the git limit.
    ///
    /// The repo path stays on the list for a normal mod so that a mis-stamped <c>originUrl</c>
    /// degrades to another GitHub request rather than making the mod uninstallable.
    /// </summary>
    public IReadOnlyList<Uri> BuildFileCandidates(CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        var relative = NormalizeCatalogRelativePath(mod.File ?? "");
        var rawUrl = new Uri(GetRawUrl(relative));
        var origin = TryParseOriginUrl(mod.OriginUrl);
        var mirror = Route == DownloadRouteKind.GitHubOnly
            ? null
            : RemoteFetch.TryMirrorUri(MirrorBaseUrl, $"MechabellumMods/{relative}");

        if (mod.Size > GitRepoMaxBytes)
        {
            var oversized = new List<Uri>(3);
            if (mirror is not null)
                oversized.Add(mirror);
            if (origin is not null)
                oversized.Add(origin);
            if (oversized.Count == 0)
                oversized.Add(rawUrl);
            return oversized;
        }

        var candidates = new List<Uri>(3);
        if (mirror is not null)
            candidates.Add(mirror);
        if (origin is not null)
            candidates.Add(origin);
        if (origin is null || origin != rawUrl)
            candidates.Add(rawUrl);
        return candidates;
    }

    /// <summary>
    /// Mirror first, repo path after. A part is small enough for git, so it never uses the
    /// whole-file <c>originUrl</c>. A GitHub-only route omits the mirror.
    /// </summary>
    public IReadOnlyList<Uri> BuildPartCandidates(CatalogPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var relative = NormalizeCatalogRelativePath(part.File);
        return RemoteFetch.BuildCandidates(
            MirrorBaseUrl,
            $"MechabellumMods/{relative}",
            new Uri(GetRawUrl(relative)),
            Route);
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
    /// Downloads a catalog mod to <paramref name="destPath"/>. GitHub is tried first. A candidate
    /// that cannot be reached is abandoned and the mirror is tried next, with no prompt in between.
    /// When <see cref="CatalogMod.Parts"/> is set, those slices are downloaded and concatenated;
    /// the whole file URL is not requested. The destination only appears once the bytes match the
    /// catalog's sha256; until then the transfer lives in a sibling <c>.part</c> file.
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
        if (mod.Bundle is { Count: > 0 })
            throw new InvalidOperationException("该目录条目是素材包，不能按单个文件下载。");

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

        if (mod.Parts is { Count: > 0 })
        {
            await DownloadPartsAsync(mod, destPath, expectedHash, expectedSize, progress, ct)
                .ConfigureAwait(false);
            return;
        }

        var partPath = destPath + ".part";
        var candidates = BuildFileCandidates(mod);
        var used = await DownloadVerifiedAsync(
            candidates, partPath, expectedHash, expectedSize, ceiling, progress, ct, signalAllNotFound: true)
            .ConfigureAwait(false);

        LastDownloadSource = RemoteFetch.ClassifySource(used);
        File.Move(partPath, destPath, overwrite: true);
    }

    /// <summary>
    /// Downloads each bundle member into <paramref name="stagingDirectory"/> at its deploy path.
    /// A member at or above <see cref="GitRepoMaxBytes"/> is refused before any request.
    /// </summary>
    public async Task DownloadBundleAsync(
        CatalogMod mod,
        string stagingDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mod);
        if (string.IsNullOrWhiteSpace(stagingDirectory))
            throw new ArgumentException("Staging directory is required.", nameof(stagingDirectory));

        var files = mod.Bundle ?? throw new InvalidOperationException("素材包目录为空，已拒绝下载。");
        if (mod.Parts is { Count: > 0 })
            throw new InvalidOperationException("目录同时声明了分块和素材包，已拒绝下载。");

        long total = 0;
        foreach (var item in files)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.File) || string.IsNullOrWhiteSpace(item.Path))
                throw new InvalidOperationException("素材包缺少文件路径，已拒绝下载。");
            if (NormalizeHash(item.Sha256) is null)
                throw new InvalidOperationException(MissingHashMessage);
            if (item.Size <= 0 || item.Size >= GitRepoMaxBytes)
                throw new InvalidOperationException(
                    "目录里的素材包达到或超过 100MB，无法放进 git 仓库，已拒绝下载。请联系目录维护者把素材包切小。");
            _ = BundleDestination(stagingDirectory, item.Path);
            total += item.Size;
        }

        Directory.CreateDirectory(stagingDirectory);
        long done = 0;
        var source = RemoteFetch.GithubSource;
        for (var i = 0; i < files.Count; i++)
        {
            var item = files[i];
            var dest = BundleDestination(stagingDirectory, item.Path!);
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var slice = progress is null ? null : new OffsetDownloadProgress(progress, done, total);
            var used = await DownloadVerifiedAsync(
                BuildFileCandidates(item.File!),
                dest + ".part",
                NormalizeHash(item.Sha256)!,
                item.Size,
                item.Size,
                slice,
                ct).ConfigureAwait(false);
            File.Move(dest + ".part", dest, overwrite: true);
            if (RemoteFetch.ClassifySource(used) == RemoteFetch.MirrorSource)
                source = RemoteFetch.MirrorSource;
            done += item.Size;
        }

        progress?.Report(new DownloadProgress(total, total));
        LastDownloadSource = source;
    }

    static string BundleDestination(string stagingDirectory, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(relativePath) || normalized.Contains(':') || normalized.StartsWith('/'))
            throw new InvalidOperationException("素材包路径不安全，已拒绝下载。");

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
            throw new InvalidOperationException("素材包路径不安全，已拒绝下载。");

        var dest = Path.GetFullPath(Path.Combine(
            new[] { stagingDirectory }.Concat(segments).ToArray()));
        var root = Path.GetFullPath(stagingDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("素材包路径不安全，已拒绝下载。");
        return dest;
    }

    async Task DownloadPartsAsync(
        CatalogMod mod,
        string destPath,
        string expectedHash,
        long expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var parts = mod.Parts ?? throw new InvalidOperationException("分块目录为空，已拒绝下载。");
        long sum = 0;
        foreach (var part in parts)
        {
            if (part is null || string.IsNullOrWhiteSpace(part.File))
                throw new InvalidOperationException("分块缺少文件路径，已拒绝下载。");
            if (NormalizeHash(part.Sha256) is null)
                throw new InvalidOperationException(MissingHashMessage);
            if (part.Size <= 0 || part.Size >= GitRepoMaxBytes)
                throw new InvalidOperationException(
                    "目录里的分块达到或超过 100MB，无法放进 git 仓库，已拒绝下载。请联系目录维护者把分块切小。");
            sum += part.Size;
        }

        if (expectedSize > 0 && sum != expectedSize)
            throw new InvalidOperationException(
                $"分块大小之和（{sum}）与目录声明的文件大小（{expectedSize}）不一致，已拒绝下载。");

        var staging = destPath + ".parts";
        Directory.CreateDirectory(staging);
        var chunkPaths = new List<string>(parts.Count);
        var partPath = destPath + ".part";
        try
        {
            long done = 0;
            var source = RemoteFetch.GithubSource;
            var total = expectedSize > 0 ? expectedSize : sum;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var chunkPath = Path.Combine(staging, i.ToString("D4"));
                chunkPaths.Add(chunkPath);
                var partHash = NormalizeHash(part.Sha256)!;
                var slice = progress is null ? null : new OffsetDownloadProgress(progress, done, total);
                var used = await DownloadVerifiedAsync(
                    BuildPartCandidates(part),
                    chunkPath,
                    partHash,
                    part.Size,
                    part.Size,
                    slice,
                    ct).ConfigureAwait(false);
                if (RemoteFetch.ClassifySource(used) == RemoteFetch.MirrorSource)
                    source = RemoteFetch.MirrorSource;
                done += part.Size;
            }

            TryDeleteFile(partPath);
            await using (var output = new FileStream(
                partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                foreach (var chunkPath in chunkPaths)
                {
                    await using var input = new FileStream(
                        chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                    await input.CopyToAsync(output, ct).ConfigureAwait(false);
                }
            }

            var actualHash = await ComputeFileHashAsync(partPath, ct).ConfigureAwait(false);
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                TryDeleteFile(partPath);
                throw new InvalidOperationException(HashMismatchMessage(expectedHash, actualHash));
            }

            progress?.Report(new DownloadProgress(expectedSize > 0 ? expectedSize : sum, expectedSize > 0 ? expectedSize : sum));
            LastDownloadSource = source;
            File.Move(partPath, destPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(partPath);
            try { Directory.Delete(staging, recursive: true); } catch { /* best effort */ }
        }
    }

    sealed class OffsetDownloadProgress : IProgress<DownloadProgress>
    {
        readonly IProgress<DownloadProgress> _inner;
        readonly long _done;
        readonly long _total;

        public OffsetDownloadProgress(IProgress<DownloadProgress> inner, long done, long total)
        {
            _inner = inner;
            _done = done;
            _total = total;
        }

        public void Report(DownloadProgress value) =>
            _inner.Report(new DownloadProgress(_done + value.BytesDownloaded, _total));
    }

    /// <summary>
    /// Tries each candidate until one matches <paramref name="expectedHash"/>. Throws the same
    /// errors the single-file download always has when every candidate is refused.
    /// </summary>
    async Task<Uri> DownloadVerifiedAsync(
        IReadOnlyList<Uri> candidates,
        string partPath,
        string expectedHash,
        long expectedSize,
        long ceiling,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct,
        bool signalAllNotFound = false)
    {
        var failures = new List<string>();
        string? contentFailure = null;
        Uri? used = null;

        for (var index = 0; index < candidates.Count; index++)
        {
            var uri = candidates[index];
            var anotherCandidateRemains = index < candidates.Count - 1;
            var headerBudget = anotherCandidateRemains ? RemoteFetch.ConnectBudget : IdleTimeout;
            var moveOn = false;
            for (var attempt = 1; attempt <= AttemptsPerCandidate && used is null && !moveOn; attempt++)
            {
                if (attempt > 1)
                    await Task.Delay(RetryBackoff(attempt), ct).ConfigureAwait(false);

                try
                {
                    await FetchIntoPartAsync(uri, partPath, expectedSize, ceiling, headerBudget, progress, ct)
                        .ConfigureAwait(false);

                    // Resuming rules out a streaming hash: TransformBlock state cannot survive a
                    // restart, so the completed file is hashed in one pass instead. It happens per
                    // candidate because a source that is behind on syncing can serve the previous
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
                    TryDeleteFile(partPath);
                    contentFailure ??= ex.Message;
                    failures.Add($"{uri.Host}: {ex.Message}");
                    moveOn = true;
                }
                catch (PermanentFetchException ex)
                {
                    failures.Add($"{uri.Host}: {ex.Message}");
                    moveOn = true;
                }
                catch (HttpRequestException ex) when (anotherCandidateRemains)
                {
                    failures.Add($"{uri.Host}: {ex.Message}");
                    moveOn = true;
                }
                catch (TransientFetchException ex) when (
                    anotherCandidateRemains &&
                    ex.Message.StartsWith("no response headers", StringComparison.Ordinal))
                {
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
            if (contentFailure is not null)
            {
                throw new InvalidOperationException(
                    $"下载内容与目录声明不符，已全部拒绝：{contentFailure}（{detail}）。请联系目录维护者核对。");
            }

            var message = "All remote fetch candidates failed: " + detail;
            if (signalAllNotFound && AllCandidatesHttp404(failures))
                throw new CatalogFileMissingException(message);
            throw new HttpRequestException(message);
        }

        return used;
    }

    internal static bool AllCandidatesHttp404(IReadOnlyList<string> failures) =>
        failures.Count > 0 && failures.All(failure => failure.EndsWith(": HTTP 404", StringComparison.Ordinal));

    static string HashMismatchMessage(string expectedHash, string actualHash) =>
        $"下载文件校验失败（目录声明 {expectedHash[..Math.Min(12, expectedHash.Length)]}…，实际 {actualHash[..12]}…）。文件已丢弃，请稍后重试。";

    /// <summary>
    /// Pulls one candidate into the <c>.part</c> file, resuming from whatever is already there.
    /// Returns only when the part file holds the complete resource.
    /// </summary>
    async Task FetchIntoPartAsync(
        Uri uri,
        string partPath,
        long expectedSize,
        long ceiling,
        TimeSpan headerBudget,
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

        // Headers use a short budget when another source is still available, so a blocked GitHub
        // route switches to the mirror instead of hanging. Once headers arrive the budget is
        // disarmed and the body keeps the idle timeout, which fires on a stall but not on a
        // merely slow transfer.
        using var header = CancellationTokenSource.CreateLinkedTokenSource(ct);
        header.CancelAfter(headerBudget);
        using var response = await SendAsync(request, header.Token, headerBudget, ct).ConfigureAwait(false);
        header.CancelAfter(Timeout.InfiniteTimeSpan);

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

        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(IdleTimeout);

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
        TimeSpan headerBudget,
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
                $"no response headers within {headerBudget.TotalSeconds:0} s");
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
    /// True when a library package matches the catalog entry by catalog id, file hash, or a
    /// distinctive Melon assembly stem (see <see cref="Matches"/>).
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

        if (mod.Bundle is { Count: > 0 })
        {
            if (installed.Any(pkg => BundleIsComplete(pkg, mod)))
                return CatalogEntryState.UpToDate;
            if (installed.Any(HasAnyRecordedHash))
                return CatalogEntryState.UpdateAvailable;
        }

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
    /// The library packages that represent this catalog entry (catalog id, hash, or Melon stem).
    /// An update replaces exactly these.
    /// </summary>
    /// <summary>
    /// This package is behind the catalog, but another library copy already matches the catalog
    /// bytes. The row must not offer Update — that click used to no-op because the catalog entry
    /// itself is already current.
    /// </summary>
    public static bool IsStaleDuplicate(ModPackage pkg, IEnumerable<ModPackage> library, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(pkg);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(mod);

        if (GetEntryState([pkg], mod) != CatalogEntryState.UpdateAvailable)
            return false;

        return GetEntryState(library, mod) == CatalogEntryState.UpToDate;
    }

    public static IReadOnlyList<ModPackage> FindInstalled(IEnumerable<ModPackage> packages, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(mod);

        return packages.Where(pkg => Matches(pkg, mod)).ToList();
    }

    /// <summary>
    /// Whether this library package is this catalog entry. Generic filenames like <c>Mod.dll</c>
    /// never count; only a distinctive catalog file stem may pair with the Melon
    /// <see cref="ModPackage.DisplayName"/> (set from MelonInfo on import).
    /// </summary>
    public static bool Matches(ModPackage pkg, CatalogMod mod)
    {
        ArgumentNullException.ThrowIfNull(pkg);
        ArgumentNullException.ThrowIfNull(mod);

        return MatchesCatalogId(pkg, (mod.Id ?? "").Trim()) ||
            HasMatchingFileHash(pkg, mod) ||
            MatchesMelonAssembly(pkg, mod);
    }

    static bool MatchesCatalogId(ModPackage pkg, string catalogId)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
            return false;
        return string.Equals(pkg.CatalogId, catalogId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pkg.Id, catalogId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Game-folder imports keep MelonInfo name as <see cref="ModPackage.DisplayName"/> and a
    /// slug+hash package id, so they never share catalog id. Pair them to the catalog file stem
    /// when that stem is distinctive (not <c>Mod</c> / <c>Plugin</c>) and authors agree when both
    /// sides declare one.
    /// </summary>
    static bool MatchesMelonAssembly(ModPackage pkg, CatalogMod mod)
    {
        var displayName = (pkg.DisplayName ?? "").Trim();
        if (displayName.Length == 0)
            return false;

        var stem = Path.GetFileNameWithoutExtension((mod.File ?? "").Replace('\\', '/').Trim());
        if (string.IsNullOrWhiteSpace(stem) || IsGenericAssemblyStem(stem))
            return false;

        if (!string.Equals(displayName, stem, StringComparison.OrdinalIgnoreCase))
            return false;

        if (pkg.Type != ParsePackageType(mod.Type))
            return false;

        var pkgAuthor = (pkg.Author ?? "").Trim();
        var catAuthor = (mod.Author ?? "").Trim();
        if (pkgAuthor.Length > 0 &&
            catAuthor.Length > 0 &&
            !string.Equals(pkgAuthor, catAuthor, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    static bool IsGenericAssemblyStem(string stem) =>
        stem.Equals("Mod", StringComparison.OrdinalIgnoreCase) ||
        stem.Equals("Plugin", StringComparison.OrdinalIgnoreCase) ||
        stem.Equals("Assembly", StringComparison.OrdinalIgnoreCase) ||
        stem.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase);

    static bool BundleIsComplete(ModPackage pkg, CatalogMod mod)
    {
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in mod.Bundle ?? [])
        {
            var hash = NormalizeHash(item?.Sha256);
            if (hash is null)
                return false;
            wanted.Add(hash);
        }

        if (wanted.Count == 0)
            return false;

        foreach (var file in pkg.Files)
        {
            var hash = NormalizeHash(file.Sha256);
            if (hash is not null)
                wanted.Remove(hash);
        }

        return wanted.Count == 0;
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
