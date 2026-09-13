using System.Net;
using System.Net.Http;

namespace MechabellumModManager.Tests.Support;

public sealed record RecordedRequest(Uri Uri, string? IfNoneMatch);

public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    readonly object _gate = new();
    readonly List<Uri> _requests = new();
    readonly List<RecordedRequest> _recorded = new();

    /// <summary>Snapshot of the URIs seen so far. Parallel fetches make the live list unsafe to expose.</summary>
    public IReadOnlyList<Uri> Requests
    {
        get { lock (_gate) return _requests.ToList(); }
    }

    public IReadOnlyList<RecordedRequest> RequestSnapshots
    {
        get { lock (_gate) return _recorded.ToList(); }
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
            _requests.Add(request.RequestUri ?? new Uri("about:blank"));
            string? inm = null;
            if (request.Headers.IfNoneMatch.Count > 0)
                inm = request.Headers.IfNoneMatch.ToString();
            _recorded.Add(new RecordedRequest(request.RequestUri ?? new Uri("about:blank"), inm));
            return Task.FromResult(_respond(request));
        }
    }
}
