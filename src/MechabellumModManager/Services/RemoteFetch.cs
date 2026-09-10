using System.Net.Http;

namespace MechabellumModManager.Services;

public sealed record RemoteFetchResult(Uri Used, HttpResponseMessage Response) : IDisposable
{
    public void Dispose() => Response.Dispose();
}

/// <summary>
/// Sequential GET over candidate URLs (mirror first, then GitHub). One pass, no retry loop.
/// Caller owns and must dispose the successful response.
///
/// <see cref="GetAllAsync"/> exists for the metadata documents (catalog / update manifest) where
/// a mirror serving a stale copy answers 200 and would otherwise shadow the newer GitHub copy.
/// Binary downloads stay on the sequential path — for those the mirror is simply the cheaper
/// route to the same sha256-verified bytes.
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

    /// <summary>
    /// Longest a straggler is given once some other candidate has already answered in full. Waiting
    /// for all of them unconditionally would put a blackholed origin's connect timeout in front of
    /// every refresh for players whose mirror answers instantly, and would hold the caller's busy flag
    /// for the duration. The cost is that an origin slower than this loses a freshness comparison it
    /// would have won; the next refresh asks again.
    /// </summary>
    public static readonly TimeSpan StragglerGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Ceiling on one pass while nothing has answered yet. The catalog client runs with an infinite
    /// <see cref="HttpClient.Timeout"/> on purpose (a finite one would cap catalog size), so without
    /// this a host that accepts the connection and then says nothing would never return.
    ///
    /// It is deliberately far longer than any metadata document needs: <see cref="GetAllAsync"/>
    /// serves catalog and update manifests only, so this bounds a hang rather than a size. Mod
    /// binaries go through <see cref="GetAsync"/> and keep their per-read idle timeout instead.
    /// </summary>
    public static readonly TimeSpan AllCandidatesTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Fetches every candidate in parallel and returns each success, in candidate order.
    /// Throws the same aggregate <see cref="HttpRequestException"/> as <see cref="GetAsync"/> when
    /// none succeed. The caller owns and must dispose every returned response.
    /// </summary>
    public static Task<IReadOnlyList<RemoteFetchResult>> GetAllAsync(
        HttpClient http,
        IReadOnlyList<Uri> candidates,
        CancellationToken ct = default) =>
        GetAllAsync(http, candidates, StragglerGrace, AllCandidatesTimeout, ct);

    public static async Task<IReadOnlyList<RemoteFetchResult>> GetAllAsync(
        HttpClient http,
        IReadOnlyList<Uri> candidates,
        TimeSpan stragglerGrace,
        TimeSpan allCandidatesTimeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
            throw new ArgumentException("At least one candidate URL is required.", nameof(candidates));

        using var abandon = CancellationTokenSource.CreateLinkedTokenSource(ct);
        abandon.CancelAfter(allCandidatesTimeout);

        var order = new Dictionary<Task<(RemoteFetchResult? Result, string Failure)>, int>(candidates.Count);
        var pending = new List<Task<(RemoteFetchResult? Result, string Failure)>>(candidates.Count);
        for (var i = 0; i < candidates.Count; i++)
        {
            var attempt = TryGetOneAsync(http, candidates[i], abandon.Token, ct);
            order[attempt] = i;
            pending.Add(attempt);
        }

        var results = new SortedList<int, RemoteFetchResult>();
        var failures = new List<string>(candidates.Count);
        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending).ConfigureAwait(false);
            pending.Remove(finished);

            var (result, failure) = await finished.ConfigureAwait(false);
            if (result is null)
            {
                failures.Add(failure);
                continue;
            }

            var wasFirst = results.Count == 0;
            results.Add(order[finished], result);
            if (wasFirst)
            {
                // Reschedules the existing timer, so the remaining candidates now have the grace
                // window rather than the full ceiling.
                abandon.CancelAfter(stragglerGrace);
            }
        }

        if (ct.IsCancellationRequested)
        {
            foreach (var result in results.Values)
                result.Dispose();
            ct.ThrowIfCancellationRequested();
        }

        if (results.Count > 0)
            return results.Values.ToList();

        throw new HttpRequestException(
            "All remote fetch candidates failed: " + string.Join("; ", failures));
    }

    static async Task<(RemoteFetchResult? Result, string Failure)> TryGetOneAsync(
        HttpClient http,
        Uri uri,
        CancellationToken attemptCt,
        CancellationToken callerCt)
    {
        try
        {
            // Buffered, so "succeeded" means the body is already in hand. Headers-only would hand back
            // a stream still tied to this token, and the straggler timer would then abort the winner's
            // body mid-read.
            var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseContentRead, attemptCt)
                .ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
                return (new RemoteFetchResult(uri, resp), "");

            var code = (int)resp.StatusCode;
            resp.Dispose();
            return (null, $"{uri.Host}: HTTP {code}");
        }
        catch (OperationCanceledException) when (callerCt.IsCancellationRequested)
        {
            return (null, $"{uri.Host}: canceled");
        }
        catch (OperationCanceledException) when (attemptCt.IsCancellationRequested)
        {
            return (null, $"{uri.Host}: abandoned");
        }
        catch (TaskCanceledException)
        {
            return (null, $"{uri.Host}: timeout");
        }
        catch (Exception ex)
        {
            return (null, $"{uri.Host}: {ex.Message}");
        }
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
