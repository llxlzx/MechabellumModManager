using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class RelayClient
{
    public const string DefaultPlaceholderBaseUrl = "https://YOUR_SUBDOMAIN.workers.dev";
    public const long MaxSubmitBytes = 20L * 1024 * 1024;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    readonly HttpClient _http;
    readonly string _baseUrl;

    public RelayClient(string? baseUrl, HttpClient? http = null)
    {
        _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        _http = http ?? CreateDefaultClient();
    }

    public string BaseUrl => _baseUrl;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_baseUrl) &&
        !_baseUrl.Contains("YOUR_SUBDOMAIN", StringComparison.OrdinalIgnoreCase);

    public static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MechabellumModManager-Relay/1.0");
        return client;
    }

    public static string JoinUrl(string baseUrl, string relativePath)
    {
        var root = (baseUrl ?? "").Trim().TrimEnd('/');
        var path = (relativePath ?? "").Trim();
        if (path.StartsWith('/'))
            path = path[1..];
        if (string.IsNullOrWhiteSpace(root))
            return "/" + path;
        return root + "/" + path;
    }

    public async Task SubmitReportAsync(ReportRequest request, CancellationToken ct = default)
    {
        EnsureConfigured();
        if (!ReportRequest.TryValidate(request, out var error))
            throw new InvalidOperationException(error);

        var url = JoinUrl(_baseUrl, "/v1/reports");
        var json = JsonSerializer.Serialize(new
        {
            modId = request.ModId,
            modName = request.ModName,
            source = request.Source,
            category = request.Category.ToString().ToLowerInvariant(),
            notes = request.Notes,
            appVersion = request.AppVersion
        }, JsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Relay report failed ({(int)response.StatusCode}): {TrimBody(body)}");
    }

    public async Task SubmitModAsync(
        SubmitModRequest meta,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        EnsureConfigured();
        if (meta is null) throw new ArgumentNullException(nameof(meta));
        if (fileStream is null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(meta.Name))
            throw new InvalidOperationException("name is required");
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("only .dll is allowed");

        var url = JoinUrl(_baseUrl, "/v1/submissions");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(meta.Name), "name");
        if (!string.IsNullOrWhiteSpace(meta.Author))
            form.Add(new StringContent(meta.Author), "author");
        if (!string.IsNullOrWhiteSpace(meta.Version))
            form.Add(new StringContent(meta.Version), "version");
        if (!string.IsNullOrWhiteSpace(meta.Summary))
            form.Add(new StringContent(meta.Summary), "summary");
        form.Add(new StringContent(meta.Sha256 ?? ""), "sha256");
        form.Add(new StringContent(meta.FileSize.ToString()), "fileSize");
        if (!string.IsNullOrWhiteSpace(meta.AppVersion))
            form.Add(new StringContent(meta.AppVersion), "appVersion");

        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, "file", fileName);

        using var response = await _http.PostAsync(url, form, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Relay submit failed ({(int)response.StatusCode}): {TrimBody(body)}");
    }

    void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Relay is not configured");
    }

    static string TrimBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        body = body.Trim();
        return body.Length <= 240 ? body : body[..240] + "…";
    }
}
