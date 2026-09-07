using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class RemoteFetchTests
{
    [Fact]
    public async Task GetAsync_skips_failed_first_candidate_and_uses_second()
    {
        var first = new Uri("https://mirror.example/MechabellumMods/catalog.json");
        var second = new Uri("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json");
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri == first)
                return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "missing");
            return ScriptedHttpHandler.Json(HttpStatusCode.OK, "{\"mods\":[]}");
        });
        using var http = new HttpClient(handler);

        using var result = await RemoteFetch.GetAsync(http, new[] { first, second });

        result.Used.Should().Be(second);
        result.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await result.Response.Content.ReadAsStringAsync()).Should().Contain("mods");
        handler.Requests.Should().Equal(first, second);
        RemoteFetch.ClassifySource(result.Used).Should().Be(RemoteFetch.GithubSource);
    }

    [Fact]
    public async Task GetAsync_all_failed_throws_with_both_hosts()
    {
        var first = new Uri("https://mirror.example/latest.json");
        var second = new Uri("https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json");
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "down"));
        using var http = new HttpClient(handler);

        var act = async () =>
        {
            using var _ = await RemoteFetch.GetAsync(http, new[] { first, second });
        };

        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.Message.Should().Contain("mirror.example");
        ex.Which.Message.Should().Contain("github.com");
        ex.Which.Message.Should().Contain("503");
    }

    [Fact]
    public async Task GetAsync_records_timeout_for_canceled_request_without_caller_token()
    {
        var first = new Uri("https://slow.example/a");
        var second = new Uri("https://ok.example/b");
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri == first)
                throw new TaskCanceledException("timeout");
            return ScriptedHttpHandler.Json(HttpStatusCode.OK, "ok");
        });
        using var http = new HttpClient(handler);

        using var result = await RemoteFetch.GetAsync(http, new[] { first, second });

        result.Used.Should().Be(second);
        handler.Requests.Should().Equal(first, second);
    }

    [Fact]
    public void TryMirrorUri_empty_base_returns_null()
    {
        RemoteFetch.TryMirrorUri(null, "MechabellumMods/catalog.json").Should().BeNull();
        RemoteFetch.TryMirrorUri("  ", "MechabellumMods/catalog.json").Should().BeNull();
    }

    [Fact]
    public void TryMirrorUri_joins_without_trailing_slash()
    {
        var uri = RemoteFetch.TryMirrorUri("https://cdn.example/mirror/", "MechabellumModManager/latest.json");
        uri.Should().NotBeNull();
        uri!.ToString().Should().Be("https://cdn.example/mirror/MechabellumModManager/latest.json");
        RemoteFetch.ClassifySource(uri).Should().Be(RemoteFetch.MirrorSource);
    }

    [Fact]
    public void BuildCandidates_skips_mirror_when_unconfigured()
    {
        var github = new Uri("https://github.com/llxlzx/x/latest.json");
        var list = RemoteFetch.BuildCandidates(null, "MechabellumModManager/latest.json", github);
        list.Should().Equal(github);
    }

    [Fact]
    public void BuildCandidates_mirror_then_github()
    {
        var github = new Uri("https://github.com/llxlzx/x/latest.json");
        var list = RemoteFetch.BuildCandidates("https://cdn.example/m", "MechabellumModManager/latest.json", github);
        list.Should().HaveCount(2);
        list[0].ToString().Should().Be("https://cdn.example/m/MechabellumModManager/latest.json");
        list[1].Should().Be(github);
    }
}
