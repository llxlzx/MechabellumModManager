using System.Net;
using System.Net.Http;

namespace MechabellumModManager.Tests.Support;

public sealed record ScriptedHttpRequestSnapshot(Uri Uri, string? IfNoneMatch);

public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    readonly object _gate = new();
    readonly List<Uri> _requests = new();
    readonly List<ScriptedHttpRequestSnapshot> _snapshots = new();

    /// <summary>Snapshot of the URIs seen so far. Parallel fetches make the live list unsafe to expose.</summary>
    public IReadOnlyList<Uri> Requests
    {
        get { lock (_gate) return _requests.ToList(); }
    }

    public IReadOnlyList<ScriptedHttpRequestSnapshot> RequestSnapshots
    {
        get { lock (_gate) return _snapshots.ToList(); }
    }

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
        // Serialized because parallel fetches let two candidates land here at once, and the
        // scripted responders keep their own counters.
        lock (_gate)
        {
            var uri = request.RequestUri ?? new Uri("about:blank");
            _requests.Add(uri);
            string? ifNone = null;
            if (request.Headers.IfNoneMatch.Count > 0)
                ifNone = request.Headers.IfNoneMatch.ToString();
            else if (request.Headers.TryGetValues("If-None-Match", out var raw))
                ifNone = string.Join(", ", raw);
            _snapshots.Add(new ScriptedHttpRequestSnapshot(uri, ifNone));
            return Task.FromResult(_respond(request));
        }
    }
}
