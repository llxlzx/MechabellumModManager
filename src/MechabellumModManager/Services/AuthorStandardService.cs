using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace MechabellumModManager.Services;

public sealed class AuthorStandardFetch
{
    public required byte[] Bytes { get; init; }
    public bool FromCache { get; init; }
}

public enum AuthorStandardCheck
{
    NoBaseline,
    Current,
    UpdateAvailable,
    Unreachable
}

/// <summary>
/// Fetches the author-facing standard. The domestic mirror is tried first.
/// This is the only download path the manager exposes for that document.
/// </summary>
public sealed class AuthorStandardService
{
    public const int MaxBytes = 262_144;
    public const int MinBytes = 400;
    public const string CacheFileName = "author-standard-cache.md";
    public const string RelativeKey = "MechabellumMods/docs/ai-mod-standard.md";

    public static readonly Uri GitHubUri = new(
        "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/docs/ai-mod-standard.md");

    readonly HttpClient _http;

    public string? MirrorBaseUrl { get; set; }
    public DownloadRouteKind Route { get; set; } = DownloadRouteKind.MirrorFirst;
    public string? DataRoot { get; set; }

    public AuthorStandardService(HttpClient? http = null, string? dataRoot = null, string? mirrorBaseUrl = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        DataRoot = dataRoot;
        MirrorBaseUrl = string.IsNullOrWhiteSpace(mirrorBaseUrl) ? null : mirrorBaseUrl.Trim().TrimEnd('/');
    }

    public async Task<AuthorStandardFetch?> FetchAsync(CancellationToken ct = default)
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

            if (bytes is null || !IsDocument(bytes))
                continue;
            return new AuthorStandardFetch { Bytes = bytes };
        }

        if (TryReadCache(out var cached) && IsDocument(cached))
            return new AuthorStandardFetch { Bytes = cached, FromCache = true };

        return null;
    }

    /// <summary>
    /// Compares the last saved copy with the remote document. No saved copy means no request.
    /// A check never replaces the saved copy.
    /// </summary>
    public async Task<AuthorStandardCheck> CheckAsync(CancellationToken ct = default)
    {
        if (!TryReadCache(out var saved) || !IsDocument(saved))
            return AuthorStandardCheck.NoBaseline;

        byte[]? remote = null;
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

            if (bytes is null || !IsDocument(bytes))
                continue;
            remote = bytes;
            break;
        }

        if (remote is null)
            return AuthorStandardCheck.Unreachable;
        return SHA256.HashData(remote).AsSpan().SequenceEqual(SHA256.HashData(saved))
            ? AuthorStandardCheck.Current
            : AuthorStandardCheck.UpdateAvailable;
    }

    public void Remember(byte[] bytes)
    {
        if (!IsDocument(bytes))
            return;
        TryWriteCache(bytes);
    }

    public static bool IsDocument(byte[] bytes)
    {
        if (bytes.Length < MinBytes || bytes.Length > MaxBytes)
            return false;
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!text.StartsWith("#", StringComparison.Ordinal))
            return false;
        if (text.Contains("<html", StringComparison.OrdinalIgnoreCase))
            return false;
        return text.Contains("minManagerVersion", StringComparison.Ordinal)
            && text.Contains("MelonMod", StringComparison.Ordinal);
    }

    IEnumerable<Uri> Candidates()
    {
        if (Route != DownloadRouteKind.GitHubOnly &&
            !string.IsNullOrWhiteSpace(MirrorBaseUrl) &&
            Uri.TryCreate(MirrorBaseUrl.Trim().TrimEnd('/') + "/" + RelativeKey, UriKind.Absolute, out var mirror))
            yield return mirror;
        yield return GitHubUri;
    }

    async Task<byte[]?> ReadAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
            return null;
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

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
            // A failed cache write still returns the fresh bytes.
        }
    }

    bool TryReadCache(out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(DataRoot))
            return false;
        var path = Path.Combine(DataRoot, CacheFileName);
        if (!File.Exists(path))
            return false;
        try
        {
            bytes = File.ReadAllBytes(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
