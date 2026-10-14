using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public class UpdateCheckerEditionTests
{
    const string Manifest = """
        {
          "version": "9.9.9",
          "notes": "n",
          "setupUrl": "https://github.com/x/Setup_v9.9.9_Global.exe",
          "setupUrlCn": "https://cdn.example/Setup_v9.9.9_CN.exe",
          "setupUrlGlobal": "https://github.com/x/Setup_v9.9.9_Global.exe"
        }
        """;

    static UpdateChecker Serving(string json, AppEdition edition, string? mirror = null, DownloadRouteKind route = DownloadRouteKind.MirrorFirst)
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.AbsolutePath.EndsWith("latest.json", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, json)
                : ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "no"));
        return new UpdateChecker(new HttpClient(handler), () => "1.0.0", mirror)
        {
            Edition = edition,
            Route = route
        };
    }

    [Theory]
    [InlineData(AppEdition.Cn, "https://cdn.example/Setup_v9.9.9_CN.exe")]
    [InlineData(AppEdition.Global, "https://github.com/x/Setup_v9.9.9_Global.exe")]
    public async Task Each_edition_downloads_its_own_setup(AppEdition edition, string expected)
    {
        var result = await Serving(Manifest, edition).CheckAsync();

        result.Kind.Should().Be(UpdateCheckKind.UpdateAvailable);
        result.SetupUrl.Should().Be(expected);
    }

    [Fact]
    public async Task A_manifest_without_edition_links_falls_back_to_setupUrl()
    {
        var result = await Serving(
                """{"version":"9.9.9","setupUrl":"https://github.com/x/Setup.exe"}""", AppEdition.Cn)
            .CheckAsync();

        result.SetupUrl.Should().Be("https://github.com/x/Setup.exe");
    }

    [Fact]
    public async Task A_non_installer_edition_link_is_not_handed_to_the_downloader()
    {
        var result = await Serving(
                """{"version":"9.9.9","setupUrl":"https://github.com/x/Setup.exe","setupUrlCn":"https://example.com/page"}""",
                AppEdition.Cn)
            .CheckAsync();

        result.SetupUrl.Should().BeNull();
    }

    [Fact]
    public async Task The_download_page_follows_the_edition()
    {
        var cn = await Serving(Manifest, AppEdition.Cn, "https://mirror.example/m").CheckAsync();
        var global = await Serving(Manifest, AppEdition.Global, "https://mirror.example/m").CheckAsync();

        cn.BrowseUrl.Should().Be("https://mirror.example/m/" + EditionLinks.DownloadPagePath);
        global.BrowseUrl.Should().Be(UpdateChecker.DefaultBrowseUrl);
    }

    [Theory]
    [InlineData(AppEdition.Cn, "MechabellumModManager_Setup_v9.9.9_CN.exe")]
    [InlineData(AppEdition.Global, "MechabellumModManager_Setup_v9.9.9_Global.exe")]
    public async Task The_api_fallback_picks_the_editions_asset(AppEdition edition, string expectedName)
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host == "api.github.com"
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, """
                    {
                      "tag_name": "v9.9.9",
                      "body": "n",
                      "html_url": "https://github.com/x/releases/tag/v9.9.9",
                      "assets": [
                        { "name": "MechabellumModManager_Setup_v9.9.9_Global.exe", "browser_download_url": "https://github.com/x/MechabellumModManager_Setup_v9.9.9_Global.exe" },
                        { "name": "MechabellumModManager_Setup_v9.9.9_CN.exe", "browser_download_url": "https://github.com/x/MechabellumModManager_Setup_v9.9.9_CN.exe" }
                      ]
                    }
                    """)
                : ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "no"));
        var checker = new UpdateChecker(new HttpClient(handler), () => "1.0.0") { Edition = edition };

        var result = await checker.CheckAsync();

        result.SetupUrl.Should().EndWith("/" + expectedName);
    }
}
