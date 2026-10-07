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
            svc.LastStaleSource.Should().BeNull();
            File.Exists(CatalogCache.GetPath(dataRoot)).Should().BeTrue();
            handler.Requests.Should().ContainSingle("a successful mirror answer is used without waiting on GitHub");
        }
        finally
        {
            try { Directory.Delete(dataRoot, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public async Task FetchCatalogAsync_uses_github_when_the_mirror_misses()
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
        handler.Requests[0].Host.Should().Contain("mirror.example");
    }

    static ModCatalogService CatalogServiceServing(string mirrorJson, string githubJson, out ScriptedHttpHandler handler, out HttpClient http)
    {
        handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, mirrorJson)
                : ScriptedHttpHandler.Json(HttpStatusCode.OK, githubJson));
        http = new HttpClient(handler);
        return new ModCatalogService(http, "https://mirror.example/m");
    }

    static string CatalogWith(string? rootUpdatedAt, string modUpdatedAt, string modVersion)
    {
        var rootLine = rootUpdatedAt is null ? "" : $"\"updatedAt\": \"{rootUpdatedAt}\",";
        return $$"""
            {
              {{rootLine}}
              "mods": [
                {
                  "id": "cam",
                  "name": "Cam HUD",
                  "version": "{{modVersion}}",
                  "updatedAt": "{{modUpdatedAt}}",
                  "file": "mods/cam/Cam.dll"
                }
              ]
            }
            """;
    }

    /// <summary>
    /// A mirror that answers is used immediately. Waiting to compare it with GitHub would add
    /// the connect budget on every mainland launch where GitHub does not answer.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_keeps_a_successful_mirror_copy()
    {
        var svc = CatalogServiceServing(
            CatalogWith("2026-09-01T00:00:00Z", "2026-09-01T00:00:00Z", "1.0.0"),
            CatalogWith("2026-09-10T00:00:00Z", "2026-09-10T00:00:00Z", "1.3.0"),
            out _,
            out var http);

        using (http)
        {
            var root = await svc.FetchCatalogAsync();

            root.Mods[0].Version.Should().Be("1.0.0");
            svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
            svc.LastStaleSource.Should().BeNull();
        }
    }

    [Fact]
    public async Task FetchCatalogAsync_keeps_github_when_both_would_be_equally_fresh()
    {
        var svc = CatalogServiceServing(
            CatalogWith("2026-09-10T00:00:00Z", "2026-09-10T00:00:00Z", "1.3.0"),
            CatalogWith("2026-09-10T00:00:00Z", "2026-09-10T00:00:00Z", "1.3.0"),
            out var handler,
            out var http);

        using (http)
        {
            await svc.FetchCatalogAsync();

            svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
            svc.LastStaleSource.Should().BeNull();
            handler.Requests.Should().Contain(u => u.Host.Contains("mirror.example"));
            handler.Requests.Should().NotContain(u => u.Host.Contains("githubusercontent"));
        }
    }

    [Fact]
    public async Task FetchCatalogAsync_keeps_a_reachable_github_copy_even_if_the_mirror_is_newer()
    {
        var svc = CatalogServiceServing(
            CatalogWith("2026-09-10T00:00:00Z", "2026-09-10T00:00:00Z", "1.3.0"),
            CatalogWith("2026-09-01T00:00:00Z", "2026-09-01T00:00:00Z", "1.0.0"),
            out var handler,
            out var http);

        using (http)
        {
            var root = await svc.FetchCatalogAsync();

            root.Mods[0].Version.Should().Be("1.3.0");
            svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
            svc.LastStaleSource.Should().BeNull();
            handler.Requests.Should().Contain(u => u.Host.Contains("mirror.example"));
        }
    }

    /// <summary>
    /// Entry stamps are not compared across hosts. The first copy that parses is the one kept.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_keeps_the_mirror_copy_when_only_its_entries_are_older()
    {
        var svc = CatalogServiceServing(
            CatalogWith("2026-09-01T00:00:00Z", "2026-09-01T00:00:00Z", "1.0.0"),
            CatalogWith("2026-09-01T00:00:00Z", "2026-09-10T00:00:00Z", "1.3.0"),
            out _,
            out var http);

        using (http)
        {
            var root = await svc.FetchCatalogAsync();

            root.Mods[0].Version.Should().Be("1.0.0");
            svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
            svc.LastStaleSource.Should().BeNull();
        }
    }

    /// <summary>
    /// A missing root stamp does not cause a second fetch. The mirror copy is kept.
    /// </summary>
    [Fact]
    public async Task FetchCatalogAsync_keeps_the_mirror_when_a_root_stamp_is_missing()
    {
        var svc = CatalogServiceServing(
            CatalogWith(null, "2026-09-01T00:00:00Z", "1.0.0"),
            CatalogWith(null, "2026-09-10T00:00:00Z", "1.3.0"),
            out _,
            out var http);

        using (http)
        {
            var root = await svc.FetchCatalogAsync();

            root.Mods[0].Version.Should().Be("1.0.0");
            svc.LastFetchSource.Should().Be(RemoteFetch.MirrorSource);
        }
    }

    [Fact]
    public async Task FetchCatalogAsync_ignores_a_malformed_copy_and_uses_the_parsable_one()
    {
        var svc = CatalogServiceServing(
            "{ this is not json",
            CatalogWith("2026-09-10T00:00:00Z", "2026-09-10T00:00:00Z", "1.3.0"),
            out _,
            out var http);

        using (http)
        {
            var root = await svc.FetchCatalogAsync();

            root.Mods[0].Version.Should().Be("1.3.0");
            svc.LastFetchSource.Should().Be(RemoteFetch.GithubSource);
        }
    }

    [Fact]
    public async Task FetchCatalogAsync_throws_when_every_copy_is_malformed()
    {
        var svc = CatalogServiceServing("{ nope", "{ also nope", out _, out var http);

        using (http)
        {
            var act = () => svc.FetchCatalogAsync();

            await act.Should().ThrowAsync<HttpRequestException>();
        }
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
    public async Task DownloadModAsync_rejects_content_that_does_not_match_catalog_sha256()
    {
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, "malicious"u8.ToArray()));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"), "Mod.dll");

        var act = async () => await svc.DownloadModAsync(
            new CatalogMod { File = "mods/x/Mod.dll", Sha256 = new string('a', 64) },
            dest);

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadModAsync_accepts_content_matching_catalog_sha256()
    {
        var payload = "trusted-mod-bytes"u8.ToArray();
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, payload));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"), "Mod.dll");
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();

        try
        {
            await svc.DownloadModAsync(new CatalogMod { File = "mods/x/Mod.dll", Sha256 = expected }, dest);

            File.ReadAllBytes(dest).Should().Equal(payload);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(dest)!, recursive: true); } catch { /* cleanup */ }
        }
    }

    /// <summary>
    /// The catalog can now come from GitHub while binaries are still tried mirror-first, so a mirror
    /// serving the previous build at the identical size slips past the size check. The next candidate
    /// has to get a turn instead of the whole download failing.
    /// </summary>
    [Fact]
    public async Task DownloadModAsync_falls_through_to_the_next_candidate_when_a_hash_does_not_match()
    {
        var wanted = "the-build-the-catalog-names"u8.ToArray();
        var stale = "a-previous-build-same-size!"u8.ToArray();
        stale.Length.Should().Be(wanted.Length, "the point of this test is a size-identical rebuild");

        var handler = new ScriptedHttpHandler(req =>
            ScriptedHttpHandler.Bytes(
                HttpStatusCode.OK,
                req.RequestUri!.Host.Contains("mirror.example", StringComparison.Ordinal) ? stale : wanted));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m");
        var dest = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"), "Mod.dll");

        try
        {
            await svc.DownloadModAsync(
                new CatalogMod
                {
                    File = "mods/x/Mod.dll",
                    Size = wanted.Length,
                    Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(wanted)).ToLowerInvariant()
                },
                dest);

            File.ReadAllBytes(dest).Should().Equal(wanted);
            svc.LastDownloadSource.Should().Be(RemoteFetch.GithubSource);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(dest)!, recursive: true); } catch { /* cleanup */ }
        }
    }

    [Fact]
    public async Task DownloadModAsync_refuses_mirror_file_without_catalog_sha256()
    {
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, "anything"u8.ToArray()));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http, "https://mirror.example/m");
        var dest = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"), "Mod.dll");

        var act = async () => await svc.DownloadModAsync(new CatalogMod { File = "mods/x/Mod.dll" }, dest);

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadModAsync_refuses_a_github_file_without_catalog_sha256_too()
    {
        // The old rule trusted anything served from a github.com host. That stopped being a
        // meaningful signal once mods outgrew the repo tree and had to come from Release assets
        // and mirrors, so the hash is now required from every source alike.
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, "legacy"u8.ToArray()));
        using var http = new HttpClient(handler);
        var svc = new ModCatalogService(http);
        var dest = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"), "Mod.dll");

        var act = async () => await svc.DownloadModAsync(new CatalogMod { File = "mods/x/Mod.dll" }, dest);

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public void PreviewUrl_with_mirror_lists_the_mirror_first()
    {
        var cam = new CatalogMod { Preview = "mods/cam/preview.png" };
        var urls = ModCatalogService.GetPreviewCandidateUrls(cam, "https://mirror.example/m");
        urls[0].Should().Be("https://mirror.example/m/MechabellumMods/mods/cam/preview.png");
        urls[1].Should().Be("https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/preview.png");
        ModCatalogService.PreviewUrl(cam, "https://mirror.example/m").Should().Be(urls[0]);
    }

    [Fact]
    public void PreviewUrl_uses_the_locale_preview_for_the_active_language()
    {
        var cam = new CatalogMod
        {
            Preview = "mods/cam/preview.png",
            Locales = new Dictionary<string, CatalogModLocale>
            {
                ["en"] = new CatalogModLocale { Preview = "mods/cam/preview.en.png" }
            }
        };
        LocalizationService.Apply("en");
        try
        {
            ModCatalogService.PreviewUrl(cam, "https://mirror.example/m")
                .Should().Be("https://mirror.example/m/MechabellumMods/mods/cam/preview.en.png");
        }
        finally
        {
            LocalizationService.Apply("zh-CN");
        }
    }
}
