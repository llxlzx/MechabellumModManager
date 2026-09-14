# Scale-Friendly Update Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 60s full parallel catalog refetch with Hot / Warm / Cold freshness so day-to-day mirror traffic stays quiet while post-publish discovery still lands within about 15 minutes.

**Architecture:** Keep existing trigger points (startup check, switch to Library, manual refresh). Introduce a small catalog-cache sidecar for ETag, a conditional GET helper on `RemoteFetch`, and a smart fetch entry on `ModCatalogService`. `MainViewModel` silent paths call smart fetch; manual refresh always forces Cold (`GetAllAsync` unchanged).

**Tech Stack:** .NET 8 WPF, `HttpClient` / `HttpRequestMessage`, xUnit + FluentAssertions, existing `ScriptedHttpHandler` / `MainViewModelFixture`.

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-13-scale-friendly-update-detection-design.md`
- `HotCacheTtl` default = **15 minutes**; no Settings UI in this plan
- Warm **200 → Cold** (do not apply mirror-only body in phase 1)
- No background polling; no preview-image disk cache; do not change mod binary download order
- Manual「刷新目录」and「检查更新」remain immediate full fetches
- Correctness: Cold path must still let a fresher GitHub catalog beat a stale mirror
- Fail-open: probe failures may Cold once or keep cache; never leave Library enrichment blank by design
- Tests: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter <name>`

## File map

| File | Role |
|------|------|
| `src/MechabellumModManager/Services/CatalogCache.cs` | JSON cache + new ETag sidecar read/write |
| `src/MechabellumModManager/Services/RemoteFetch.cs` | Conditional GET for a single URI |
| `src/MechabellumModManager/Services/ModCatalogService.cs` | Smart fetch (Hot/Warm/Cold); private `FreshnessStamp` reused for fingerprint |
| `src/MechabellumModManager/Models/AppConfig.cs` | Optional `CatalogHotCacheMinutes` (default 15) for persistence/tests |
| `src/MechabellumModManager/ViewModels/MainViewModel.cs` | Wire silent/startup/soft-refresh to smart fetch; default interval 15m; log `[catalog warm-304\|warm-fingerprint\|cold]` (no HotSkip spam) |
| `docs/releasing.md` (short note) | COS ETag / avoid forcing full 200 every time |
| `tests/.../Support/ScriptedHttpHandler.cs` | Record request headers for `If-None-Match` asserts |
| `tests/.../CatalogFreshnessTests.cs` | New unit/flow tests for Hot/Warm/Cold |
| `tests/.../ModUpdateFlowTests.cs` | Adjust throttle expectations to new default semantics |

---

### Task 1: Catalog cache ETag sidecar + ScriptedHttpHandler headers

**Files:**
- Modify: `src/MechabellumModManager/Services/CatalogCache.cs`
- Modify: `tests/MechabellumModManager.Tests/Support/ScriptedHttpHandler.cs`
- Create: `tests/MechabellumModManager.Tests/CatalogCacheEtagTests.cs`

**Interfaces:**
- Consumes: existing `CatalogCache.Write` / `TryRead`
- Produces:
  - `CatalogCache.EtagFileName` → `"catalog-cache.etag"`
  - `CatalogCache.WriteEtag(string dataRoot, string? etag)`
  - `CatalogCache.TryReadEtag(string dataRoot, out string etag)` → `bool`
  - `ScriptedHttpHandler.RequestSnapshots` → `IReadOnlyList<RecordedRequest>` where `RecordedRequest` has `Uri Uri` and `string? IfNoneMatch`

- [ ] **Step 1: Write the failing test**

```csharp
using MechabellumModManager.Services;

public class CatalogCacheEtagTests
{
    [Fact]
    public void WriteEtag_round_trips_and_empty_clears()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-etag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CatalogCache.WriteEtag(root, "\"abc\"");
            CatalogCache.TryReadEtag(root, out var etag).Should().BeTrue();
            etag.Should().Be("\"abc\"");

            CatalogCache.WriteEtag(root, null);
            CatalogCache.TryReadEtag(root, out _).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ok */ }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~CatalogCacheEtagTests -v n`  
