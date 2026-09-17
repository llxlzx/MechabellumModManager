using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;

public class DirectUploadServiceTests
{
    [Fact]
    public async Task CheckAsync_maps_401_to_invalid_invite()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/v1/auth/check");
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"invalid_invite"}""", Encoding.UTF8, "application/json")
            };
        });
        var svc = new DirectUploadService(new HttpClient(handler), "https://example.test");

        var result = await svc.CheckAsync("bad-code");

        result.Ok.Should().BeFalse();
        result.Error.Should().Be(DirectUploadErrorCode.InvalidInvite);
    }

    [Fact]
    public async Task CheckAsync_ok_returns_invite_id()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"inviteId":"author1"}""", Encoding.UTF8, "application/json")
            });
        var svc = new DirectUploadService(new HttpClient(handler), "https://example.test");

        var result = await svc.CheckAsync("good");

        result.Ok.Should().BeTrue();
        result.InviteId.Should().Be("author1");
    }

    [Fact]
    public async Task PublishAsync_forbidden_maps_error()
    {
        var dll = Path.Combine(Path.GetTempPath(), "mmm-du-" + Guid.NewGuid().ToString("N") + ".dll");
        await File.WriteAllBytesAsync(dll, new byte[] { 1, 2, 3 });
        try
        {
            var handler = new StubHandler((_, _) =>
                new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("""{"error":"forbidden_not_owner"}""", Encoding.UTF8, "application/json")
                });
            var svc = new DirectUploadService(new HttpClient(handler), "https://example.test");

            var result = await svc.PublishAsync(new DirectUploadPublishRequest
            {
                InviteCode = "c",
                Id = "foo",
                Name = "Foo",
                DllPath = dll
            });

            result.Ok.Should().BeFalse();
            result.Error.Should().Be(DirectUploadErrorCode.ForbiddenNotOwner);
        }
        finally
        {
            try { File.Delete(dll); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task PublishAsync_success_parses_body()
    {
        var dll = Path.Combine(Path.GetTempPath(), "mmm-du-" + Guid.NewGuid().ToString("N") + ".dll");
        await File.WriteAllBytesAsync(dll, new byte[] { 1, 2, 3 });
        try
        {
            var handler = new StubHandler((_, _) =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"modId":"foo","sha256":"abc","size":3,"commitSha":"c1","mirrorNote":"lag"}""",
                        Encoding.UTF8,
                        "application/json")
                });
            var svc = new DirectUploadService(new HttpClient(handler), "https://example.test");

            var result = await svc.PublishAsync(new DirectUploadPublishRequest
            {
                InviteCode = "c",
                Id = "foo",
                Name = "Foo",
                DllPath = dll
            });

            result.Ok.Should().BeTrue();
            result.ModId.Should().Be("foo");
            result.MirrorNote.Should().Be("lag");
            result.Size.Should().Be(3);
        }
        finally
        {
            try { File.Delete(dll); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task CheckAsync_empty_base_url_is_not_configured()
    {
        var svc = new DirectUploadService(new HttpClient(), "");
        var result = await svc.CheckAsync("x");
        result.Error.Should().Be(DirectUploadErrorCode.NotConfigured);
    }

    sealed class StubHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _fn;
        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_fn(request, cancellationToken));
    }
}
