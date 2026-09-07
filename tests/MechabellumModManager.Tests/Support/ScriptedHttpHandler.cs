using System.Net;
using System.Net.Http;

namespace MechabellumModManager.Tests.Support;

public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public List<Uri> Requests { get; } = new();

    public ScriptedHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        _respond = respond ?? throw new ArgumentNullException(nameof(respond));

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body)
        };

    public static HttpResponseMessage Bytes(HttpStatusCode status, byte[] body) =>
        new(status)
        {
            Content = new ByteArrayContent(body)
        };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri ?? new Uri("about:blank"));
        return Task.FromResult(_respond(request));
    }
}