Expected: FAIL (missing `WriteEtag` / `TryReadEtag`)

- [ ] **Step 3: Implement sidecar + handler recording**

In `CatalogCache.cs` add:

```csharp
public const string EtagFileName = "catalog-cache.etag";

public static string GetEtagPath(string dataRoot) =>
    Path.Combine(dataRoot, EtagFileName);

public static void WriteEtag(string dataRoot, string? etag)
{
    if (string.IsNullOrWhiteSpace(dataRoot))
        throw new ArgumentException("Data root is required.", nameof(dataRoot));
    Directory.CreateDirectory(dataRoot);
    var path = GetEtagPath(dataRoot);
    if (string.IsNullOrWhiteSpace(etag))
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ok */ }
        return;
    }
    var tmp = path + ".tmp";
    File.WriteAllText(tmp, etag.Trim(), Encoding.UTF8);
    File.Copy(tmp, path, overwrite: true);
    try { File.Delete(tmp); } catch { /* ok */ }
}

public static bool TryReadEtag(string dataRoot, out string etag)
{
    etag = "";
    if (string.IsNullOrWhiteSpace(dataRoot))
        return false;
    var path = GetEtagPath(dataRoot);
    if (!File.Exists(path))
        return false;
    try
    {
        etag = File.ReadAllText(path, Encoding.UTF8).Trim();
        return !string.IsNullOrWhiteSpace(etag);
    }
    catch
    {
        etag = "";
        return false;
    }
}
```

In `ScriptedHttpHandler.cs`, keep `Requests` as today; add:

```csharp
public sealed record RecordedRequest(Uri Uri, string? IfNoneMatch);

readonly List<RecordedRequest> _recorded = new();

public IReadOnlyList<RecordedRequest> RequestSnapshots
{
    get { lock (_gate) return _recorded.ToList(); }
}

// inside SendAsync lock, after _requests.Add:
string? inm = null;
if (request.Headers.IfNoneMatch.Count > 0)
    inm = request.Headers.IfNoneMatch.ToString();
_recorded.Add(new RecordedRequest(request.RequestUri ?? new Uri("about:blank"), inm));
```

- [ ] **Step 4: Run test to verify it passes**

Run: same filter as Step 2  
Expected: PASS

---

### Task 2: RemoteFetch conditional GET

**Files:**
- Modify: `src/MechabellumModManager/Services/RemoteFetch.cs`
- Create: `tests/MechabellumModManager.Tests/RemoteFetchConditionalTests.cs`

