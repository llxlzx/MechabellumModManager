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
        handler.Requests.Should().ContainSingle();
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
        handler.Requests.Should().HaveCount(2);
    }
}
