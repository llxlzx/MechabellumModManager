using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class DownloadRouteTests
{
    [Theory]
    [InlineData("fl=1\nloc=JP\n", "JP")]
    [InlineData("loc=cn\n", "cn")]
    public void TryParseCountry_reads_the_loc_line(string body, string expected)
    {
        DownloadRoutePolicy.TryParseCountry(body, out var country).Should().BeTrue();
        country.Should().Be(expected);
    }

    [Fact]
    public void TryParseCountry_rejects_a_body_without_a_country()
    {
        DownloadRoutePolicy.TryParseCountry("ip=1.2.3.4\n", out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_non_mainland_answer_is_remembered_and_a_later_failure_stays_on_github()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-route-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var http = new HttpClient(new ScriptedHttpHandler(_ =>
                ScriptedHttpHandler.Json(HttpStatusCode.OK, "loc=JP\n")));

            var first = await DownloadRoutePolicy.ResolveAsync(
                DownloadRoutePreference.Auto, root, http, CancellationToken.None);

            first.Should().Be(DownloadRouteKind.GitHubOnly);
            File.Exists(DownloadRoutePolicy.CachePath(root)).Should().BeTrue();

            using var failing = new HttpClient(new ScriptedHttpHandler(_ =>
                throw new HttpRequestException("down")));
            var second = await DownloadRoutePolicy.ResolveAsync(
                DownloadRoutePreference.Auto, root, failing, CancellationToken.None);

            second.Should().Be(DownloadRouteKind.GitHubOnly);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public async Task A_failed_probe_stays_on_the_mirror_and_is_not_remembered()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-route-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var http = new HttpClient(new ScriptedHttpHandler(_ =>
                throw new HttpRequestException("down")));

            var route = await DownloadRoutePolicy.ResolveAsync(
                DownloadRoutePreference.Auto, root, http, CancellationToken.None);

            route.Should().Be(DownloadRouteKind.MirrorFirst);
            File.Exists(DownloadRoutePolicy.CachePath(root)).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public async Task Mainland_is_not_remembered_as_overseas()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-route-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var http = new HttpClient(new ScriptedHttpHandler(_ =>
                ScriptedHttpHandler.Json(HttpStatusCode.OK, "loc=CN\n")));

            var route = await DownloadRoutePolicy.ResolveAsync(
                DownloadRoutePreference.Auto, root, http, CancellationToken.None);

            route.Should().Be(DownloadRouteKind.MirrorFirst);
            File.Exists(DownloadRoutePolicy.CachePath(root)).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public void Always_github_omits_the_mirror_even_when_one_is_configured()
    {
        var svc = new ModCatalogService(mirrorBaseUrl: "https://mirror.example/m")
        {
            Route = DownloadRouteKind.GitHubOnly
        };

        svc.BuildCatalogCandidates().Should().ContainSingle();
        svc.BuildCatalogCandidates()[0].Host.Should().Contain("githubusercontent");
        svc.BuildFileCandidates(new CatalogMod
        {
            File = "mods/cut-guide/Mechabellum.Guide.TextHover.dll",
            Size = ModCatalogService.GitRepoMaxBytes + 1,
            OriginUrl = "https://github.com/llxlzx/MechabellumMods/releases/download/mods-current/big.dll"
        }).Should().ContainSingle(u => u.Host.Contains("github.com"));
    }

    [Fact]
    public void Quiet_periods_are_six_hours()
    {
        ModCatalogService.DiskQuietPeriod.Should().Be(TimeSpan.FromHours(6));
        UpdateChecker.QuietPeriod.Should().Be(TimeSpan.FromHours(6));
    }
}
