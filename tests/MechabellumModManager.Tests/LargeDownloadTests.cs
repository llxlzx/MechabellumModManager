using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;
using Xunit;

namespace MechabellumModManager.Tests;

/// <summary>
/// Covers the download path under the conditions a hundreds-of-GB catalog creates: files far
/// past the old 80 MB cap, links slow enough to outlast a total-duration timeout, and transfers
/// that break part way and have to resume.
///
/// The transport-level cases run against <see cref="LoopbackHttpServer"/> rather than a fake
/// handler. A fake handler ignores the cancellation token and hands back a ready-made stream, so
/// it cannot reproduce HttpClient's real timeout behaviour, range handling, or a mid-body
/// disconnect — the three things these tests exist to pin down.
/// </summary>
public sealed class LargeDownloadTests
{
    static string TempDest() =>
        Path.Combine(Path.GetTempPath(), "mmm-large-" + Guid.NewGuid().ToString("N"), "Mod.dll");

    static void Cleanup(string dest)
    {
        try { Directory.Delete(Path.GetDirectoryName(dest)!, recursive: true); }
        catch { /* cleanup */ }
    }

    static string Sha256Of(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    static byte[] RandomBytes(int count)
    {
        var payload = new byte[count];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    // ---- why the total-duration timeout had to go -------------------------------------------

    static LoopbackHttpServer TrickleServer(byte[] payload) =>
        new(_ => new LoopbackHttpServer.Reply
        {
            Payload = payload,
            ChunkSize = 1,
            DelayPerChunk = TimeSpan.FromMilliseconds(20)
        });

    /// <summary>
    /// Measured, not assumed: HttpClient.Timeout stops applying once the response headers are in.
    /// A body that takes four times the timeout to arrive still completes.
    ///
    /// This is the counterpart of
    /// <see cref="HttpClient_total_timeout_does_abort_a_slow_body_under_ResponseContentRead"/>,
    /// and together they explain why the old 30 s Timeout was never the thing capping mod size,
    /// and why removing it is nonetheless right: under ResponseHeadersRead the body had no
    /// liveness guard at all, so a stalled peer would have hung the download forever.
    /// </summary>
    [Fact]
    public async Task HttpClient_total_timeout_does_not_reach_the_body_under_ResponseHeadersRead()
    {
        var payload = RandomBytes(40);
        using var server = TrickleServer(payload);
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(200) };

        using var response = await http.GetAsync(
            server.BaseUri, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var sink = new MemoryStream();

        // 40 bytes at one byte per 20 ms needs ~800 ms, four times the timeout.
        await stream.CopyToAsync(sink);

        sink.ToArray().Should().Equal(payload);
    }

    /// <summary>Under the default completion option the same timeout does kill the same body.</summary>
    [Fact]
    public async Task HttpClient_total_timeout_does_abort_a_slow_body_under_ResponseContentRead()
    {
        using var server = TrickleServer(RandomBytes(40));
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(200) };

        var act = async () => await http.GetAsync(
            server.BaseUri, HttpCompletionOption.ResponseContentRead);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public void Default_client_has_no_total_duration_timeout()
    {
        using var client = ModCatalogService.CreateDefaultClient();

        client.Timeout.Should().Be(Timeout.InfiniteTimeSpan,
            "catalog reads buffer the body, where a finite Timeout would cap how large a catalog may be");
    }

    /// <summary>
    /// The liveness guard the body read never had: a peer that accepts the connection, sends
    /// headers, then goes quiet must fail rather than hang.
    /// </summary>
    [Fact]
    public async Task A_peer_that_stops_sending_mid_body_fails_on_the_idle_timeout()
    {
        var payload = RandomBytes(100_000);
        using var server = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply
        {
            Payload = payload,
            ChunkSize = 8192,
            // First chunk lands immediately, then nothing for far longer than the idle budget.
            DelayPerChunk = TimeSpan.FromSeconds(30)
        });

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http) { IdleTimeout = TimeSpan.FromMilliseconds(200) };
        var dest = TempDest();

        try
        {
            var started = DateTime.UtcNow;
            var act = async () => await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = server.BaseUri + "Mod.dll"
                },
                dest);

