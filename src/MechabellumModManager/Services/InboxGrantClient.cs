using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MechabellumModManager.Services;

public sealed record InboxGrantCredentials(string TmpSecretId, string TmpSecretKey, string SessionToken);

public sealed record InboxGrantObject(string Key, string? Role, long Size, string? Sha256, bool Main);

public sealed record InboxGrantResult(
    bool Ok,
    string? InviteId,
    string? Bucket,
    string? Region,
    int DurationSeconds,
    InboxGrantCredentials? Credentials,
    IReadOnlyList<InboxGrantObject> Objects,
    DirectUploadErrorCode Error,
    string? Detail);

public sealed record InboxCompleteResult(bool Ok, DirectUploadErrorCode Error, string? Detail);

public sealed class InboxGrantClient
{
    readonly HttpClient _http;
    readonly string _apiBaseUrl;

    public InboxGrantClient(HttpClient http, string apiBaseUrl)
    {
        _http = http;
        _apiBaseUrl = (apiBaseUrl ?? "").Trim().TrimEnd('/');
    }

    bool IsHttps => _apiBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public async Task<InboxGrantResult> RequestAsync(string inviteCode, object payload, CancellationToken ct = default)
    {
        if (!IsHttps)
            return FailGrant(DirectUploadErrorCode.NotConfigured);

        try
        {
            var node = JsonSerializer.SerializeToNode(payload) as JsonObject ?? new JsonObject();
            node["inviteCode"] = inviteCode ?? "";
            using var content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json");
            using var res = await _http.PostAsync($"{_apiBaseUrl}/v1/inbox/grant", content, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return FailGrant(MapError(body, res.StatusCode));
            return ParseGrant(body);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return FailGrant(DirectUploadErrorCode.Network);
        }
    }

    public async Task<InboxCompleteResult> CompleteAsync(string inviteCode, string id, CancellationToken ct = default)
    {
        if (!IsHttps)
            return FailComplete(DirectUploadErrorCode.NotConfigured);

        try
        {
            var json = JsonSerializer.Serialize(new { inviteCode, id });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var res = await _http.PostAsync($"{_apiBaseUrl}/v1/inbox/complete", content, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return FailComplete(MapError(body, res.StatusCode));
            if (!IsOk(body))
                return FailComplete(DirectUploadErrorCode.Internal);
            return new InboxCompleteResult(true, DirectUploadErrorCode.None, null);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return FailComplete(DirectUploadErrorCode.Network);
        }
    }

    static InboxGrantResult ParseGrant(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
                return FailGrant(DirectUploadErrorCode.Internal);

            var credsEl = root.GetProperty("credentials");
            var credentials = new InboxGrantCredentials(
                credsEl.GetProperty("tmpSecretId").GetString() ?? "",
                credsEl.GetProperty("tmpSecretKey").GetString() ?? "",
                credsEl.GetProperty("sessionToken").GetString() ?? "");

            var objects = new List<InboxGrantObject>();
            foreach (var item in root.GetProperty("objects").EnumerateArray())
            {
                objects.Add(new InboxGrantObject(
                    item.GetProperty("key").GetString() ?? "",
                    item.TryGetProperty("role", out var role) ? role.GetString() : null,
                    item.TryGetProperty("size", out var size) && size.TryGetInt64(out var n) ? n : 0,
                    item.TryGetProperty("sha256", out var sha) ? sha.GetString() : null,
                    item.TryGetProperty("main", out var main) && main.ValueKind == JsonValueKind.True));
            }

            if (objects.Count == 0 || string.IsNullOrEmpty(objects[0].Key))
                return FailGrant(DirectUploadErrorCode.Internal);

            return new InboxGrantResult(
                true,
                root.TryGetProperty("inviteId", out var inviteId) ? inviteId.GetString() : null,
                root.TryGetProperty("bucket", out var bucket) ? bucket.GetString() : null,
                root.TryGetProperty("region", out var region) ? region.GetString() : null,
                root.TryGetProperty("durationSeconds", out var duration) && duration.TryGetInt32(out var seconds) ? seconds : 0,
                credentials,
                objects,
                DirectUploadErrorCode.None,
                null);
        }
        catch
        {
            return FailGrant(DirectUploadErrorCode.Internal);
        }
    }

    static bool IsOk(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    static DirectUploadErrorCode MapError(string body, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString() switch
                {
                    "invalid_invite" => DirectUploadErrorCode.InvalidInvite,
                    "forbidden_not_owner" => DirectUploadErrorCode.ForbiddenNotOwner,
                    "id_conflict" => DirectUploadErrorCode.IdConflict,
                    "validation_failed" => DirectUploadErrorCode.ValidationFailed,
                    "payload_too_large" => DirectUploadErrorCode.PayloadTooLarge,
                    "catalog_conflict" => DirectUploadErrorCode.CatalogConflict,
                    "upstream_github" => DirectUploadErrorCode.UpstreamGithub,
                    "not_found" => DirectUploadErrorCode.EntryNotFound,
                    _ => Fallback(status)
                };
            }

            return Fallback(status);
        }
        catch
        {
            return DirectUploadErrorCode.Internal;
        }
    }

    static DirectUploadErrorCode Fallback(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => DirectUploadErrorCode.InvalidInvite,
        HttpStatusCode.Forbidden => DirectUploadErrorCode.ForbiddenNotOwner,
        HttpStatusCode.Conflict => DirectUploadErrorCode.CatalogConflict,
        (HttpStatusCode)413 => DirectUploadErrorCode.PayloadTooLarge,
        HttpStatusCode.NotFound => DirectUploadErrorCode.EntryNotFound,
        _ => DirectUploadErrorCode.Internal
    };

    static InboxGrantResult FailGrant(DirectUploadErrorCode code) =>
        new(false, null, null, null, 0, null, Array.Empty<InboxGrantObject>(), code, code.ToString());

    static InboxCompleteResult FailComplete(DirectUploadErrorCode code) =>
        new(false, code, code.ToString());
}
