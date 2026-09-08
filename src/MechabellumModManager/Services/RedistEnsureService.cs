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

        foreach (var artifact in wanted)
        {
            try
            {
                var source = await EnsureOneAsync(redistDir, mirrorBaseUrl, artifact, ct).ConfigureAwait(false);
                sources[artifact.Id] = source;
            }
            catch (Exception ex)
            {
                errors.Add($"{artifact.Id}: {ex.Message}");
            }
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
        CancellationToken ct)
    {
        var dest = Path.Combine(redistDir, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        if (File.Exists(dest) && HashFile(dest) == artifact.Sha256)
            return LocalSource;

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

                await using (var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output, ct).ConfigureAwait(false);
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
