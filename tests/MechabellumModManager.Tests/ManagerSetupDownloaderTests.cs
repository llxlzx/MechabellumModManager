using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class ManagerSetupDownloaderTests
{
    [Fact]
    public void SanitizeVersion_keeps_safe_chars_only()
    {
        ManagerSetupDownloader.SanitizeVersion("1.2.8").Should().Be("1.2.8");
        ManagerSetupDownloader.SanitizeVersion("../evil 1.0").Should().Be(".._evil_1.0");
        ManagerSetupDownloader.SanitizeVersion("").Should().Be("unknown");
        ManagerSetupDownloader.SanitizeVersion(null).Should().Be("unknown");
    }

    [Fact]
    public async Task DownloadAsync_rejects_non_downloadable_url()
    {
        var downloader = new ManagerSetupDownloader(new HttpClient(), Path.Combine(Path.GetTempPath(), "mmm-test-" + Guid.NewGuid().ToString("N")));
        var act = async () => await downloader.DownloadAsync(
            "https://github.com/llxlzx/MechabellumModManager/releases/latest",
            "1.2.8");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DownloadAsync_writes_exe_and_reports_progress()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var payload = Encoding.UTF8.GetBytes("fake-setup-bytes");
            var handler = new ScriptedHttpHandler(_ =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                };
                resp.Content.Headers.ContentLength = payload.Length;
                return resp;
            });
            using var http = new HttpClient(handler);
            var downloader = new ManagerSetupDownloader(http, root);
            var reports = new List<ManagerSetupDownloadProgress>();
            var progress = new Progress<ManagerSetupDownloadProgress>(p => reports.Add(p));

            var path = await downloader.DownloadAsync(
                "https://cdn.example/MechabellumModManager_Setup_v1.2.8.exe",
                "1.2.8",
                progress);

            path.Should().Be(Path.Combine(root, "1.2.8", "MechabellumModManager_Setup.exe"));
            File.ReadAllBytes(path).Should().Equal(payload);
            reports.Should().NotBeEmpty();
            reports.Last().Percent.Should().Be(100);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_cancel_deletes_partial()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new ScriptedHttpHandler(_ =>
            {
                var content = new SlowByteContent(64 * 1024, chunks: 20, delayMs: 40);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
            using var http = new HttpClient(handler);
            var downloader = new ManagerSetupDownloader(http, root);
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(80);

            var act = async () => await downloader.DownloadAsync(
                "https://cdn.example/Setup.exe",
                "1.0.0",
                cancellationToken: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            var dir = Path.Combine(root, "1.0.0");
            if (Directory.Exists(dir))
            {
                Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                    .Should().BeEmpty("partial .part and incomplete exe must be removed");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    sealed class SlowByteContent : HttpContent
    {
        readonly int _chunkSize;
        readonly int _chunks;
        readonly int _delayMs;

        public SlowByteContent(int chunkSize, int chunks, int delayMs)
        {
            _chunkSize = chunkSize;
            _chunks = chunks;
            _delayMs = delayMs;
            Headers.ContentLength = (long)_chunkSize * _chunks;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_chunkSize];
            for (var i = 0; i < _chunks; i++)
            {
                await stream.WriteAsync(buffer);
                await Task.Delay(_delayMs);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = (long)_chunkSize * _chunks;
            return true;
        }
    }

    [Fact]
    public async Task DownloadAsync_rejects_http_final_uri_after_redirect()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-dl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new RedirectToHttpHandler();
            using var http = new HttpClient(handler);
            var downloader = new ManagerSetupDownloader(http, root);

            var act = async () => await downloader.DownloadAsync(
                "https://cdn.example/Setup.exe",
                "1.0.0");

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Simulates https → http redirect by returning a response whose RequestUri is http.</summary>
    sealed class RedirectToHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var httpUri = new Uri("http://evil.example/Setup.exe");
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("x")),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, httpUri)
            };
            return Task.FromResult(resp);
        }
    }
}
