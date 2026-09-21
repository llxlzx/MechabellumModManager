using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MechabellumModManager.Services;

public enum DirectUploadErrorCode
{
    None,
    InvalidInvite,
    ForbiddenNotOwner,
    IdConflict,
    ValidationFailed,
    PayloadTooLarge,
    CatalogConflict,
    UpstreamGithub,
    Internal,
    Network,
    NotConfigured,
    EntryNotFound
}

public sealed record DirectUploadAuthResult(bool Ok, string? InviteId, DirectUploadErrorCode Error);

public sealed record DirectUploadPublishResult(
    bool Ok,
    string? ModId,
    string? Sha256,
    long Size,
    string? CommitSha,
    string? MirrorNote,
    DirectUploadErrorCode Error,
    string? Detail);

public sealed class DirectUploadPublishRequest
{
    public required string InviteCode { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Summary { get; init; }
    public string? Version { get; init; }
    public string? Category { get; init; }
    public string? Type { get; init; }
    public required string DllPath { get; init; }
    public string? PreviewPath { get; init; }
}

public sealed class DirectUploadService
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    readonly HttpClient _http;
    readonly string _apiBaseUrl;

    public DirectUploadService(HttpClient http, string apiBaseUrl)
    {
        _http = http;
        _apiBaseUrl = (apiBaseUrl ?? "").Trim().TrimEnd('/');
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiBaseUrl);

    public async Task<DirectUploadAuthResult> CheckAsync(string inviteCode, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new DirectUploadAuthResult(false, null, DirectUploadErrorCode.NotConfigured);
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { inviteCode }),
                Encoding.UTF8,
                "application/json");
            using var res = await _http.PostAsync($"{_apiBaseUrl}/v1/auth/check", content, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return new DirectUploadAuthResult(false, null, MapError(body, res.StatusCode));
            var parsed = JsonSerializer.Deserialize<AuthOkDto>(body, JsonOptions);
            if (parsed?.Ok != true || string.IsNullOrWhiteSpace(parsed.InviteId))
                return new DirectUploadAuthResult(false, null, DirectUploadErrorCode.InvalidInvite);
            return new DirectUploadAuthResult(true, parsed.InviteId, DirectUploadErrorCode.None);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return new DirectUploadAuthResult(false, null, DirectUploadErrorCode.Network);
        }
    }

    public async Task<IReadOnlyList<string>> ListOwnedAsync(string inviteCode, CancellationToken ct = default)
    {
        if (!IsConfigured) return Array.Empty<string>();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/v1/mods/owned");
        req.Headers.TryAddWithoutValidation("X-Invite-Code", inviteCode);
        using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return Array.Empty<string>();
        var parsed = JsonSerializer.Deserialize<OwnedDto>(body, JsonOptions);
        return parsed?.ModIds ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public async Task<DirectUploadPublishResult> PublishAsync(
        DirectUploadPublishRequest req,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return Fail(DirectUploadErrorCode.NotConfigured, "API base URL not set");
        if (string.IsNullOrWhiteSpace(req.DllPath) || !File.Exists(req.DllPath))
            return Fail(DirectUploadErrorCode.ValidationFailed, "dll missing");

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(req.InviteCode), "inviteCode");
            form.Add(new StringContent(req.Id.Trim()), "id");
            form.Add(new StringContent(req.Name.Trim()), "name");
            if (!string.IsNullOrWhiteSpace(req.Summary))
                form.Add(new StringContent(req.Summary), "summary");
            if (!string.IsNullOrWhiteSpace(req.Version))
                form.Add(new StringContent(req.Version), "version");
            if (!string.IsNullOrWhiteSpace(req.Category))
                form.Add(new StringContent(req.Category), "category");
            if (!string.IsNullOrWhiteSpace(req.Type))
                form.Add(new StringContent(req.Type), "type");

            await AddFileAsync(form, "dll", req.DllPath, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(req.PreviewPath) && File.Exists(req.PreviewPath))
                await AddFileAsync(form, "preview", req.PreviewPath, ct).ConfigureAwait(false);

            using var res = await _http.PostAsync($"{_apiBaseUrl}/v1/mods/publish", form, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return Fail(MapError(body, res.StatusCode), body);

            var ok = JsonSerializer.Deserialize<PublishOkDto>(body, JsonOptions);
            if (ok is null || string.IsNullOrWhiteSpace(ok.ModId))
                return Fail(DirectUploadErrorCode.Internal, body);

            return new DirectUploadPublishResult(
                true, ok.ModId, ok.Sha256, ok.Size, ok.CommitSha, ok.MirrorNote,
                DirectUploadErrorCode.None, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Fail(DirectUploadErrorCode.Network, ex.Message);
        }
    }

    static async Task AddFileAsync(MultipartFormDataContent form, string field, string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, field, Path.GetFileName(path));
    }

    static DirectUploadPublishResult Fail(DirectUploadErrorCode code, string? detail) =>
        new(false, null, null, 0, null, null, code, detail);

    static DirectUploadErrorCode MapError(string body, System.Net.HttpStatusCode status)
    {
        try
        {
            var err = JsonSerializer.Deserialize<ErrorDto>(body, JsonOptions)?.Error;
            return err switch
            {
                "invalid_invite" => DirectUploadErrorCode.InvalidInvite,
                "forbidden_not_owner" => DirectUploadErrorCode.ForbiddenNotOwner,
                "id_conflict" => DirectUploadErrorCode.IdConflict,
                "validation_failed" => DirectUploadErrorCode.ValidationFailed,
                "payload_too_large" => DirectUploadErrorCode.PayloadTooLarge,
                "catalog_conflict" => DirectUploadErrorCode.CatalogConflict,
                "upstream_github" => DirectUploadErrorCode.UpstreamGithub,
                _ => status switch
                {
                    System.Net.HttpStatusCode.Unauthorized => DirectUploadErrorCode.InvalidInvite,
                    System.Net.HttpStatusCode.Forbidden => DirectUploadErrorCode.ForbiddenNotOwner,
                    System.Net.HttpStatusCode.Conflict => DirectUploadErrorCode.CatalogConflict,
                    (System.Net.HttpStatusCode)413 => DirectUploadErrorCode.PayloadTooLarge,
                    _ => DirectUploadErrorCode.Internal
                }
            };
        }
        catch
        {
            return DirectUploadErrorCode.Internal;
        }
    }

    sealed class AuthOkDto
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("inviteId")] public string? InviteId { get; set; }
    }

    sealed class OwnedDto
    {
        [JsonPropertyName("modIds")] public List<string>? ModIds { get; set; }
    }

    sealed class PublishOkDto
    {
        [JsonPropertyName("modId")] public string? ModId { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("commitSha")] public string? CommitSha { get; set; }
        [JsonPropertyName("mirrorNote")] public string? MirrorNote { get; set; }
    }

    sealed class ErrorDto
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
