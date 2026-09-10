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
    public async Task GetAllAsync_returns_every_success_in_candidate_order()
    {
        var mirror = new Uri("https://mirror.example/MechabellumMods/catalog.json");
        var github = new Uri("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json");
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Json(HttpStatusCode.OK, "{\"mods\":[]}"));
        using var http = new HttpClient(handler);

        var results = await RemoteFetch.GetAllAsync(http, new[] { mirror, github });

        try
        {
            results.Select(r => r.Used).Should().Equal(mirror, github);
            handler.Requests.Should().BeEquivalentTo(new[] { mirror, github });
        }
        finally
        {
            foreach (var result in results) result.Dispose();
        }
    }

    [Fact]
    public async Task GetAllAsync_keeps_successes_when_one_candidate_fails()
    {
        var mirror = new Uri("https://mirror.example/MechabellumMods/catalog.json");
        var github = new Uri("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json");
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri == mirror
                ? ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "missing")
                : ScriptedHttpHandler.Json(HttpStatusCode.OK, "{\"mods\":[]}"));
        using var http = new HttpClient(handler);

        var results = await RemoteFetch.GetAllAsync(http, new[] { mirror, github });

        try
        {
            results.Should().ContainSingle();
            results[0].Used.Should().Be(github);
        }
        finally
        {
            foreach (var result in results) result.Dispose();
        }
    }

    [Fact]
    public async Task GetAllAsync_all_failed_throws_with_both_hosts()
    {
        var mirror = new Uri("https://mirror.example/latest.json");
        var github = new Uri("https://github.com/llxlzx/MechabellumModManager/releases/latest/download/latest.json");
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "down"));
        using var http = new HttpClient(handler);

        var act = () => RemoteFetch.GetAllAsync(http, new[] { mirror, github });

        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.Message.Should().Contain("mirror.example");
        ex.Which.Message.Should().Contain("github.com");
        ex.Which.Message.Should().Contain("503");
    }

    /// <summary>
    /// A blackholed origin must not put its connect timeout in front of every refresh for players
    /// whose mirror answers instantly.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_abandons_a_straggler_once_another_candidate_has_answered()
    {
        var fast = new Uri("https://mirror.example/MechabellumMods/catalog.json");
        var stalled = new Uri("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json");
        var handler = new StallingHttpHandler(stalled, ScriptedHttpHandler.Json(HttpStatusCode.OK, "{\"mods\":[]}"));
        using var http = new HttpClient(handler);

        var started = DateTime.UtcNow;
        var results = await RemoteFetch.GetAllAsync(
            http,
            new[] { fast, stalled },
            stragglerGrace: TimeSpan.FromMilliseconds(150),
            allCandidatesTimeout: TimeSpan.FromSeconds(30),
            ct: default);

        try
        {
            results.Should().ContainSingle().Which.Used.Should().Be(fast);
            (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(5));
        }
        finally
        {
            foreach (var result in results) result.Dispose();
        }
    }

    /// <summary>
    /// The catalog client runs with an infinite HttpClient timeout, so the ceiling has to live here.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_gives_up_when_no_candidate_answers_within_the_ceiling()
    {
        var stalled = new Uri("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json");
        var handler = new StallingHttpHandler(stalled, null);
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };

        var act = () => RemoteFetch.GetAllAsync(
            http,
            new[] { stalled },
            stragglerGrace: TimeSpan.FromSeconds(1),
            allCandidatesTimeout: TimeSpan.FromMilliseconds(200),
            ct: default);

        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.Message.Should().Contain("abandoned");
    }

    [Fact]
    public async Task GetAllAsync_propagates_caller_cancellation()
    {
        var stalled = new Uri("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/catalog.json");
        var handler = new StallingHttpHandler(stalled, null);
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var act = () => RemoteFetch.GetAllAsync(http, new[] { stalled }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetAllAsync_rejects_empty_candidate_list()
    {
        using var http = new HttpClient(new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK, "{}")));

        var act = () => RemoteFetch.GetAllAsync(http, Array.Empty<Uri>());

        await act.Should().ThrowAsync<ArgumentException>();
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

    /// <summary>Hangs one URI until its token is cancelled; answers the rest immediately.</summary>
    sealed class StallingHttpHandler : HttpMessageHandler
    {
        readonly Uri _stalled;
        readonly HttpResponseMessage? _otherwise;

        public StallingHttpHandler(Uri stalled, HttpResponseMessage? otherwise)
        {
            _stalled = stalled;
            _otherwise = otherwise;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri == _stalled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable");
            }

            return _otherwise ?? ScriptedHttpHandler.Json(HttpStatusCode.OK, "{}");
        }
    }
}
