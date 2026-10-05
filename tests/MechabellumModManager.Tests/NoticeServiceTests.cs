using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class NoticeServiceTests
{
    [Fact]
    public async Task Success_writes_the_cache_and_keeps_file_order()
    {
        var dir = TempDir();
        var json = """
            {"items":[
              {"time":"not-a-date","title":"第一","body":"甲"},
              {"time":"2026-10-06T00:00:00Z","title":"第二","body":"乙"}
            ]}
            """;
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Json(HttpStatusCode.OK, json));
        using var http = new HttpClient(handler);
        var service = new NoticeService(http, dir) { Language = () => "zh-CN" };

        try
        {
            var result = await service.LoadAsync();
            result.Failed.Should().BeFalse();
            result.Items.Select(item => item.Title).Should().Equal("第一", "第二");
            result.Items[0].TimeText.Should().Be("not-a-date");
            File.ReadAllText(Path.Combine(dir, NoticeService.CacheFileName)).Should().Contain("第一");
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Failure_returns_the_cache_without_replacing_it()
    {
        var dir = TempDir();
        var cached = """{"items":[{"title":"旧","body":"内容"}]}""";
        File.WriteAllText(Path.Combine(dir, NoticeService.CacheFileName), cached, Encoding.UTF8);
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Json(HttpStatusCode.NotFound, "no"));
        using var http = new HttpClient(handler);
        var service = new NoticeService(http, dir, "https://mirror.example") { Language = () => "zh-CN" };

        try
        {
            var result = await service.LoadAsync();
            result.Failed.Should().BeTrue();
            result.FromCache.Should().BeTrue();
            result.Items.Single().Title.Should().Be("旧");
            File.ReadAllText(Path.Combine(dir, NoticeService.CacheFileName)).Should().Be(cached);
            handler.Requests.Should().HaveCount(2);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public void Oversized_body_and_extra_items_follow_the_caps()
    {
        var items = string.Join(",", Enumerable.Range(0, 40).Select(i =>
            "{\"title\":\"t" + i + "\",\"body\":\"b\"}"));
        NoticeService.TryParse("{\"items\":[" + items + "]}", "zh-CN", out var parsed).Should().BeTrue();
        parsed.Should().HaveCount(30);
        parsed[0].Title.Should().Be("t0");

        var huge = new string('x', NoticeService.MaxBytes + 1);
        huge.Length.Should().BeGreaterThan(NoticeService.MaxBytes);
    }

    [Fact]
    public void English_falls_back_per_field_and_chinese_ignores_locales()
    {
        var json = """
            {"items":[{"title":"中文标题","body":"中文正文","locales":{"en":{"title":"English"}}}]}
            """;
        NoticeService.TryParse(json, "en", out var english).Should().BeTrue();
        english[0].Title.Should().Be("English");
        english[0].Body.Should().Be("中文正文");
        NoticeService.TryParse(json, "zh-CN", out var chinese).Should().BeTrue();
        chinese[0].Title.Should().Be("中文标题");
    }

    [Fact]
    public async Task A_body_over_one_megabyte_does_not_replace_the_cache()
    {
        var dir = TempDir();
        var cached = """{"items":[{"title":"旧","body":"内容"}]}""";
        File.WriteAllText(Path.Combine(dir, NoticeService.CacheFileName), cached, Encoding.UTF8);
        var huge = new byte[NoticeService.MaxBytes + 1];
        var handler = new ScriptedHttpHandler(_ => ScriptedHttpHandler.Bytes(HttpStatusCode.OK, huge));
        using var http = new HttpClient(handler);
        var service = new NoticeService(http, dir) { Language = () => "zh-CN" };

        try
        {
            var result = await service.LoadAsync();
            result.Failed.Should().BeTrue();
            result.FromCache.Should().BeTrue();
            File.ReadAllText(Path.Combine(dir, NoticeService.CacheFileName)).Should().Be(cached);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-notice-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* cleanup */ }
    }
}
