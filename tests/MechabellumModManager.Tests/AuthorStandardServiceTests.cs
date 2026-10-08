using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class AuthorStandardServiceTests
{
    static byte[] Document(string extra = "") =>
        Encoding.UTF8.GetBytes("# 标准\nminManagerVersion\nMelonMod\n" + extra.PadRight(400, '。'));

    [Fact]
    public async Task Mirror_is_tried_before_github()
    {
        var dir = TempDir();
        var handler = new ScriptedHttpHandler(request =>
        {
            var host = request.RequestUri!.Host;
            if (host == "mirror.example")
                return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Document("镜像"));
            return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Document("github"));
        });
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, dir, "https://mirror.example");

        try
        {
            var fetched = await service.FetchAsync();
            fetched.Should().NotBeNull();
            Encoding.UTF8.GetString(fetched!.Bytes).Should().Contain("镜像");
            fetched.FromCache.Should().BeFalse();
            handler.Requests.Should().ContainSingle();
            handler.Requests[0].AbsolutePath.Should().Be("/" + AuthorStandardService.RelativeKey);
            File.Exists(Path.Combine(dir, AuthorStandardService.CacheFileName)).Should().BeFalse();
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Html_from_the_mirror_falls_through_to_github()
    {
        var handler = new ScriptedHttpHandler(request =>
        {
            if (request.RequestUri!.Host == "mirror.example")
                return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Encoding.UTF8.GetBytes("<html>minManagerVersion MelonMod</html>" + new string('x', 400)));
            return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Document("github"));
        });
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, mirrorBaseUrl: "https://mirror.example");

        var fetched = await service.FetchAsync();
        Encoding.UTF8.GetString(fetched!.Bytes).Should().Contain("github");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Should().Be(AuthorStandardService.GitHubUri);
    }

    [Fact]
    public async Task GitHub_only_skips_the_mirror()
    {
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Document("github")));
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, mirrorBaseUrl: "https://mirror.example")
        {
            Route = DownloadRouteKind.GitHubOnly
        };

        await service.FetchAsync();
        handler.Requests.Should().ContainSingle().Which.Should().Be(AuthorStandardService.GitHubUri);
    }

    [Fact]
    public async Task Both_remotes_down_returns_the_cached_copy()
    {
        var dir = TempDir();
        var cached = Document("缓存");
        File.WriteAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName), cached);
        var handler = new ScriptedHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, dir, "https://mirror.example");

        try
        {
            var fetched = await service.FetchAsync();
            fetched!.FromCache.Should().BeTrue();
            fetched.Bytes.Should().Equal(cached);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task No_saved_copy_does_not_ask_the_network()
    {
        var dir = TempDir();
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Document("新")));
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, dir, "https://mirror.example");

        try
        {
            var check = await service.CheckAsync();
            check.Should().Be(AuthorStandardCheck.NoBaseline);
            handler.Requests.Should().BeEmpty();
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Same_bytes_are_not_an_update_and_the_saved_copy_stays()
    {
        var dir = TempDir();
        var saved = Document("同一份");
        File.WriteAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName), saved);
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Bytes(HttpStatusCode.OK, saved));
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, dir, "https://mirror.example");

        try
        {
            var check = await service.CheckAsync();
            check.Should().Be(AuthorStandardCheck.Current);
            File.ReadAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName)).Should().Equal(saved);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Changed_document_is_an_update_without_replacing_the_saved_copy()
    {
        var dir = TempDir();
        var saved = Document("旧");
        File.WriteAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName), saved);
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Bytes(HttpStatusCode.OK, Document("新")));
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, dir, "https://mirror.example");

        try
        {
            var check = await service.CheckAsync();
            check.Should().Be(AuthorStandardCheck.UpdateAvailable);
            File.ReadAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName)).Should().Equal(saved);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Unreadable_remote_is_not_reported_as_an_update()
    {
        var dir = TempDir();
        var saved = Document("旧");
        File.WriteAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName), saved);
        var handler = new ScriptedHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var service = new AuthorStandardService(http, dir, "https://mirror.example");

        try
        {
            var check = await service.CheckAsync();
            check.Should().Be(AuthorStandardCheck.Unreachable);
            File.ReadAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName)).Should().Equal(saved);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public void Remember_stores_the_saved_document()
    {
        var dir = TempDir();
        var service = new AuthorStandardService(dataRoot: dir);
        var saved = Document("已保存");

        try
        {
            service.Remember(saved);
            File.ReadAllBytes(Path.Combine(dir, AuthorStandardService.CacheFileName)).Should().Equal(saved);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public void Short_or_json_bodies_are_not_the_standard()
    {
        AuthorStandardService.IsDocument(Encoding.UTF8.GetBytes("# 短 minManagerVersion MelonMod")).Should().BeFalse();
        var json = Encoding.UTF8.GetBytes("{" + new string(' ', 400) + "\"minManagerVersion\":\"1.3.14\",\"type\":\"MelonMod\"}");
        AuthorStandardService.IsDocument(json).Should().BeFalse();
    }

    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-author-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, true); } catch { /* ignore */ }
    }
}
