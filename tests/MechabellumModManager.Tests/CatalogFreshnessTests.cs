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
        File.SetLastWriteTimeUtc(CatalogCache.GetPath(data), DateTime.UtcNow.AddHours(-13));

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
        File.SetLastWriteTimeUtc(CatalogCache.GetPath(data), DateTime.UtcNow.AddHours(-13));

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

    [Fact]
    public async Task A_recent_disk_catalog_makes_zero_requests_in_a_new_process()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-disk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        try
        {
            CatalogCache.Write(data, CatalogJson("2026-10-01"));
            var handler = new ScriptedHttpHandler(_ =>
                throw new InvalidOperationException("disk copy is fresh"));
            using var http = new HttpClient(handler);
            var svc = new ModCatalogService(http, "https://mirror.example/m", data)
            {
                HotCacheTtl = TimeSpan.Zero
            };

            var result = await svc.FetchCatalogSmartAsync(false);

            result.Kind.Should().Be(CatalogFetchKind.HotSkip);
            result.Root!.UpdatedAt.Should().Be("2026-10-01");
            handler.Requests.Should().BeEmpty();
        }
        finally
        {
            try { Directory.Delete(data, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Manual_refresh_still_contacts_the_network_when_the_disk_copy_is_fresh()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-force-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        try
        {
            CatalogCache.Write(data, CatalogJson("2026-10-01"));
            var handler = new ScriptedHttpHandler(_ =>
                ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("2026-10-06")));
            using var http = new HttpClient(handler);
            var svc = new ModCatalogService(http, "https://mirror.example/m", data);

            var result = await svc.FetchCatalogSmartAsync(forceCold: true);

            result.Kind.Should().Be(CatalogFetchKind.ColdApplied);
            result.Root!.UpdatedAt.Should().Be("2026-10-06");
            handler.Requests.Should().NotBeEmpty();
        }
        finally
        {
            try { Directory.Delete(data, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task A_stale_disk_catalog_sends_the_mirror_etag_when_github_fails()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-metag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        try
        {
            var handler = new ScriptedHttpHandler(req =>
            {
                if (!req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

                var resp = ScriptedHttpHandler.Json(HttpStatusCode.NotModified, "");
                return resp;
            });
            using var http = new HttpClient(handler);
            CatalogCache.Write(data, CatalogJson("2026-10-01"));
            CatalogCache.WriteEtag(data, "\"mirror-1\"");
            CatalogCache.WriteSource(data, RemoteFetch.MirrorSource);
            File.SetLastWriteTimeUtc(CatalogCache.GetPath(data), DateTime.UtcNow.AddHours(-13));

            var svc = new ModCatalogService(http, "https://mirror.example/m", data)
            {
                HotCacheTtl = TimeSpan.Zero
            };

            var result = await svc.FetchCatalogSmartAsync(false);

            result.Kind.Should().Be(CatalogFetchKind.WarmNotModified);
            result.Root!.UpdatedAt.Should().Be("2026-10-01");
            handler.RequestSnapshots.Should().Contain(r =>
                r.Uri.Host.Contains("mirror.example", StringComparison.Ordinal) &&
                r.IfNoneMatch != null &&
                r.IfNoneMatch.Contains("mirror-1"));
        }
        finally
        {
            try { Directory.Delete(data, recursive: true); } catch { /* best effort */ }
        }
    }
}
