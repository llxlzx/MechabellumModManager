using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;

public class InboxGrantClientTests
{
    [Fact]
    public async Task Http_base_returns_NotConfigured_without_sending()
    {
        var calls = 0;
        var handler = new AsyncHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new InboxGrantClient(new HttpClient(handler), "http://example.test");

        var grant = await client.RequestAsync("invite-plaintext-9f3a", new { id = "quick-mark" });
        var complete = await client.CompleteAsync("invite-plaintext-9f3a", "quick-mark");

        calls.Should().Be(0);
        grant.Ok.Should().BeFalse();
        grant.Error.Should().Be(DirectUploadErrorCode.NotConfigured);
        grant.Detail.Should().Be(nameof(DirectUploadErrorCode.NotConfigured));
        complete.Ok.Should().BeFalse();
        complete.Error.Should().Be(DirectUploadErrorCode.NotConfigured);
        complete.Detail.Should().Be(nameof(DirectUploadErrorCode.NotConfigured));
    }

    [Fact]
    public async Task RequestAsync_posts_grant_and_returns_first_object_key()
    {
        string? posted = null;
        var handler = new AsyncHandler(async (req, ct) =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.Scheme.Should().Be("https");
            req.RequestUri.Host.Should().Be("example.test");
            req.RequestUri.AbsolutePath.Should().Be("/v1/inbox/grant");
            req.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
            posted = await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GrantJson, Encoding.UTF8, "application/json")
            };
        });
        var client = new InboxGrantClient(new HttpClient(handler), "https://example.test");

        var result = await client.RequestAsync("invite-plaintext-9f3a", new { id = "quick-mark", kind = "single" });

        posted.Should().Contain("invite-plaintext-9f3a");
        posted.Should().Contain("quick-mark");
        result.Ok.Should().BeTrue();
        result.Error.Should().Be(DirectUploadErrorCode.None);
        result.Detail.Should().BeNull();
        result.InviteId.Should().Be("author1");
        result.Bucket.Should().Be("mmm-inbox-1312774738");
        result.Region.Should().Be("ap-shanghai");
        result.DurationSeconds.Should().Be(3600);
        result.Credentials!.TmpSecretId.Should().Be("tmp-id");
        result.Credentials.TmpSecretKey.Should().Be("tmp-secret");
        result.Credentials.SessionToken.Should().Be("tmp-token");
        result.Objects.Should().ContainSingle().Which.Key.Should().Be("inbox/author1/quick-mark/QuickMark.dll");
    }

    [Fact]
    public async Task RequestAsync_error_detail_omits_secret_and_discards_body()
    {
        const string secret = "LEAKED-SECRET-KEY";
        const string marker = "BODY-MARKER-9f3";
        var step = 0;
        var handler = new AsyncHandler((_, _) =>
        {
            step++;
            if (step == 1)
            {
                var leaked = "{\"error\":\"internal\",\"tmpSecretKey\":\"" + secret + "\",\"note\":\"" + marker + "\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(leaked, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json " + marker + " " + secret, Encoding.UTF8, "text/plain")
            });
        });
        var client = new InboxGrantClient(new HttpClient(handler), "https://example.test");

        var httpError = await client.RequestAsync("invite-plaintext-9f3a", new { id = "quick-mark" });
        var parseError = await client.RequestAsync("invite-plaintext-9f3a", new { id = "quick-mark" });

        step.Should().Be(2);
        AssertDiscarded(httpError, secret, marker);
        AssertDiscarded(parseError, secret, marker);
    }

    [Fact]
    public async Task RequestAsync_network_failure_detail_is_only_the_error_code()
    {
        const string secret = "LEAKED-SECRET-KEY";
        var handler = new AsyncHandler((_, _) => throw new HttpRequestException("boom " + secret + " BODY-MARKER-9f3"));
        var client = new InboxGrantClient(new HttpClient(handler), "https://example.test");

        var result = await client.RequestAsync("invite-plaintext-9f3a", new { id = "quick-mark" });

        result.Ok.Should().BeFalse();
        result.Error.Should().Be(DirectUploadErrorCode.Network);
        result.Detail.Should().Be(nameof(DirectUploadErrorCode.Network));
        result.Detail.Should().NotContain(secret);
        result.Credentials.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_posts_invite_code_and_id()
    {
        string? posted = null;
        var handler = new AsyncHandler(async (req, ct) =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.Scheme.Should().Be("https");
            req.RequestUri.Host.Should().Be("example.test");
            req.RequestUri.AbsolutePath.Should().Be("/v1/inbox/complete");
            posted = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"status":"complete-claimed"}""", Encoding.UTF8, "application/json")
            };
        });
        var client = new InboxGrantClient(new HttpClient(handler), "https://example.test");

        var result = await client.CompleteAsync("invite-plaintext-9f3a", "quick-mark");

        posted.Should().Contain("invite-plaintext-9f3a");
        posted.Should().Contain("quick-mark");
        result.Ok.Should().BeTrue();
        result.Error.Should().Be(DirectUploadErrorCode.None);
        result.Detail.Should().BeNull();
    }

    static void AssertDiscarded(InboxGrantResult result, string secret, string marker)
    {
        result.Ok.Should().BeFalse();
        result.Error.Should().Be(DirectUploadErrorCode.Internal);
        result.Detail.Should().Be(nameof(DirectUploadErrorCode.Internal));
        result.Detail.Should().NotContain(secret);
        result.Detail.Should().NotContain(marker);
        result.Credentials.Should().BeNull();
        result.Objects.Should().BeEmpty();
    }

    const string GrantJson =
        """
        {
          "ok": true,
          "inviteId": "author1",
          "bucket": "mmm-inbox-1312774738",
          "region": "ap-shanghai",
          "durationSeconds": 3600,
          "credentials": {
            "tmpSecretId": "tmp-id",
            "tmpSecretKey": "tmp-secret",
            "sessionToken": "tmp-token"
          },
          "objects": [
            {
              "key": "inbox/author1/quick-mark/QuickMark.dll",
              "role": "dll",
              "size": 10,
              "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "main": false
            }
          ]
        }
        """;

    sealed class AsyncHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _fn;
        public AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _fn(request, cancellationToken);
    }
}