**Interfaces:**
- Consumes: `HttpClient`, existing `RemoteFetchResult`
- Produces:
  - `RemoteFetch.GetConditionalAsync(HttpClient http, Uri uri, string? ifNoneMatch, CancellationToken ct = default)` → `Task<RemoteFetchResult>`
  - Success includes **304** and **2xx**; caller inspects `Response.StatusCode`
  - On non-success (except 304 already success): dispose and throw `HttpRequestException` with host + code (same style as `GetAsync`)

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class RemoteFetchConditionalTests
{
    [Fact]
    public async Task GetConditionalAsync_sends_IfNoneMatch_and_accepts_304()
    {
        var uri = new Uri("https://mirror.example/m/MechabellumMods/catalog.json");
        var handler = new ScriptedHttpHandler(req =>
        {
            req.Headers.IfNoneMatch.ToString().Should().Contain("abc");
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        using var http = new HttpClient(handler);

        using var result = await RemoteFetch.GetConditionalAsync(http, uri, "\"abc\"");

        result.Used.Should().Be(uri);
        result.Response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        handler.RequestSnapshots.Should().ContainSingle()
            .Which.IfNoneMatch.Should().Contain("abc");
    }

    [Fact]
    public async Task GetConditionalAsync_returns_200_body()
    {
        var uri = new Uri("https://mirror.example/m/MechabellumMods/catalog.json");
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK, "{\"updatedAt\":\"2026-09-13\",\"mods\":[]}"));
        using var http = new HttpClient(handler);

        using var result = await RemoteFetch.GetConditionalAsync(http, uri, "\"old\"");
        var json = await result.Response.Content.ReadAsStringAsync();

        result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("2026-09-13");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~RemoteFetchConditionalTests -v n`  
Expected: FAIL (`GetConditionalAsync` missing)

- [ ] **Step 3: Implement**

```csharp
public static async Task<RemoteFetchResult> GetConditionalAsync(
    HttpClient http,
    Uri uri,
    string? ifNoneMatch,
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(http);
    ArgumentNullException.ThrowIfNull(uri);

    using var req = new HttpRequestMessage(HttpMethod.Get, uri);
    if (!string.IsNullOrWhiteSpace(ifNoneMatch))
        req.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch.Trim());

    HttpResponseMessage resp;
    try
    {
        resp = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        throw;
    }
    catch (TaskCanceledException)
    {
        throw new HttpRequestException($"{uri.Host}: timeout");
    }
    catch (HttpRequestException)
    {
        throw;
    }

    if (resp.StatusCode == System.Net.HttpStatusCode.NotModified || resp.IsSuccessStatusCode)
        return new RemoteFetchResult(uri, resp);

    var code = (int)resp.StatusCode;
    resp.Dispose();
    throw new HttpRequestException($"{uri.Host}: HTTP {code}");
}
```

- [ ] **Step 4: Run tests to verify they pass**

Expected: PASS

---

### Task 3: ModCatalogService smart fetch (Hot / Warm / Cold)

**Files:**
- Modify: `src/MechabellumModManager/Services/ModCatalogService.cs`
- Modify: `src/MechabellumModManager/Models/AppConfig.cs` (add `CatalogHotCacheMinutes`)
- Create: `tests/MechabellumModManager.Tests/CatalogFreshnessTests.cs`

**Interfaces:**
- Consumes: `RemoteFetch.GetConditionalAsync`, `CatalogCache.TryReadEtag` / `WriteEtag`, existing `FetchCatalogAsync` / `FreshnessStamp` / `TryLoadCachedCatalog`
- Produces:
  - `public enum CatalogFetchKind { HotSkip, WarmNotModified, ColdApplied }`
  - `public sealed record CatalogFetchResult(CatalogFetchKind Kind, CatalogRoot? Root, string? Detail)`
    - `HotSkip`: `Root` is null (caller keeps current UI state)
    - `WarmNotModified`: `Root` is cached catalog (non-null if cache exists; if cache missing, treat as Cold)
    - `ColdApplied`: `Root` is the applied catalog
  - `public TimeSpan HotCacheTtl { get; set; } = TimeSpan.FromMinutes(15);`
  - `public DateTimeOffset? LastCatalogAppliedUtc { get; private set; }` — set on Cold apply and WarmNotModified
  - `public string? LastCatalogEtag { get; private set; }`
  - `public async Task<CatalogFetchResult> FetchCatalogSmartAsync(bool forceCold, CancellationToken ct = default)`
  - Keep `FetchCatalogAsync` as **always Cold** (extract shared apply path); smart path calls it for Cold
  - After successful Cold write of JSON, also `WriteEtag` from response `Headers.ETag` when present; clear etag sidecar when absent
  - Make `FreshnessStamp` accessible as `internal` or `public static` for fingerprint compare in Warm fallback (same algorithm as today)

**Warm algorithm (exact):**
1. If `!forceCold` and `LastCatalogAppliedUtc` is within `HotCacheTtl` → `HotSkip`
2. Else if mirror URI exists and `TryReadEtag` (or `LastCatalogEtag`) has value → `GetConditionalAsync(mirror, etag)`
   - 304 → touch `LastCatalogAppliedUtc`, return `WarmNotModified` + `TryLoadCachedCatalog()` (if null → Cold)
   - 200 → dispose/ignore body for apply purposes → call existing Cold `FetchCatalogAsync` (spec: 200 → Cold)
3. Else (no etag): `GetAsync` **mirror-only** candidates list of one URI (or BuildCandidates with mirror+github but only use first success from mirror URI via single-uri list)
   - Parse JSON; compare `FreshnessStamp(remote)` to `FreshnessStamp(cached)`
   - Equal → WarmNotModified + cache
   - Different / unparsable / no cache → Cold
4. Probe throws → Cold (fail-open)

- [ ] **Step 1: Write failing service-level tests**

```csharp
using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class CatalogFreshnessTests
{
    static string CatalogJson(string updatedAt) =>
        $$"""{"updatedAt":"{{updatedAt}}","mods":[]}""";

    [Fact]
    public async Task Smart_fetch_HotSkip_makes_zero_requests_inside_ttl()
    {
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-10")));
        using var http = new HttpClient(handler);
        var data = Path.Combine(Path.GetTempPath(), "mmm-hot-" + Guid.NewGuid().ToString("N"));
        var svc = new ModCatalogService(http, "https://mirror.example/m", data)
        {
            HotCacheTtl = TimeSpan.FromMinutes(15)
        };

        var cold = await svc.FetchCatalogSmartAsync(forceCold: true);
        cold.Kind.Should().Be(CatalogFetchKind.ColdApplied);
        var n = handler.Requests.Count;

        var hot = await svc.FetchCatalogSmartAsync(forceCold: false);
        hot.Kind.Should().Be(CatalogFetchKind.HotSkip);
        handler.Requests.Count.Should().Be(n);
    }

    [Fact]
    public async Task Smart_fetch_Warm_304_does_not_hit_github()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-w304-" + Guid.NewGuid().ToString("N"));
        var hitsGithub = false;
        var mode = "cold";
        var handler = new ScriptedHttpHandler(req =>
        {
            var host = req.RequestUri!.Host;
            if (host.Contains("github", StringComparison.OrdinalIgnoreCase) ||
                host.Contains("githubusercontent", StringComparison.OrdinalIgnoreCase))
            {
                hitsGithub = true;
                return ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-10"));
            }

            if (mode == "cold")
            {
                var resp = ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-10"));
                resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
                return resp;
            }

            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m", data)
        {
            HotCacheTtl = TimeSpan.FromMinutes(15)
        };

        (await svc.FetchCatalogSmartAsync(true)).Kind.Should().Be(CatalogFetchKind.ColdApplied);
        hitsGithub = false;
        mode = "warm";
        svc.HotCacheTtl = TimeSpan.Zero;

        var warm = await svc.FetchCatalogSmartAsync(false);
        warm.Kind.Should().Be(CatalogFetchKind.WarmNotModified);
        warm.Root.Should().NotBeNull();
        hitsGithub.Should().BeFalse();
        handler.RequestSnapshots.Should().Contain(r =>
            r.Uri.Host.Contains("mirror.example", StringComparison.Ordinal) &&
            r.IfNoneMatch != null && r.IfNoneMatch.Contains("v1"));
    }

    [Fact]
    public async Task Smart_fetch_Warm_200_runs_Cold_and_can_prefer_github()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-w200-" + Guid.NewGuid().ToString("N"));
        var mode = "seed";
        var handler = new ScriptedHttpHandler(req =>
        {
            var mirror = req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal);
            if (mode == "seed")
            {
                var resp = ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-01"));
                if (mirror)
                    resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"old\"");
                return resp;
            }

            if (mirror && req.Headers.IfNoneMatch.Count > 0)
            {
                var resp = ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-01"));
                resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"new\"");
                return resp;
            }

            return mirror
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-01"))
                : ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-13"));
        });
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m", data)
        {
            HotCacheTtl = TimeSpan.FromMinutes(15)
        };

        await svc.FetchCatalogSmartAsync(true);
        mode = "probe";
        svc.HotCacheTtl = TimeSpan.Zero;

        var result = await svc.FetchCatalogSmartAsync(false);
        result.Kind.Should().Be(CatalogFetchKind.ColdApplied);
        result.Root!.UpdatedAt.Should().Be("2026-09-13");
        svc.LastFetchSource.Should().Be(RemoteFetch.GithubSource);
    }

    [Fact]
    public async Task Smart_fetch_fingerprint_match_skips_github_without_etag()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-fp-" + Guid.NewGuid().ToString("N"));
        CatalogCache.Write(data, CatalogJson("2026-09-10"));
        var hitsGithub = false;
        var handler = new ScriptedHttpHandler(req =>
        {
            if (!req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
            {
                hitsGithub = true;
                return ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-10"));
            }
            return ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-10"));
        });
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m", data)
        {
            HotCacheTtl = TimeSpan.Zero
        };

        var result = await svc.FetchCatalogSmartAsync(false);
        result.Kind.Should().Be(CatalogFetchKind.WarmNotModified);
        hitsGithub.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~CatalogFreshnessTests -v n`

- [ ] **Step 3: Implement smart fetch**

Sketch (place near `FetchCatalogAsync`):

```csharp
public enum CatalogFetchKind { HotSkip, WarmNotModified, ColdApplied }

public sealed record CatalogFetchResult(CatalogFetchKind Kind, CatalogRoot? Root, string? Detail);

public TimeSpan HotCacheTtl { get; set; } = TimeSpan.FromMinutes(15);
public DateTimeOffset? LastCatalogAppliedUtc { get; private set; }
public string? LastCatalogEtag { get; private set; }

public async Task<CatalogFetchResult> FetchCatalogSmartAsync(bool forceCold, CancellationToken ct = default)
{
    if (!forceCold &&
        LastCatalogAppliedUtc is { } applied &&
        DateTimeOffset.UtcNow - applied < HotCacheTtl)
    {
        return new CatalogFetchResult(CatalogFetchKind.HotSkip, null, "hot");
    }

    if (!forceCold && MirrorBaseUrl is not null)
    {
        var mirrorUri = RemoteFetch.TryMirrorUri(MirrorBaseUrl, "MechabellumMods/catalog.json");
        if (mirrorUri is not null)
        {
            var etag = LastCatalogEtag;
            if (string.IsNullOrWhiteSpace(etag) && !string.IsNullOrWhiteSpace(DataRoot))
                CatalogCache.TryReadEtag(DataRoot, out etag);

            try
            {
                if (!string.IsNullOrWhiteSpace(etag))
                {
                    using var probe = await RemoteFetch.GetConditionalAsync(_http, mirrorUri, etag, ct)
                        .ConfigureAwait(false);
                    if (probe.Response.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        var cached = TryLoadCachedCatalog();
                        if (cached is null)
                            return await ColdAsync(ct).ConfigureAwait(false);
                        LastCatalogAppliedUtc = DateTimeOffset.UtcNow;
                        return new CatalogFetchResult(CatalogFetchKind.WarmNotModified, cached, "warm-304");
                    }
                    // 200 → Cold
                    return await ColdAsync(ct).ConfigureAwait(false);
                }

                // No etag: mirror-only GET + fingerprint
                using var mirrorOnly = await RemoteFetch.GetAsync(_http, new[] { mirrorUri }, ct)
                    .ConfigureAwait(false);
                var json = await mirrorOnly.Response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var remote = DeserializeCatalog(json);
                var cachedRoot = TryLoadCachedCatalog();
                if (cachedRoot is not null &&
                    FreshnessStamp(remote) is { } a &&
                    FreshnessStamp(cachedRoot) is { } b &&
                    a == b)
                {
                    LastCatalogAppliedUtc = DateTimeOffset.UtcNow;
                    return new CatalogFetchResult(CatalogFetchKind.WarmNotModified, cachedRoot, "warm-fingerprint");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // fall through to Cold
            }
        }
    }

    return await ColdAsync(ct).ConfigureAwait(false);
}

async Task<CatalogFetchResult> ColdAsync(CancellationToken ct)
{
    var root = await FetchCatalogAsync(ct).ConfigureAwait(false);
    LastCatalogAppliedUtc = DateTimeOffset.UtcNow;
    return new CatalogFetchResult(CatalogFetchKind.ColdApplied, root, "cold");
}
```

**Cold path ETag capture:** change `CatalogCopy` to  
`readonly record struct CatalogCopy(Uri Used, string Json, CatalogRoot Root, string? ETag)`.  
When building copies, set `ETag` from `result.Response.Headers.ETag?.Tag`. After choosing winner:

```csharp
LastCatalogEtag = chosen.ETag;
if (!string.IsNullOrWhiteSpace(DataRoot))
{
    CatalogCache.Write(DataRoot, chosen.Json);
    CatalogCache.WriteEtag(DataRoot, chosen.ETag);
}
```

Keep `FreshnessStamp` **private**; fingerprint behavior is covered only via smart-fetch tests.

In `AppConfig.cs`:

```csharp
/// <summary>Minutes catalog stays Hot before a Warm probe. Default 15. Not shown in Settings UI.</summary>
public int CatalogHotCacheMinutes { get; set; } = 15;
```

- [ ] **Step 4: Run CatalogFreshnessTests — expect PASS**

Also run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter FullyQualifiedName~ModCatalogServiceTests -v n`  
Expected: existing catalog tests still PASS (`FetchCatalogAsync` still Cold)

---

### Task 4: Wire MainViewModel silent / startup paths + logging

**Files:**
- Modify: `src/MechabellumModManager/ViewModels/MainViewModel.cs` (startup check, `SilentRefreshCatalogForLibraryAsync`, `SoftRefreshCatalogForLibraryDetailAsync`, `RefreshCatalogAsync`, config load/save for `CatalogHotCacheMinutes`)
- Modify: `tests/MechabellumModManager.Tests/ModUpdateFlowTests.cs`
- Logging: English-invariant `[catalog {detail}]` only — **no** new resx keys in this plan

**Interfaces:**
- Consumes: `ModCatalogService.FetchCatalogSmartAsync`, `CatalogFetchKind`
- Produces: `SilentCatalogRefreshInterval` default becomes **15 minutes** (keep property for tests; map from `CatalogHotCacheMinutes` on load)
- On construction / config load: `_catalog.HotCacheTtl = TimeSpan.FromMinutes(Math.Clamp(config.CatalogHotCacheMinutes, 1, 24 * 60))`

**Behavior:**
- `RefreshCatalogAsync` (manual): `FetchCatalogSmartAsync(forceCold: true)` then `ApplyCatalogRoot`
- `SilentRefreshCatalogForLibraryAsync`: replace interval gate with smart fetch:
  - Remove duplicate TTL gate **or** keep VM interval equal to service HotCacheTtl and still call smart (smart HotSkip is source of truth). Prefer: stamp `_lastSilentCatalogFetch` only on Warm/Cold network attempts; on HotSkip return immediately without clearing enrichment
  - `var outcome = await _catalog.FetchCatalogSmartAsync(false)`
  - HotSkip → log optional debug once per session max, or skip log spam — log at most when `Detail` changes; simplest: **no log on HotSkip**
  - WarmNotModified → `ApplyCatalogRoot(outcome.Root!)` only if needed for stamp consistency; enrichment already correct — calling Apply is OK if cheap
  - ColdApplied → `ApplyCatalogRoot` + `LogStaleCatalogSource`
  - AppendLog once: `$"[catalog {outcome.Detail}]"` or localized
- `RunStartupModUpdateCheckAsync`: `FetchCatalogSmartAsync(false)` first. If `HotSkip` and `CatalogMods.Count == 0`, apply `TryLoadCachedCatalog()` when present; if still no root, `FetchCatalogSmartAsync(true)`. If Warm/Cold returned a root, `ApplyCatalogRoot` as today.
- `SoftRefreshCatalogForLibraryDetailAsync`: replace `FetchCatalogAsync()` with `FetchCatalogSmartAsync(false)` (still gated on `CatalogMods.Count == 0`); on `HotSkip` return; otherwise apply root when non-null.
- Do **not** change `CheckForUpdatesAsync` (manager) — manual stays full parallel (spec)

- [ ] **Step 1: Update / add VM flow tests**

Update `A_second_switch_inside_the_throttle_window_does_not_refetch`:
- Default interval is 15m; first switch may Cold; second within window must not add requests (HotSkip)
- Keep `SilentCatalogRefreshInterval = TimeSpan.Zero` (and `_catalog.HotCacheTtl = TimeSpan.Zero`) path to force Warm/Cold on third switch

Add:

```csharp
[Fact]
public async Task Manual_refresh_force_colds_inside_hot_window()
{
    using var fx = MainViewModelFixture.CreateReady();
    var catalog = CatalogServing(version: "1.2.0", out var handler);
    var vm = fx.CreateVm(catalog: catalog);
    vm.CheckModUpdatesOnStartup = false;
    vm.SilentCatalogRefreshInterval = TimeSpan.FromMinutes(15);
    // ensure service TTL matches — CreateVm must assign catalog.HotCacheTtl from VM

    await vm.RefreshCatalogCommand.ExecuteAsync(null);
    var afterManual1 = handler.Requests.Count;
    await vm.RefreshCatalogCommand.ExecuteAsync(null);
    handler.Requests.Count.Should().BeGreaterThan(afterManual1);
}
```

- [ ] **Step 2: Run ModUpdateFlowTests — expect FAIL on interval semantics until wired**

- [ ] **Step 3: Wire MainViewModel**

Critical edits in `SilentRefreshCatalogForLibraryAsync`:

```csharp
async Task SilentRefreshCatalogForLibraryAsync()
{
    if (_checkingCatalog || _addingCatalogMod) return;

    _checkingCatalog = true;
    try
    {
        var outcome = await _catalog.FetchCatalogSmartAsync(forceCold: false).ConfigureAwait(true);
        if (outcome.Kind == CatalogFetchKind.HotSkip)
            return;

        _lastSilentCatalogFetch = DateTimeOffset.UtcNow;
        if (outcome.Root is not null)
        {
            ApplyCatalogRoot(outcome.Root);
            if (outcome.Kind == CatalogFetchKind.ColdApplied)
                LogStaleCatalogSource();
            if (SelectedLibraryMod is not null)
                UpdateLibraryModDetail();
        }

        if (outcome.Kind != CatalogFetchKind.HotSkip && !string.IsNullOrEmpty(outcome.Detail))
            AppendLog("[catalog " + outcome.Detail + "]");
    }
    catch (Exception ex)
    {
        AppendLog(string.Format(LocalizationService.T("LogModUpdateCheckFailed"), ex.Message));
    }
    finally
    {
        _checkingCatalog = false;
    }
}
```

Also replace the body of `SoftRefreshCatalogForLibraryDetailAsync` fetch line with the same smart-fetch pattern (no log required on that path).

Set default:

```csharp
public TimeSpan SilentCatalogRefreshInterval { get; set; } = TimeSpan.FromMinutes(15);
```

When assigning interval for tests that set `SilentCatalogRefreshInterval = TimeSpan.Zero`, also set `_catalog.HotCacheTtl = value` in the property setter:

```csharp
public TimeSpan SilentCatalogRefreshInterval
{
    get => _silentCatalogRefreshInterval;
    set
    {
        _silentCatalogRefreshInterval = value;
        _catalog.HotCacheTtl = value;
    }
}
```

(Or keep auto-property and sync in the two test sites explicitly — setter sync is less error-prone.)

- [ ] **Step 4: Run full related filters**

```text
dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj --filter "FullyQualifiedName~CatalogFreshnessTests|FullyQualifiedName~ModUpdateFlowTests|FullyQualifiedName~ModCatalogServiceTests|FullyQualifiedName~RemoteFetch" -v n
```

Expected: PASS

---

### Task 5: Docs ops note + final regression

**Files:**
- Modify: `docs/releasing.md` — short subsection under mirror sync
- Modify: spec status line optional (`Status: Approved — plan ready`)

- [ ] **Step 1: Add releasing note**

Add near `sync-mirror` / COS section:

```markdown
### Catalog / latest.json caching (clients ≥ scale-friendly detection)

Mirror objects should keep normal COS ETags. Avoid Cache-Control that forces every client GET to ignore validators and always download a full body. Conditional requests (`If-None-Match`) are how installed clients stay quiet between publishes.
```

- [ ] **Step 2: Full test suite**

Run: `dotnet test tests/MechabellumModManager.Tests/MechabellumModManager.Tests.csproj -v n`  
Expected: PASS

- [ ] **Step 3: Manual smoke (human)**

1. Launch manager twice within 15 minutes → log should not show repeated `cold` on every Library tab flick.  
2. Manual refresh → always fetches.  
3. After uploading a new `catalog.json` to COS, wait for Hot TTL or set test TTL=0 → Library shows updates.

---

## Out of scope (do not implement in this plan)

- Preview image disk cache (spec 二期)
- Manager startup conditional `latest.json` (no startup manager check today; manual check stays full)
- Settings UI for `CatalogHotCacheMinutes`
- Changing binary download / sha256 loop

---

## Plan self-review (2026-09-13)

### 1. Spec coverage

| Spec requirement | Task |
|------------------|------|
| Hot cache, no network inside TTL | Task 3 + Task 4 silent path |
| Warm conditional GET / 304 | Task 2 + Task 3 |
| Warm 200 → Cold | Task 3 algorithm + test |
| Fingerprint fallback without ETag | Task 3 test + impl |
| Manual refresh always Cold | Task 4 |
| Startup + Library triggers unchanged in *when*, changed in *how* | Task 4 |
| Shared clock startup vs Library | Task 3 `LastCatalogAppliedUtc` + Task 4 |
| Cold still GitHub-beats-stale-mirror | Task 3 Warm-200 test + existing ModCatalogServiceTests |
| Fail-open probe → Cold | Task 3 catch → ColdAsync |
| Logging kinds | Task 4 (`warm-304` / `warm-fingerprint` / `cold`) |
| Ops ETag note | Task 5 |
| No background polling / no preview cache / no binary-order change | Out of scope + Global Constraints |
| Manager manual check stays full | Explicit non-change in Task 4 |
| `HotCacheTtl` 15m / AppConfig optional | Task 3 AppConfig + Task 4 default |
| Soft library-detail refresh | Task 4 (added in self-review) |

### 2. Placeholder scan — fixed

- Removed incomplete Warm_304 stub and “rewrite when implementing” prose.
- Removed unfinished `var etag = chosen /* … */` fragment; replaced with concrete `CatalogCopy` ETag field.
- Removed ambiguous `InternalsVisibleTo` waffle; `FreshnessStamp` stays private.

### 3. Type / name consistency

- `CatalogFetchKind` / `CatalogFetchResult` / `FetchCatalogSmartAsync(bool forceCold)` used uniformly in Tasks 3–4.
- Log details: `warm-304`, `warm-fingerprint`, `cold` (aligned with spec spirit; no `warm-full` — 200 goes straight to `cold`).
- `SilentCatalogRefreshInterval` setter syncs `_catalog.HotCacheTtl` so tests setting `TimeSpan.Zero` cannot desync VM vs service.

### 4. Residual risks (accepted)

- COS may omit ETag on some objects → fingerprint path still saves GitHub hits when stamps match.
- Warm 200 always Cold costs one extra conditional GET before parallel fetch when content changes — intentional correctness tradeoff.
- Empty `mods:[]` catalogs are valid for service tests; VM flow tests keep full mod fixtures.
- `TryReadEtag(..., out etag!)` after failed read: implementers must only use etag when `TryReadEtag` returns true (sketch shows the correct branch structure).
