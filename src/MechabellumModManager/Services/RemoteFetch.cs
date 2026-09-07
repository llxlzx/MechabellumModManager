using System.Net.Http;

namespace MechabellumModManager.Services;

public sealed record RemoteFetchResult(Uri Used, HttpResponseMessage Response) : IDisposable
{
    public void Dispose() => Response.Dispose();
}

/// <summary>
/// Sequential GET over candidate URLs (mirror first, then GitHub). One pass, no retry loop.
/// Caller owns and must dispose the successful response.
/// </summary>
public static class RemoteFetch
{
    public const string MirrorSource = "mirror";
    public const string GithubSource = "github";

    public static async Task<RemoteFetchResult> GetAsync(
        HttpClient http,
        IReadOnlyList<Uri> candidates,
        CancellationToken ct = default) =>
        await GetAsync(http, candidates, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);

    public static async Task<RemoteFetchResult> GetAsync(
        HttpClient http,
        IReadOnlyList<Uri> candidates,
        HttpCompletionOption completion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
            throw new ArgumentException("At least one candidate URL is required.", nameof(candidates));

        var failures = new List<string>(candidates.Count);
        foreach (var uri in candidates)
        {
            try
            {
                var resp = await http.GetAsync(uri, completion, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return new RemoteFetchResult(uri, resp);

                var code = (int)resp.StatusCode;
                resp.Dispose();
                failures.Add($"{uri.Host}: HTTP {code}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                failures.Add($"{uri.Host}: timeout");
            }
            catch (HttpRequestException ex)
            {
                failures.Add($"{uri.Host}: {ex.Message}");
            }
            catch (Exception ex)
            {
                failures.Add($"{uri.Host}: {ex.Message}");
            }
        }

        throw new HttpRequestException(
            "All remote fetch candidates failed: " + string.Join("; ", failures));
    }

    public static string ClassifySource(Uri used)
    {
        ArgumentNullException.ThrowIfNull(used);
        var host = used.Host;
        if (host.Contains("github", StringComparison.OrdinalIgnoreCase)
            || host.Contains("githubusercontent", StringComparison.OrdinalIgnoreCase))
            return GithubSource;
        return MirrorSource;
    }

    public static Uri? TryMirrorUri(string? mirrorBaseUrl, string relativePath)
    {
        var baseUrl = (mirrorBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var rel = (relativePath ?? "").Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(rel))
            return null;

        return Uri.TryCreate($"{baseUrl}/{rel}", UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }

    public static IReadOnlyList<Uri> BuildCandidates(
        string? mirrorBaseUrl,
        string mirrorRelativePath,
        Uri githubFallback)
    {
        ArgumentNullException.ThrowIfNull(githubFallback);
        var list = new List<Uri>(2);
        var mirror = TryMirrorUri(mirrorBaseUrl, mirrorRelativePath);
        if (mirror is not null)
            list.Add(mirror);
        list.Add(githubFallback);
        return list;
    }
}
