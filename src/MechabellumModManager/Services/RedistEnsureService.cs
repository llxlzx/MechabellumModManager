using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class RedistEnsureResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public Dictionary<string, string> SourceById { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Overall redist fill progress (0–100), for installer CLI / UI.</summary>
public sealed class RedistProgress
{
    public int Percent { get; init; }
    public string ArtifactId { get; init; } = "";
    public string Detail { get; init; } = "";
}

/// <summary>
/// Fills {redistDir} from MechabellumRedist/manifest.json (COS → origin) with mandatory sha256.
/// </summary>
public sealed class RedistEnsureService
{
    public const string MirrorSource = "mirror";
    public const string OriginSource = "origin";
    public const string LocalSource = "local";
    public const string ManifestRelativePath = "MechabellumRedist/manifest.json";
    public const string BundledManifestFileName = "redist-manifest.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    readonly HttpClient _http;

    public RedistEnsureService(HttpClient? http = null)
    {
        _http = http ?? CreateDefaultHttpClient();
    }

    public static HttpClient CreateDefaultHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MechabellumModManager", "1.2"));
        return http;
    }

    public static RedistManifest ParseManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("redist manifest is empty");

        var manifest = JsonSerializer.Deserialize<RedistManifest>(json, JsonOptions)
                       ?? throw new InvalidDataException("redist manifest deserialize returned null");

        if (manifest.Artifacts.Count == 0)
            throw new InvalidDataException("redist manifest has no artifacts");

        foreach (var a in manifest.Artifacts)
        {
            if (string.IsNullOrWhiteSpace(a.Id))
                throw new InvalidDataException("redist artifact missing id");
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new InvalidDataException($"redist artifact '{a.Id}' missing path");
            var hash = NormalizeHash(a.Sha256);
            if (hash is null)
                throw new InvalidDataException($"redist artifact '{a.Id}' missing sha256");
            a.Sha256 = hash;
            a.Path = a.Path.Replace('\\', '/').Trim().TrimStart('/');
        }

        return manifest;
    }

    public static IReadOnlyList<Uri> BuildArtifactCandidates(string? mirrorBaseUrl, RedistArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var list = new List<Uri>(2);
        var mirror = RemoteFetch.TryMirrorUri(mirrorBaseUrl, "MechabellumRedist/" + artifact.Path.TrimStart('/'));
        if (mirror is not null)
            list.Add(mirror);

        if (!string.IsNullOrWhiteSpace(artifact.OriginUrl)
            && Uri.TryCreate(artifact.OriginUrl.Trim(), UriKind.Absolute, out var origin)
            && (origin.Scheme == Uri.UriSchemeHttps || origin.Scheme == Uri.UriSchemeHttp))
        {
            list.Add(origin);
        }

        return list;
    }

    public static string ClassifyArtifactSource(Uri used, string? mirrorBaseUrl)
    {
        var mirror = RemoteFetch.TryMirrorUri(mirrorBaseUrl, "MechabellumRedist/x");
        if (mirror is not null
            && string.Equals(used.Host, mirror.Host, StringComparison.OrdinalIgnoreCase))
            return MirrorSource;
        return OriginSource;
    }

    public async Task<RedistEnsureResult> EnsureAsync(
        string redistDir,
        string? mirrorBaseUrl,
        IReadOnlyList<string>? ids = null,
        string? bundledManifestJson = null,
        IProgress<RedistProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redistDir);
        Directory.CreateDirectory(redistDir);

        RedistManifest manifest;
        try
        {
            manifest = await LoadManifestAsync(mirrorBaseUrl, bundledManifestJson, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new RedistEnsureResult
            {
                Success = false,
                Message = "无法加载 redist 清单：" + ex.Message
            };
        }

        var wanted = ResolveWanted(manifest, ids);
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        var weights = wanted.Select(ArtifactWeight).ToArray();
        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
            totalWeight = 1;
        long completedWeight = 0;

        for (var i = 0; i < wanted.Count; i++)
        {
            var artifact = wanted[i];
            var weight = weights[i];
            try
            {
                var source = await EnsureOneAsync(
                        redistDir,
                        mirrorBaseUrl,
                        artifact,
                        weight,
                        completedWeight,
                        totalWeight,
                        progress,
                        ct)
                    .ConfigureAwait(false);
                sources[artifact.Id] = source;
            }
            catch (Exception ex)
            {
                errors.Add($"{artifact.Id}: {ex.Message}");
            }

            completedWeight += weight;
            ReportProgress(progress, completedWeight, totalWeight, artifact.Id, "done");
        }

        if (errors.Count > 0)
        {
            return new RedistEnsureResult
            {
                Success = false,
                Message = "部分 redist 拉取失败：\n" + string.Join("\n", errors),
                SourceById = sources
            };
        }

        ReportProgress(progress, totalWeight, totalWeight, "", "complete");
        return new RedistEnsureResult
        {
            Success = true,
            Message = "redist 已就绪（" + string.Join(", ", sources.Select(kv => $"{kv.Key}={kv.Value}")) + "）",
            SourceById = sources
        };
    }

    async Task<RedistManifest> LoadManifestAsync(
        string? mirrorBaseUrl,
        string? bundledManifestJson,
        CancellationToken ct)
    {
        var mirror = RemoteFetch.TryMirrorUri(mirrorBaseUrl, ManifestRelativePath);
        if (mirror is not null)
        {
            try
            {
                using var fetched = await RemoteFetch.GetAsync(_http, new[] { mirror }, ct).ConfigureAwait(false);
                var json = await fetched.Response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return ParseManifest(json);
            }
            catch
            {
                // Fall through to bundled pin.
            }
        }

        var bundled = bundledManifestJson ?? TryReadBundledManifestJson();
        if (string.IsNullOrWhiteSpace(bundled))
            throw new InvalidDataException("无镜像清单且本地未附带 redist-manifest.json");

        return ParseManifest(bundled);
    }

    public static string? TryReadBundledManifestJson()
    {
        foreach (var candidate in BundledManifestCandidates())
        {
            if (File.Exists(candidate))
                return File.ReadAllText(candidate, Encoding.UTF8);
        }

        return null;
    }

    static IEnumerable<string> BundledManifestCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "Assets", BundledManifestFileName);
        yield return Path.Combine(baseDir, BundledManifestFileName);

        var cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "Assets", BundledManifestFileName);
        yield return Path.Combine(cwd, "src", "MechabellumModManager", "Assets", BundledManifestFileName);
    }

    static List<RedistArtifact> ResolveWanted(RedistManifest manifest, IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
            return manifest.Artifacts.ToList();

        var map = manifest.Artifacts.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        var list = new List<RedistArtifact>(ids.Count);
        foreach (var id in ids)
        {
            if (!map.TryGetValue(id.Trim(), out var artifact))
                throw new InvalidDataException($"redist manifest 中没有 id '{id}'");
            list.Add(artifact);
        }

        return list;
    }

    async Task<string> EnsureOneAsync(
        string redistDir,
        string? mirrorBaseUrl,
        RedistArtifact artifact,
        long artifactWeight,
        long completedWeight,
        long totalWeight,
        IProgress<RedistProgress>? progress,
        CancellationToken ct)
    {
        var dest = Path.Combine(redistDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        if (File.Exists(dest) && HashFile(dest) == artifact.Sha256)
        {
            ReportProgress(
                progress,
                completedWeight + artifactWeight,
                totalWeight,
                artifact.Id,
                "local");
            return LocalSource;
        }

        var candidates = BuildArtifactCandidates(mirrorBaseUrl, artifact);
        if (candidates.Count == 0)
            throw new InvalidOperationException("无可用下载地址（无镜像且无 originUrl）");

        var failures = new List<string>();
        foreach (var uri in candidates)
        {
            var temp = dest + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                using var resp = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    failures.Add($"{uri.Host}: HTTP {(int)resp.StatusCode}");
                    continue;
                }

                var contentLength = resp.Content.Headers.ContentLength;
                await using (var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await CopyWithProgressAsync(
                            input,
                            output,
                            contentLength,
                            artifact.Id,
                            artifactWeight,
                            completedWeight,
                            totalWeight,
                            progress,
                            ct)
                        .ConfigureAwait(false);
                }

                var hash = HashFile(temp);
                if (!string.Equals(hash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{uri.Host}: sha256 mismatch");
                    TryDelete(temp);
                    continue;
                }

                if (File.Exists(dest))
                    File.Delete(dest);
                File.Move(temp, dest);
                return ClassifyArtifactSource(uri, mirrorBaseUrl);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                TryDelete(temp);
                throw;
            }
            catch (Exception ex)
            {
                TryDelete(temp);
                failures.Add($"{uri.Host}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(string.Join("; ", failures));
    }

    static async Task CopyWithProgressAsync(
        Stream input,
        Stream output,
        long? contentLength,
        string artifactId,
        long artifactWeight,
        long completedWeight,
        long totalWeight,
        IProgress<RedistProgress>? progress,
        CancellationToken ct)
    {
        var buffer = new byte[81920];
        long received = 0;
        var lastReportedPercent = -1;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;

            double fileFrac;
            string detail;
            if (contentLength is > 0)
            {
                fileFrac = Math.Min(1.0, received / (double)contentLength.Value);
                detail = $"{ByteSize.Format(received)} / {ByteSize.Format(contentLength.Value)}";
            }
            else
            {
                // Unknown length: advance within the artifact slice by received KB, capped at 99%.
                fileFrac = Math.Min(0.99, received / (double)Math.Max(artifactWeight, 1));
                detail = ByteSize.Format(received);
            }

            var overall = completedWeight + (long)(artifactWeight * fileFrac);
            var percentInt = (int)Math.Min(100, overall * 100.0 / totalWeight);
            var shouldReport = contentLength is > 0
                ? percentInt != lastReportedPercent
                : received == read || received % (512 * 1024) < read;

            if (shouldReport)
            {
                lastReportedPercent = percentInt;
                ReportProgress(progress, overall, totalWeight, artifactId, detail);
            }
        }
    }

    static long ArtifactWeight(RedistArtifact artifact) =>
        artifact.Size is > 0 ? artifact.Size.Value : 1_000_000L;

    static void ReportProgress(
        IProgress<RedistProgress>? progress,
        long completedWeight,
        long totalWeight,
        string artifactId,
        string detail)
    {
        if (progress is null)
            return;
        var percent = totalWeight <= 0
            ? 0
            : (int)Math.Clamp(completedWeight * 100.0 / totalWeight, 0, 100);
        progress.Report(new RedistProgress
        {
            Percent = percent,
            ArtifactId = artifactId,
            Detail = detail
        });
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    static string? NormalizeHash(string? sha256)
    {
        var value = (sha256 ?? "").Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..].Trim();
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* best effort */
        }
    }
}
