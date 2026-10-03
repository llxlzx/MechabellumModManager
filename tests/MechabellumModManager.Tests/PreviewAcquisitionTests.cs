using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public class PreviewAcquisitionTests
{
    static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task Local_hit_does_not_touch_the_network()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-acq-" + Guid.NewGuid().ToString("N"));
        var bytes = Encoding.UTF8.GetBytes("bundled-poster");
        var relative = "mods/show-dps/preview.png";
        var path = Path.Combine(bundle, "mods", "show-dps", "preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);

        var called = false;
        var handler = new ScriptedHttpHandler(_ =>
        {
            called = true;
            return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Encoding.UTF8.GetBytes("network"));
        });
        using var http = new HttpClient(handler);

        var got = await PreviewAcquisition.AcquireAsync(
            http, bundle, cacheRoot: null, relative, Hash(bytes),
            new[] { "https://mirror.example/poster.png" });

        got.Should().Equal(bytes);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task Mismatch_downloads_once_and_a_later_call_stays_local()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-acq-" + Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(Path.GetTempPath(), "pv-acq-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(bundle);
        var oldBytes = Encoding.UTF8.GetBytes("old");
        var newBytes = Encoding.UTF8.GetBytes("new-poster");
        var relative = "mods/show-dps/preview.png";
        var bundled = Path.Combine(bundle, "mods", "show-dps", "preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(bundled)!);
        File.WriteAllBytes(bundled, oldBytes);

        var downloads = 0;
        var handler = new ScriptedHttpHandler(_ =>
        {
            downloads++;
            return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, newBytes);
        });
        using var http = new HttpClient(handler);
        var urls = new[] { "https://mirror.example/poster.png" };

        var first = await PreviewAcquisition.AcquireAsync(http, bundle, cache, relative, Hash(newBytes), urls);
        var second = await PreviewAcquisition.AcquireAsync(http, bundle, cache, relative, Hash(newBytes), urls);

        first.Should().Equal(newBytes);
        second.Should().Equal(newBytes);
        downloads.Should().Be(1);
    }

    [Fact]
    public async Task Failed_download_falls_back_to_the_bundled_poster()
    {
        var bundle = Path.Combine(Path.GetTempPath(), "pv-acq-" + Guid.NewGuid().ToString("N"));
        var oldBytes = Encoding.UTF8.GetBytes("old");
        var relative = "mods/show-dps/preview.png";
        var bundled = Path.Combine(bundle, "mods", "show-dps", "preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(bundled)!);
        File.WriteAllBytes(bundled, oldBytes);
        var handler = new ScriptedHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);

        var got = await PreviewAcquisition.AcquireAsync(
            http, bundle, cacheRoot: null, relative,
            Hash(Encoding.UTF8.GetBytes("missing")),
            new[] { "https://mirror.example/poster.png" });

        got.Should().Equal(oldBytes);
    }

    [Fact]
    public async Task Wrong_hash_is_not_cached()
    {
        var cache = Path.Combine(Path.GetTempPath(), "pv-acq-cache-" + Guid.NewGuid().ToString("N"));
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Encoding.UTF8.GetBytes("tampered")));
        using var http = new HttpClient(handler);
        var wanted = Hash(Encoding.UTF8.GetBytes("real"));

        var got = await PreviewAcquisition.AcquireAsync(
            http,
            bundleRoot: Path.Combine(Path.GetTempPath(), "missing-bundle"),
            cache,
            "mods/show-dps/preview.png",
            wanted,
            new[] { "https://mirror.example/poster.png" });

        got.Should().BeNull();
        Directory.Exists(cache).Should().BeFalse();
    }
}
