using System.Net;
using System.Net.Http;

namespace MechabellumModManager.Services;

public sealed partial class ModCatalogService
{
    /// <summary>
    /// Reads candidates in listed order and keeps the first copy that parses. GitHub is listed
    /// first, so a player who can reach it never contacts the mirror. A candidate that does not
    /// answer within <see cref="RemoteFetch.ConnectBudget"/> is skipped with no prompt; the mirror
    /// is then tried on its own.
    /// </summary>
    public async Task<CatalogRoot> FetchCatalogAsync(CancellationToken ct = default)
    {
        var result = await FetchInOrderAsync(conditional: false, ct).ConfigureAwait(false);
        return result.Root ?? throw new HttpRequestException(
            "Catalog reached but no candidate returned parsable JSON.");
    }

    public async Task<CatalogFetchResult> FetchCatalogSmartAsync(bool forceCold, CancellationToken ct = default)
    {
        // A catalog written recently is reused as-is, including after a restart and when the
        // library page asks again. A launch does not ask GitHub or the mirror just to look.
        if (!forceCold && DiskCacheIsQuiet())
        {
            var cached = TryLoadCachedCatalog();
            if (cached is not null)
            {
                LastCatalogAppliedUtc = DateTimeOffset.UtcNow;
                return new CatalogFetchResult(CatalogFetchKind.HotSkip, cached, "disk");
            }
        }

        if (!forceCold &&
            LastCatalogAppliedUtc is { } applied &&
            DateTimeOffset.UtcNow - applied < HotCacheTtl)
        {
            return new CatalogFetchResult(CatalogFetchKind.HotSkip, null, "hot");
        }

        var result = await FetchInOrderAsync(conditional: !forceCold, ct).ConfigureAwait(false);
        LastCatalogAppliedUtc = DateTimeOffset.UtcNow;
        return result;
    }

    bool DiskCacheIsQuiet()
    {
        if (string.IsNullOrWhiteSpace(DataRoot))
            return false;
        // A new process has not applied a catalog yet, so a young file still
        // stands in for a launch. Once this session has applied one and the
        // hot window is then set to zero, that file must not keep answering.
        if (HotCacheTtl <= TimeSpan.Zero && LastCatalogAppliedUtc is not null)
            return false;
        return CatalogCache.IsYoungerThan(DataRoot, DiskQuietPeriod);
    }

    /// <summary>
    /// Sequential catalog read. An etag is sent only to the candidate whose host matches the
    /// source that produced the stored etag, so a mirror etag is never offered to GitHub
    /// (or the reverse). A mirror that is actually contacted does get its own etag.
    /// </summary>
    async Task<CatalogFetchResult> FetchInOrderAsync(bool conditional, CancellationToken ct)
    {
        LastStaleSource = null;
        var candidates = BuildCatalogCandidates();
        var failures = new List<string>(candidates.Count);

        for (var i = 0; i < candidates.Count; i++)
        {
            var uri = candidates[i];
            var budget = i == candidates.Count - 1
                ? RemoteFetch.AllCandidatesTimeout
                : RemoteFetch.ConnectBudget;
            var etag = conditional ? EtagForSource(RemoteFetch.ClassifySource(uri)) : null;

            RemoteFetchResult fetched;
            try
            {
                fetched = await RemoteFetch.GetConditionalWithinAsync(_http, uri, etag, budget, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{uri.Host}: {ex.Message}");
                continue;
            }

            using (fetched)
            {
                if (fetched.Response.StatusCode == HttpStatusCode.NotModified)
                {
                    var cached = TryLoadCachedCatalog();
                    if (cached is null)
                    {
                        failures.Add($"{uri.Host}: not modified, but no cached catalog");
                        continue;
                    }

                    LastFetchSource = RemoteFetch.ClassifySource(uri);
                    if (!string.IsNullOrWhiteSpace(DataRoot))
                    {
                        CatalogCache.Touch(DataRoot);
                        CatalogCache.WriteSource(DataRoot, LastFetchSource);
                    }

                    return new CatalogFetchResult(CatalogFetchKind.WarmNotModified, cached, "warm-304");
                }

                string json;
                CatalogRoot root;
                try
                {
                    json = await fetched.Response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    root = DeserializeCatalog(json);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add($"{uri.Host}: {ex.Message}");
                    continue;
                }

                LastFetchSource = RemoteFetch.ClassifySource(uri);
                LastCatalogEtag = fetched.Response.Headers.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(DataRoot))
                {
                    CatalogCache.Write(DataRoot, json);
                    CatalogCache.WriteEtag(DataRoot, LastCatalogEtag);
                    CatalogCache.WriteSource(DataRoot, LastFetchSource);
                }

                return new CatalogFetchResult(CatalogFetchKind.ColdApplied, root, "cold");
            }
        }

        throw new HttpRequestException(
            "All remote fetch candidates failed: " + string.Join("; ", failures));
    }

    string? EtagForSource(string source)
    {
        var remembered = LastFetchSource;
        if (string.IsNullOrWhiteSpace(remembered) &&
            !string.IsNullOrWhiteSpace(DataRoot) &&
            CatalogCache.TryReadSource(DataRoot, out var storedSource))
            remembered = storedSource;

        if (!string.Equals(remembered, source, StringComparison.Ordinal))
            return null;

        if (!string.IsNullOrWhiteSpace(LastCatalogEtag))
            return LastCatalogEtag;

        if (!string.IsNullOrWhiteSpace(DataRoot) &&
            CatalogCache.TryReadEtag(DataRoot, out var stored) &&
            !string.IsNullOrWhiteSpace(stored))
            return stored;

        return null;
    }
}
