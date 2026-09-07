using System.Net;
using System.Net.Http;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class ModCatalogServiceTests
{
    const string SampleCatalogJson = """
        {
          "updatedAt": "2026-09-03",
          "mods": [
            {
              "id": "cam",
              "name": "Cam HUD",
              "author": "巴巴",
              "version": "1.0.0",
              "updatedAt": "2026-09-01",
              "summary": "摄像头相关 QoL",
              "file": "mods/cam/Cam.dll",
              "preview": "mods/cam/preview.png",
              "type": "melon_mod"
            },
            {
              "id": "damage-display",
              "name": "伤害显示",
              "author": "巴巴",
              "version": "1.2.0",
              "updatedAt": "2026-08-20",
              "summary": "战斗伤害数字",
              "file": "mods/damage-display/DamageDisplay.dll",
              "type": "melon_mod"
            }
          ]
        }
        """;

    [Fact]
    public void DeserializeCatalog_reads_camelCase_fields()
    {
        var root = ModCatalogService.DeserializeCatalog(SampleCatalogJson);

        root.UpdatedAt.Should().Be("2026-09-03");
        root.Mods.Should().HaveCount(2);
        var cam = root.Mods[0];
        cam.Id.Should().Be("cam");
        cam.Name.Should().Be("Cam HUD");
        cam.Author.Should().Be("巴巴");
        cam.Version.Should().Be("1.0.0");
        cam.UpdatedAt.Should().Be("2026-09-01");
        cam.Summary.Should().Be("摄像头相关 QoL");
        cam.File.Should().Be("mods/cam/Cam.dll");
        cam.Preview.Should().Be("mods/cam/preview.png");
        cam.Type.Should().Be("melon_mod");
        root.Mods[1].Preview.Should().BeNull();
    }

    [Fact]
    public void DeserializeCatalog_reads_category_and_tags()
    {
        var json = """
        {"updatedAt":"t","mods":[{"id":"show-grid","name":"G","file":"mods/show-grid/ShowGrid.dll","category":"OverlayUI","tags":["grid","hud"]}]}
        """;
        var root = ModCatalogService.DeserializeCatalog(json);
        root.Mods[0].Category.Should().Be("OverlayUI");
        root.Mods[0].Tags.Should().Equal("grid", "hud");
    }

    [Fact]
    public void GetRawUrl_and_PreviewUrl_build_raw_github_urls()
    {
        ModCatalogService.GetRawUrl("mods/cam/preview.png").Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/preview.png");

        var cam = new CatalogMod { Preview = "mods/cam/preview.png" };
        ModCatalogService.PreviewUrl(cam).Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/preview.png");
        ModCatalogService.PreviewUrl(new CatalogMod()).Should().BeNull();
    }

    [Theory]
    [InlineData("../secrets.dll")]
    [InlineData("mods/../../other/x.dll")]
    [InlineData("https://evil.example/x.dll")]
    [InlineData("//evil.example/x.dll")]
    public void GetRawUrl_rejects_traversal_and_absolute_urls(string path)
    {
        var act = () => ModCatalogService.GetRawUrl(path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PreviewUrl_returns_null_for_traversal_paths()
    {
        ModCatalogService.PreviewUrl(new CatalogMod { Preview = "../x.png" }).Should().BeNull();
    }

    [Fact]
    public void IsInLibrary_matches_catalog_id_not_filename()
    {
        var packages = new[]
        {
            new ModPackage
            {
                Id = "cam-aaaaaaaa",
                CatalogId = "cam",
                DisplayName = "Cam",
                Files =
                {
                    new DeployableFile { RelativePathInPackage = "Mod.dll", Sha256 = "abc" }
                }
            }
        };

        ModCatalogService.IsInLibrary(packages, new CatalogMod { Id = "cam", File = "mods/cam/Mod.dll" })
            .Should().BeTrue();
        ModCatalogService.IsInLibrary(packages, new CatalogMod { Id = "other", File = "mods/other/Mod.dll" })
            .Should().BeFalse();
    }

    [Fact]
    public void IsInLibrary_matches_file_hash()
    {
        var packages = new[]
        {
            new ModPackage
            {
                Id = "imported-1",
                DisplayName = "X",
                Files = { new DeployableFile { RelativePathInPackage = "Mod.dll", Sha256 = "deadbeef" } }
            }
        };

        ModCatalogService.IsInLibrary(packages, new CatalogMod { Id = "other", File = "Mod.dll", Sha256 = "deadbeef" })
            .Should().BeTrue();
        ModCatalogService.IsInLibrary(packages, new CatalogMod { Id = "other", File = "Mod.dll", Sha256 = "ffff" })
            .Should().BeFalse();
    }

    [Fact]
    public void BuildFileUrl_uses_owner_repo_branch()
    {
        var svc = new ModCatalogService();
        var url = svc.BuildFileUrl(new CatalogMod { File = "mods/cam/Cam.dll" });
        url.ToString().Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/Cam.dll");
    }

    [Fact]
    public void GetRawUrl_and_BuildFileUrl_reject_null_or_empty_paths()
    {
        var getNull = () => ModCatalogService.GetRawUrl(null!);
        getNull.Should().Throw<ArgumentException>();

        ModCatalogService.PreviewUrl(null).Should().BeNull();

        var svc = new ModCatalogService();
        var buildEmpty = () => svc.BuildFileUrl(new CatalogMod { File = null! });
        buildEmpty.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("melon_mod", ModPackageType.MelonMod)]
    [InlineData("melon_plugin", ModPackageType.MelonPlugin)]
    [InlineData(null, ModPackageType.MelonMod)]
    public void ParsePackageType_maps_known_values(string? type, ModPackageType expected)
    {
        ModCatalogService.ParsePackageType(type).Should().Be(expected);
    }

    [Fact]
    public void DeserializeCatalog_reads_locales()
    {
        var json = """
        {
          "updatedAt":"t",
          "mods":[{
            "id":"feature-test",
            "name":"功能测试 MOD",
            "summary":"中文简介",
            "file":"mods/x.dll",
            "locales":{
              "en":{"name":"Feature Test Mod","summary":"English summary"},
              "de":{"name":"Funktionstest-Mod"}
            }
          }]
        }
        """;
        var root = ModCatalogService.DeserializeCatalog(json);
        var mod = root.Mods[0];
        mod.Locales.Should().NotBeNull();
        mod.Locales!["en"].Name.Should().Be("Feature Test Mod");
        mod.Locales["en"].Summary.Should().Be("English summary");
        mod.Locales["de"].Name.Should().Be("Funktionstest-Mod");
        mod.Locales["de"].Summary.Should().BeNull();
        CatalogLocaleResolver.ResolveName(mod, "en").Should().Be("Feature Test Mod");
        CatalogLocaleResolver.ResolveSummary(mod, "de").Should().Be("中文简介");
    }

    [Fact]
    public async Task FetchCatalogAsync_prefers_mirror_then_writes_cache()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-cat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        try
        {
            var handler = new ScriptedHttpHandler(req =>
            {
                if (req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
                    return ScriptedHttpHandler.Json(HttpStatusCode.OK, SampleCatalogJson);
                return ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "no");
            });
            using var http = new HttpClient(handler);
            var svc = new ModCatalogService(http, "https://mirror.example/m", dataRoot);

            var root = await svc.FetchCatalogAsync();

            root.Mods.Should().HaveCount(2);
            svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
            File.Exists(CatalogCache.GetPath(dataRoot)).Should().BeTrue();
            handler.Requests.Should().ContainSingle();
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public async Task FetchCatalogAsync_falls_back_to_github_when_mirror_fails()
    {
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal))
                return ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "no");
            return ScriptedHttpHandler.Json(HttpStatusCode.OK, SampleCatalogJson);
        });
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m");

        var root = await svc.FetchCatalogAsync();

        root.Mods.Should().HaveCount(2);
        svc.LastFetchSource.Should().Be(RemoteFetch.GithubSource);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public void TryLoadCachedCatalog_reads_written_cache()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        try
        {
            CatalogCache.Write(dataRoot, SampleCatalogJson);
            var svc = new ModCatalogService(dataRoot: dataRoot);
            var root = svc.TryLoadCachedCatalog();
            root.Should().NotBeNull();
            root!.Mods.Should().HaveCount(2);
            svc.LastFetchSource.Should().Be("cache");
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public void PreviewUrl_with_mirror_lists_mirror_first()
    {
        var cam = new CatalogMod { Preview = "mods/cam/preview.png" };
        var urls = ModCatalogService.GetPreviewCandidateUrls(cam, "https://mirror.example/m");
        urls[0].Should().Be("https://mirror.example/m/MechabellumMods/mods/cam/preview.png");
        urls[1].Should().Be("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/preview.png");
        ModCatalogService.PreviewUrl(cam, "https://mirror.example/m").Should().Be(urls[0]);
    }
}
