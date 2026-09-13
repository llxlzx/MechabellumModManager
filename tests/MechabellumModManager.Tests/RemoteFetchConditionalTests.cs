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
