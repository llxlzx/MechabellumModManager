using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MechabellumModManager.Tests.Support;

/// <summary>
/// Minimal HTTP/1.1 server on a loopback port, used where a fake HttpMessageHandler cannot
/// tell the truth: range requests, bodies that die mid-transfer, and the real
/// SocketsHttpHandler timeout behaviour all live below the handler abstraction.
///
/// Every response closes its connection, so each request gets a fresh socket and the
/// recorded request list stays easy to reason about.
/// </summary>
public sealed class LoopbackHttpServer : IDisposable
{
    public sealed record Request(string Method, string Path, long? RangeFrom);

    /// <summary>How the server should answer one request.</summary>
    public sealed class Reply
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        /// <summary>Full-resource bytes. A range request is served as a slice of this.</summary>
        public byte[] Payload { get; init; } = Array.Empty<byte>();

        /// <summary>Drop the connection after writing this many body bytes, simulating a broken transfer.</summary>
        public int? AbortAfterBytes { get; init; }

        public int ChunkSize { get; init; } = 64 * 1024;

        public TimeSpan DelayPerChunk { get; init; } = TimeSpan.Zero;

        /// <summary>When false the server ignores Range and always returns the whole body with 200.</summary>
        public bool SupportsRange { get; init; } = true;

        /// <summary>Omit Content-Length, as a chunked or proxied mirror response might.</summary>
        public bool OmitContentLength { get; init; }

        /// <summary>
        /// Value for the Content-Encoding header, e.g. "gzip". <see cref="Payload"/> must already
        /// hold the encoded bytes; the server does not compress anything itself.
        /// </summary>
        public string? ContentEncoding { get; init; }
    }

    /// <summary>Builds a reply whose payload is the gzip of <paramref name="raw"/>.</summary>
    public static Reply Gzipped(byte[] raw)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(
                   buffer, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(raw);
        }

        return new Reply { Payload = buffer.ToArray(), ContentEncoding = "gzip" };
    }

    readonly TcpListener _listener;
    readonly Func<Request, Reply> _respond;
    readonly CancellationTokenSource _cts = new();
    readonly List<Request> _requests = new();
    readonly object _gate = new();

    public LoopbackHttpServer(Func<Request, Reply> respond)
    {
        _respond = respond ?? throw new ArgumentNullException(nameof(respond));
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _ = Task.Run(AcceptLoopAsync);
    }

    public Uri BaseUri { get; }

    public IReadOnlyList<Request> Requests
    {
        get { lock (_gate) return _requests.ToList(); }
    }

    async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { return; }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var head = await ReadHeadAsync(stream).ConfigureAwait(false);
                if (head is null)
                    return;

                var request = ParseRequest(head);
                lock (_gate) _requests.Add(request);

                var reply = _respond(request);
                await WriteReplyAsync(stream, request, reply).ConfigureAwait(false);
            }
            catch (IOException) { /* client hung up */ }
            catch (SocketException) { /* client hung up */ }
            catch (ObjectDisposedException) { /* shutting down */ }
        }
    }

    static async Task<string?> ReadHeadAsync(NetworkStream stream)
    {
        var buffer = new byte[8192];
        var head = new StringBuilder();
        while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (read <= 0)
                return null;
            head.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (head.Length > 64 * 1024)
                return null;
        }

        return head.ToString();
    }

    static Request ParseRequest(string head)
    {
        var lines = head.Split("\r\n");
        var parts = lines[0].Split(' ');
        var method = parts.Length > 0 ? parts[0] : "GET";
        var path = parts.Length > 1 ? parts[1] : "/";

        long? rangeFrom = null;
        foreach (var line in lines.Skip(1))
        {
            if (!line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = line["Range:".Length..].Trim();
            const string prefix = "bytes=";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var spec = value[prefix.Length..].Split('-')[0].Trim();
            if (long.TryParse(spec, out var from))
                rangeFrom = from;
        }

        return new Request(method, path, rangeFrom);
    }

    static async Task WriteReplyAsync(NetworkStream stream, Request request, Reply reply)
    {
        var total = reply.Payload.LongLength;
        var from = reply.SupportsRange && request.RangeFrom is long r && r > 0 && r < total ? r : 0;
        var body = reply.Payload.AsMemory((int)from);

        var status = from > 0 ? HttpStatusCode.PartialContent : reply.Status;
        var head = new StringBuilder();
        head.Append($"HTTP/1.1 {(int)status} {status}\r\n");
        if (reply.SupportsRange)
            head.Append("Accept-Ranges: bytes\r\n");
        if (from > 0)
            head.Append($"Content-Range: bytes {from}-{total - 1}/{total}\r\n");
        if (!reply.OmitContentLength)
            head.Append($"Content-Length: {body.Length}\r\n");
        if (!string.IsNullOrEmpty(reply.ContentEncoding))
            head.Append($"Content-Encoding: {reply.ContentEncoding}\r\n");
        head.Append("Content-Type: application/octet-stream\r\n");
        head.Append("Connection: close\r\n\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);

        if (request.Method == "HEAD")
            return;

        var limit = reply.AbortAfterBytes is int cap ? Math.Min(cap, body.Length) : body.Length;
        var sent = 0;
        while (sent < limit)
        {
            if (reply.DelayPerChunk > TimeSpan.Zero)
                await Task.Delay(reply.DelayPerChunk).ConfigureAwait(false);

            var count = Math.Min(reply.ChunkSize, limit - sent);
            await stream.WriteAsync(body.Slice(sent, count)).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            sent += count;
        }

        // Falling out with limit < body.Length closes the socket early, which the client sees
        // as a truncated body rather than a clean end of stream.
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* already down */ }
        _cts.Dispose();
    }
}
