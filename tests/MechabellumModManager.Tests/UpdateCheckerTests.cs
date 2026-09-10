using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("v1.2.0", "1.1.9", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("2.0", "1.9.9", true)]
    public void IsNewer_compares_semver_like(string remote, string local, bool expected)
    {
        UpdateChecker.IsNewer(remote, local).Should().Be(expected);
    }

    [Theory]
    [InlineData("v1.0.0+abc", "1.0.0")]
    [InlineData("1.2.3", "1.2.3")]
    public void NormalizeVersion_strips_prefix_and_metadata(string raw, string expected)
    {
        UpdateChecker.NormalizeVersion(raw).Should().Be(expected);
    }

    [Fact]
    public async Task CheckAsync_uses_mirror_latest_json_first()
    {
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
            {
                return ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"9.9.9","notes":"n","setupUrl":"https://cdn.example/Setup.exe"}""");
            }

            return ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "no");
        });
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.0.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.RemoteVersion.Should().Be("9.9.9");
        result.SetupUrl.Should().Be("https://cdn.example/Setup.exe");
        result.Source.Should().Be(RemoteFetch.MirrorSource);
        handler.Requests.Should().HaveCount(3, "every manifest source is read so a stale one cannot decide");
    }

    [Fact]
    public async Task CheckAsync_falls_back_to_github_latest_json()
    {
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
                return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "no");
            if (req.RequestUri.AbsolutePath.Contains("latest.json", StringComparison.Ordinal))
            {
                return ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.0.0","notes":"","setupUrl":"https://github.com/x"}""");
            }

            return ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "no");
        });
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.0.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpToDate);
        result.Source.Should().Be(RemoteFetch.GithubSource);
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public void BuildLatestJsonCandidates_includes_the_repo_pointer_behind_mirror_and_release()
    {
        var checker = new UpdateChecker(mirrorBaseUrl: "https://mirror.example/m");

        var candidates = checker.BuildLatestJsonCandidates();

        candidates.Should().HaveCount(3);
        candidates[0].ToString().Should().Be("https://mirror.example/m/MechabellumModManager/latest.json");
        candidates[1].Should().Be(UpdateChecker.LatestJsonUri);
        candidates[2].Should().Be(UpdateChecker.RawLatestJsonUri);
        UpdateChecker.RawLatestJsonUri.ToString().Should()
            .Be("https://raw.githubusercontent.com/llxlzx/MechabellumModManager/master/release/latest.json");
    }

    /// <summary>
    /// The repo pointer has to be able to announce a version the mirror and the Release manifest have
    /// not caught up with — that is the whole reason it is a source.
    /// </summary>
    [Fact]
    public async Task CheckAsync_finds_the_new_version_from_the_repo_pointer_when_the_others_are_behind()
    {
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("raw.githubusercontent.com", StringComparison.Ordinal))
            {
                return ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","notes":"n","setupUrl":"https://github.com/x/Setup.exe"}""");
            }

            return ScriptedHttpHandler.Json(HttpStatusCode.OK,
                """{"version":"1.2.0","notes":"old","setupUrl":"https://cdn.example/Setup.exe"}""");
        });
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.RemoteVersion.Should().Be("1.2.1");
        result.SetupUrl.Should().Be("https://github.com/x/Setup.exe");
        result.Source.Should().Be(RemoteFetch.GithubSource);
    }

    /// <summary>
    /// A tie must keep the mirror, or domestic players would be sent to GitHub for the same build.
    /// </summary>
    [Fact]
    public async Task CheckAsync_keeps_the_mirror_when_every_source_announces_the_same_version()
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","notes":"n","setupUrl":"https://cdn.example/Setup.exe","publishedAt":"2026-09-10T00:00:00Z"}""")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","notes":"n","setupUrl":"https://github.com/x/Setup.exe","publishedAt":"2026-09-10T00:00:00Z"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.SetupUrl.Should().Be("https://cdn.example/Setup.exe");
        result.Source.Should().Be(RemoteFetch.MirrorSource);
    }

    /// <summary>
    /// A mirror that omits publishedAt must not lose a same-version tie over the missing field alone.
    /// </summary>
    [Fact]
    public async Task CheckAsync_keeps_the_mirror_when_only_the_other_source_carries_a_stamp()
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://cdn.example/Setup.exe"}""")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://github.com/x/Setup.exe","publishedAt":"2026-09-10T00:00:00Z"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.SetupUrl.Should().Be("https://cdn.example/Setup.exe");
        result.Source.Should().Be(RemoteFetch.MirrorSource);
    }

    [Fact]
    public async Task CheckAsync_prefers_the_freshly_stamped_manifest_when_versions_are_equal()
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://cdn.example/Setup.exe","publishedAt":"2026-09-01T00:00:00Z"}""")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://github.com/x/Setup.exe","publishedAt":"2026-09-10T00:00:00Z"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.SetupUrl.Should().Be("https://github.com/x/Setup.exe");
        result.Source.Should().Be(RemoteFetch.GithubSource);
    }

    /// <summary>
    /// `IsNewer` cannot order an unparsable version in either direction, so such a manifest must be
    /// dropped outright — otherwise the publishedAt tie-break hands it the decision and a real update
    /// from another source is reported as "up to date".
    /// </summary>
    [Fact]
    public async Task CheckAsync_discards_a_manifest_whose_version_cannot_be_compared()
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"nightly","setupUrl":"https://cdn.example/Setup.exe","publishedAt":"2036-09-10T00:00:00Z"}""")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://github.com/x/Setup.exe"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.RemoteVersion.Should().Be("1.2.1");
        result.SetupUrl.Should().Be("https://github.com/x/Setup.exe");
    }

    /// <summary>
    /// A manifest is remote input and its URL is handed to the shell. The generic external-URL gate
    /// also allows mailto:, which has no business being an installer link.
    /// </summary>
    [Fact]
    public async Task CheckAsync_replaces_a_non_https_setup_url_with_the_releases_page()
    {
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK,
                """{"version":"1.2.1","setupUrl":"mailto:someone@example.com"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.SetupUrl.Should().Be("https://github.com/llxlzx/MechabellumModManager/releases/latest");
    }

    /// <summary>
    /// NormalizeVersion keeps only digits and dots, so text-with-digits would arrive as a perfectly
    /// orderable number. The filter has to judge the raw string.
    /// </summary>
    [Theory]
    [InlineData("1.2.1", true)]
    [InlineData("v1.2.1", true)]
    [InlineData("1.2", true)]
    [InlineData("v1.0.0+abc", true)]
    [InlineData("nightly9999", false)]
    [InlineData("1.2.0-rc.1", false)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("", false)]
    public void IsComparableVersion_only_accepts_what_IsNewer_can_order(string raw, bool expected)
    {
        UpdateChecker.IsComparableVersion(raw).Should().Be(expected);
    }

    [Fact]
    public async Task CheckAsync_discards_a_text_version_that_would_normalize_into_a_huge_number()
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"nightly9999","setupUrl":"https://cdn.example/Setup.exe"}""")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://github.com/x/Setup.exe"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.RemoteVersion.Should().Be("1.2.1");
        result.SetupUrl.Should().Be("https://github.com/x/Setup.exe");
    }

    [Fact]
    public async Task CheckAsync_ignores_a_malformed_manifest_and_uses_the_usable_one()
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, "{ not json")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK,
                    """{"version":"1.2.1","setupUrl":"https://github.com/x/Setup.exe"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.2.0", "https://mirror.example/m");

        var result = await checker.CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.RemoteVersion.Should().Be("1.2.1");
    }
}
