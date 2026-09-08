using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

/// <summary>
/// The contract a domestic mirror has to satisfy. These are the behaviours the mirror rollout
/// depends on and that no other test exercises together.
/// </summary>
public class MirrorContractTests
{
    [Fact]
    public void An_http_mirror_is_refused_and_logged_rather_than_silently_used()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var catalog = new ModCatalogService();
        var vm = fx.CreateVm(catalog: catalog);

        vm.MirrorBaseUrl = "http://mirror.example.com";

        catalog.MirrorBaseUrl.Should().BeNull();
        vm.LogText.Should().Contain("mirror.example.com");
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig())
            .MirrorBaseUrl.Should().BeNull();
    }

    [Fact]
    public void An_https_mirror_is_accepted_and_persisted_without_its_trailing_slash()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var catalog = new ModCatalogService();
        var vm = fx.CreateVm(catalog: catalog);

        vm.MirrorBaseUrl = "https://mirror.example.com/m/";

        catalog.MirrorBaseUrl.Should().Be("https://mirror.example.com/m");
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig())
            .MirrorBaseUrl.Should().Be("https://mirror.example.com/m");
    }

    [Fact]
    public void Mirror_paths_match_the_layout_the_bucket_has_to_publish()
    {
        var catalog = new ModCatalogService(mirrorBaseUrl: "https://mirror.example.com/m");

        catalog.BuildCatalogCandidates()[0].ToString()
            .Should().Be("https://mirror.example.com/m/MechabellumMods/catalog.json");
        catalog.BuildFileCandidates("mods/show-grid/ShowGrid.dll")[0].ToString()
            .Should().Be("https://mirror.example.com/m/MechabellumMods/mods/show-grid/ShowGrid.dll");

        var checker = new UpdateChecker(mirrorBaseUrl: "https://mirror.example.com/m");
        checker.BuildLatestJsonCandidates()[0].ToString()
            .Should().Be("https://mirror.example.com/m/MechabellumModManager/latest.json");
    }

    [Fact]
    public async Task A_mirror_download_whose_bytes_match_the_catalog_hash_is_kept()
    {
        var payload = "mirror-served-mod"u8.ToArray();
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, payload));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example.com/m");
        var dest = Path.Combine(Path.GetTempPath(), "mmm-mirror-" + Guid.NewGuid().ToString("N"), "Mod.dll");

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/show-grid/ShowGrid.dll",
                    Sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()
                },
                dest);

            svc.LastDownloadSource.Should().Be(RemoteFetch.MirrorSource);
            File.ReadAllBytes(dest).Should().Equal(payload);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(dest)!, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public void A_mirror_host_containing_github_would_be_misclassified_as_the_trusted_origin()
    {
        // Guards the naming rule in docs/国内镜像搭建.md: ClassifySource keys off the substring,
        // so a "github-mirror.*" bucket domain would bypass the hash requirement for mirrors.
        RemoteFetch.ClassifySource(new Uri("https://github-mirror.example.com/MechabellumMods/catalog.json"))
            .Should().Be(RemoteFetch.GithubSource);
        RemoteFetch.ClassifySource(new Uri("https://mmm-mirror.cos.ap-shanghai.myqcloud.com/MechabellumMods/catalog.json"))
            .Should().Be(RemoteFetch.MirrorSource);
    }
}