            await act.Should().ThrowAsync<HttpRequestException>();
            DateTime.UtcNow.Subtract(started).Should().BeLessThan(TimeSpan.FromSeconds(20),
                "the idle timeout must cut in long before the peer's own 30 s silence ends");
            File.Exists(dest).Should().BeFalse();
        }
        finally
        {
            Cleanup(dest);
        }
    }

    // ---- size bounds come from the catalog, not a constant ----------------------------------

    [Fact]
    public async Task A_mod_far_larger_than_the_old_80MB_cap_downloads()
    {
        var payload = RandomBytes(100 * 1024 * 1024);
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, payload));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Big.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength
                },
                dest);

            new FileInfo(dest).Length.Should().Be(payload.LongLength);
        }
        finally
        {
            Cleanup(dest);
        }
    }

    [Fact]
    public async Task A_response_longer_than_the_catalog_size_is_rejected()
    {
        var payload = RandomBytes(4096);
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, payload));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            var act = async () => await svc.DownloadModAsync(
                new CatalogMod { File = "mods/x/Mod.dll", Sha256 = Sha256Of(payload), Size = 1024 },
                dest);

            await act.Should().ThrowAsync<InvalidOperationException>();
            File.Exists(dest).Should().BeFalse();
            File.Exists(dest + ".part").Should().BeFalse();
        }
        finally
        {
            Cleanup(dest);
        }
    }

    [Fact]
    public async Task A_response_shorter_than_the_catalog_size_is_rejected()
    {
        var payload = RandomBytes(512);
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, payload));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            var act = async () => await svc.DownloadModAsync(
                new CatalogMod { File = "mods/x/Mod.dll", Sha256 = Sha256Of(payload), Size = 4096 },
                dest);

            await act.Should().ThrowAsync<InvalidOperationException>();
            File.Exists(dest).Should().BeFalse();
        }
        finally
        {
            Cleanup(dest);
        }
    }

    // ---- resume -----------------------------------------------------------------------------

    [Fact]
    public async Task A_transfer_that_dies_mid_body_resumes_from_where_it_stopped()
    {
        var payload = RandomBytes(200_000);
        using var server = new LoopbackHttpServer(request => new LoopbackHttpServer.Reply
        {
            Payload = payload,
            ChunkSize = 8192,
            // The first, rangeless attempt is cut short; the resume that follows runs to the end.
            AbortAfterBytes = request.RangeFrom is null ? 40_000 : null
        });

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = server.BaseUri + "Mod.dll"
                },
                dest);

            File.ReadAllBytes(dest).Should().Equal(payload);
            File.Exists(dest + ".part").Should().BeFalse();

            var ranges = server.Requests.Select(r => r.RangeFrom).ToList();
            ranges.Should().HaveCount(2);
            ranges[0].Should().BeNull();
            ranges[1].Should().Be(40_000, "the resume must ask for exactly the bytes still missing");
        }
        finally
        {
            Cleanup(dest);
        }
    }

    [Fact]
    public async Task A_server_that_ignores_range_restarts_the_file_instead_of_appending()
    {
        var payload = RandomBytes(120_000);
        var served = 0;
        using var server = new LoopbackHttpServer(_ =>
        {
            var first = Interlocked.Increment(ref served) == 1;
            return new LoopbackHttpServer.Reply
            {
                Payload = payload,
                ChunkSize = 8192,
                SupportsRange = false,
                AbortAfterBytes = first ? 30_000 : null
            };
        });

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = server.BaseUri + "Mod.dll"
                },
                dest);

            // Appending a second full body would have produced 150_000 bytes and a hash failure.
            File.ReadAllBytes(dest).Should().Equal(payload);
        }
        finally
        {
            Cleanup(dest);
        }
    }

    [Fact]
    public async Task A_mod_missing_from_the_mirror_is_fetched_from_the_origin()
    {
        var payload = RandomBytes(50_000);
        using var origin = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply { Payload = payload });
        using var mirror = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply
        {
            Status = HttpStatusCode.NotFound
        });

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http, mirror.BaseUri.ToString().TrimEnd('/'));
        var dest = TempDest();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = origin.BaseUri + "Mod.dll"
                },
                dest);

            File.ReadAllBytes(dest).Should().Equal(payload);
            mirror.Requests.Should().HaveCount(1, "a cold mod's 404 must not be retried three times");
            origin.Requests.Should().HaveCount(1);
        }
        finally
        {
            Cleanup(dest);
        }
    }

    // ---- integrity ---------------------------------------------------------------------------

    [Fact]
    public async Task A_catalog_entry_without_a_hash_is_refused_even_from_github()
    {
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Bytes(HttpStatusCode.OK, "legacy"u8.ToArray()));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        var act = async () => await svc.DownloadModAsync(
            new CatalogMod { File = "mods/x/Mod.dll", Size = 6 }, dest);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sha256*");
        handler.Requests.Should().BeEmpty("the refusal must land before any bytes are pulled");
    }

    [Fact]
    public async Task Content_that_fails_the_hash_leaves_neither_the_destination_nor_a_part_file()
    {
        var payload = RandomBytes(20_000);
        using var server = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply { Payload = payload });

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            var act = async () => await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = new string('a', 64),
                    Size = payload.LongLength,
                    OriginUrl = server.BaseUri + "Mod.dll"
                },
                dest);

            await act.Should().ThrowAsync<InvalidOperationException>();
            File.Exists(dest).Should().BeFalse();
            File.Exists(dest + ".part").Should().BeFalse();
        }
        finally
        {
            Cleanup(dest);
        }
    }

    [Fact]
    public void An_origin_url_over_plain_http_is_ignored_in_favour_of_the_repo_path()
    {
        var svc = new ModCatalogService();

        var candidates = svc.BuildFileCandidates(new CatalogMod
        {
            File = "mods/x/Mod.dll",
            OriginUrl = "http://cdn.example.com/Mod.dll"
        });

        candidates.Should().ContainSingle();
        candidates[0].ToString().Should()
            .Be("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/x/Mod.dll");
    }

    [Fact]
    public void An_https_origin_url_sits_behind_the_mirror_and_ahead_of_the_repo_path()
    {
        var svc = new ModCatalogService(mirrorBaseUrl: "https://mirror.example.com/m");

        var candidates = svc.BuildFileCandidates(new CatalogMod
        {
            File = "mods/x/Mod.dll",
            OriginUrl = "https://github.com/llxlzx/MechabellumMods/releases/download/mods-v1/Mod.dll"
        });

        candidates.Select(u => u.ToString()).Should().Equal(
            "https://mirror.example.com/m/MechabellumMods/mods/x/Mod.dll",
            "https://github.com/llxlzx/MechabellumMods/releases/download/mods-v1/Mod.dll",
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/x/Mod.dll");
    }

    [Fact]
    public async Task A_mis_stamped_origin_url_still_lets_the_repo_copy_serve_the_mod()
    {
        var payload = RandomBytes(30_000);
        using var repoCopy = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply { Payload = payload });
        using var deadOrigin = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply
        {
            Status = HttpStatusCode.NotFound
        });

        // Stand in for raw.githubusercontent.com by making the mirror serve the repo copy: the
        // point is that a 404 on originUrl is survivable, not which host answers.
        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http, repoCopy.BaseUri.ToString().TrimEnd('/'));
        var dest = TempDest();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = deadOrigin.BaseUri + "gone.dll"
                },
                dest);

            File.ReadAllBytes(dest).Should().Equal(payload);
        }
        finally
        {
            Cleanup(dest);
        }
    }

    // ---- compression ---------------------------------------------------------------------------

    /// <summary>
    /// A catalog that grows with the mod count is worth serving pre-compressed, which only works
    /// if the client asks for and understands gzip. Without this the mirror would hand back gzip
    /// bytes and every player would see a parse failure.
    /// </summary>
    [Fact]
    public async Task A_gzip_encoded_catalog_is_decompressed_transparently()
    {
        const string json = """
            {
              "updatedAt": "2026-09-08",
              "mods": [
                { "id": "a", "name": "A", "file": "mods/a/A.dll", "sha256": "aa", "size": 1 },
                { "id": "b", "name": "B", "file": "mods/b/B.dll", "sha256": "bb", "size": 2 }
              ]
            }
            """;

        var raw = System.Text.Encoding.UTF8.GetBytes(json);
        using var server = new LoopbackHttpServer(_ => LoopbackHttpServer.Gzipped(raw));

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http, server.BaseUri.ToString().TrimEnd('/'));

        var root = await svc.FetchCatalogAsync();

        root.Mods.Should().HaveCount(2);
        root.Mods[0].Size.Should().Be(1);
    }

    /// <summary>
    /// The size checks must survive a compressed body. The handler strips Content-Length when it
    /// decompresses, so the header comparison has to stand down and the byte count has to decide.
    /// </summary>
    [Fact]
    public async Task A_gzip_encoded_mod_download_is_still_checked_against_the_catalog_size()
    {
        var payload = RandomBytes(40_000);
        using var server = new LoopbackHttpServer(_ => LoopbackHttpServer.Gzipped(payload));

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http);
        var dest = TempDest();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = server.BaseUri + "Mod.dll"
                },
                dest);

            File.ReadAllBytes(dest).Should().Equal(payload);
        }
        finally
        {
            Cleanup(dest);
        }
    }

    // ---- progress ----------------------------------------------------------------------------

    [Fact]
    public async Task Progress_ends_at_the_catalog_declared_total()
    {
        var payload = RandomBytes(300_000);
        using var server = new LoopbackHttpServer(_ => new LoopbackHttpServer.Reply
        {
            Payload = payload,
            ChunkSize = 16384
        });

        using var http = ModCatalogService.CreateDefaultClient();
        var svc = new ModCatalogService(http);
        var dest = TempDest();
        var seen = new List<DownloadProgress>();

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Sha256 = Sha256Of(payload),
                    Size = payload.LongLength,
                    OriginUrl = server.BaseUri + "Mod.dll"
                },
                dest,
                new Progress<DownloadProgress>(p => { lock (seen) seen.Add(p); }));

            // Progress<T> posts asynchronously, so the tail may still be in flight.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                lock (seen)
                {
                    if (seen.Any(p => p.BytesDownloaded == payload.LongLength))
                        break;
                }
                await Task.Delay(20);
            }

            lock (seen)
            {
                seen.Should().NotBeEmpty();
                seen.Should().OnlyContain(p => p.TotalBytes == payload.LongLength);
                seen.Select(p => p.BytesDownloaded).Should().BeInAscendingOrder();
                seen.Last().BytesDownloaded.Should().Be(payload.LongLength);
                seen.Last().Fraction.Should().Be(1d);
            }
        }
        finally
        {
            Cleanup(dest);
        }
    }
}
