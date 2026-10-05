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
    public async Task Smart_fetch_Warm_304_does_not_hit_the_mirror()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-w304-" + Guid.NewGuid().ToString("N"));
        var hitsMirror = false;
        var mode = "cold";
        var handler = new ScriptedHttpHandler(req =>
        {
            var host = req.RequestUri!.Host;
            if (host.Contains("mirror.example", StringComparison.Ordinal))
            {
                hitsMirror = true;
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
        hitsMirror = false;
        mode = "warm";
        svc.HotCacheTtl = TimeSpan.Zero;

        var warm = await svc.FetchCatalogSmartAsync(false);
        warm.Kind.Should().Be(CatalogFetchKind.WarmNotModified);
        warm.Root.Should().NotBeNull();
        hitsMirror.Should().BeFalse();
        handler.RequestSnapshots.Should().Contain(r =>
            (r.Uri.Host.Contains("github", StringComparison.OrdinalIgnoreCase) ||
             r.Uri.Host.Contains("githubusercontent", StringComparison.OrdinalIgnoreCase)) &&
            r.IfNoneMatch != null && r.IfNoneMatch.Contains("v1"));
    }

    [Fact]
    public async Task Smart_fetch_after_ttl_uses_github_and_skips_the_mirror()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-w200-" + Guid.NewGuid().ToString("N"));
        var mode = "seed";
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
                return ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-01"));

            if (mode == "seed")
            {
                var resp = ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-01"));
                resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"old\"");
                return resp;
            }

            return ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-13"));
        });
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m", data)
        {
            HotCacheTtl = TimeSpan.FromMinutes(15)
        };

        await svc.FetchCatalogSmartAsync(true);
        mode = "probe";
        svc.HotCacheTtl = TimeSpan.Zero;

        var before = handler.Requests.Count;
        var result = await svc.FetchCatalogSmartAsync(false);
        result.Kind.Should().Be(CatalogFetchKind.ColdApplied);
        result.Root!.UpdatedAt.Should().Be("2026-09-13");
        svc.LastFetchSource.Should().Be(RemoteFetch.GithubSource);
        handler.Requests.Skip(before).Should().NotContain(uri =>
            uri.Host.Contains("mirror.example", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Smart_fetch_uses_the_mirror_when_github_cannot_be_reached()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-fb-" + Guid.NewGuid().ToString("N"));
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-09-13"))
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m", data)
        {
            HotCacheTtl = TimeSpan.Zero
        };

        var result = await svc.FetchCatalogSmartAsync(false);

        result.Kind.Should().Be(CatalogFetchKind.ColdApplied);
        result.Root!.UpdatedAt.Should().Be("2026-09-13");
        svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
    }
}
