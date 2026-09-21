using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MechabellumModManager.Services;

public sealed class MirrorPublishInput
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Summary { get; init; }
    public string? Version { get; init; }
    public required string DllPath { get; init; }
    public string? PreviewPath { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record MirrorUpload(string LocalPath, string RemoteKey);

public sealed class MirrorPublishPlan
{
    public required string Sha256 { get; init; }
    public required long Size { get; init; }
    public required string CatalogJson { get; init; }
    public required string EntryJson { get; init; }
    public required string EntryKey { get; init; }
    public required IReadOnlyList<MirrorUpload> Uploads { get; init; }
}

/// <summary>
/// Builds a mirror publish. File uploads stay inside mods/{id}/. catalog.json is produced
/// for the maintainer and is not part of <see cref="MirrorPublishPlan.Uploads"/>.
/// </summary>
public static class MirrorCatalogPublisher
{
    public const long MaxBytes = 95L * 1024 * 1024;
    public const string CatalogKey = "MechabellumMods/catalog.json";

    public static string? BucketFromMirrorUrl(string? mirrorBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(mirrorBaseUrl))
            return null;
        if (!Uri.TryCreate(mirrorBaseUrl.Trim(), UriKind.Absolute, out var uri))
            return null;
        const string marker = ".cos.";
        var host = uri.Host;
        var at = host.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at <= 0)
            return null;
        return host[..at];
    }

    public static string? RegionFromMirrorUrl(string? mirrorBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(mirrorBaseUrl))
            return null;
        if (!Uri.TryCreate(mirrorBaseUrl.Trim(), UriKind.Absolute, out var uri))
            return null;
        const string marker = ".cos.";
        const string suffix = ".myqcloud.com";
        var host = uri.Host;
        var at = host.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return null;
        var rest = host[(at + marker.Length)..];
        var end = rest.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
        if (end <= 0)
            return null;
        return rest[..end];
    }

    public static MirrorPublishPlan Plan(string? catalogJson, MirrorPublishInput input)
    {
        var id = input.Id.Trim();
        var name = input.Name.Trim();
        if (!IsSafeId(id) || string.IsNullOrWhiteSpace(name))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "id or name");
        if (string.IsNullOrWhiteSpace(input.DllPath) || !File.Exists(input.DllPath))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "dll missing");

        var dllInfo = new FileInfo(input.DllPath);
        if (dllInfo.Length <= 0)
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "dll empty");
        if (dllInfo.Length > MaxBytes)
            throw new MirrorPublishException(DirectUploadErrorCode.PayloadTooLarge, "dll");

        var dllName = SafeFileName(Path.GetFileName(input.DllPath), id + ".dll");
        var fileRel = $"mods/{id}/{dllName}";
        string? previewRel = null;
        if (!string.IsNullOrWhiteSpace(input.PreviewPath))
        {
            if (!File.Exists(input.PreviewPath))
                throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "preview missing");
            var previewLen = new FileInfo(input.PreviewPath).Length;
            if (previewLen <= 0)
                throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "preview empty");
            if (previewLen > MaxBytes)
                throw new MirrorPublishException(DirectUploadErrorCode.PayloadTooLarge, "preview");
            previewRel = $"mods/{id}/preview.png";
        }

        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input.DllPath))).ToLowerInvariant();
        var root = ParseRoot(catalogJson);
        var mods = root["mods"] as JsonArray ?? new JsonArray();
        root["mods"] = mods;

        JsonObject? existing = null;
        foreach (var node in mods)
        {
            if (node is JsonObject obj && string.Equals(obj["id"]?.GetValue<string>(), id, StringComparison.Ordinal))
            {
                existing = obj;
                break;
            }
        }

        var entry = existing ?? new JsonObject();
        entry["id"] = id;
        entry["name"] = name;
        if (!string.IsNullOrWhiteSpace(input.Summary))
            entry["summary"] = input.Summary.Trim();
        if (!string.IsNullOrWhiteSpace(input.Version))
            entry["version"] = input.Version.Trim();
        entry["file"] = fileRel;
        entry["sha256"] = sha;
        entry["size"] = dllInfo.Length;
        entry["updatedAt"] = input.UpdatedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        if (previewRel is not null)
            entry["preview"] = previewRel;
        if (existing is null)
            mods.Add(entry);

        root["updatedAt"] = entry["updatedAt"]!.GetValue<string>();

        var uploads = new List<MirrorUpload>
        {
            new(input.DllPath, "MechabellumMods/" + fileRel)
        };
        if (previewRel is not null)
            uploads.Add(new MirrorUpload(input.PreviewPath!, "MechabellumMods/" + previewRel));

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        return new MirrorPublishPlan
        {
            Sha256 = sha,
            Size = dllInfo.Length,
            CatalogJson = root.ToJsonString(jsonOptions),
            EntryJson = entry.ToJsonString(jsonOptions),
            EntryKey = EntryKeyFor(id),
            Uploads = uploads
        };
    }

    public static string EntryKeyFor(string id) => $"MechabellumMods/mods/{id}/entry.json";

    /// <summary>
    /// Copies one uploader-owned entry into the catalog. Fields the uploader must not
    /// control (author, tags, originUrl, locales) stay as they already are.
    /// </summary>
    public static string MergeListedEntry(string? catalogJson, string entryJson, string expectedId)
    {
        var id = expectedId.Trim();
        if (!IsSafeId(id))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "id");

        JsonObject incoming;
        try
        {
            incoming = JsonNode.Parse(entryJson) as JsonObject
                ?? throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "entry");
        }
        catch (MirrorPublishException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, ex.Message);
        }

        if (!string.Equals(ReadString(incoming, "id"), id, StringComparison.Ordinal))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "id mismatch");

        var file = ReadString(incoming, "file");
        if (!IsOwnedRelativeFile(id, file))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "file");

        var name = ReadString(incoming, "name")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "name");

        var sha = ReadString(incoming, "sha256")?.Trim().ToLowerInvariant();
        if (sha is null || sha.Length != 64 || sha.Any(c => c is < '0' or > '9' and < 'a' or > 'f'))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "sha256");

        if (!TryReadSize(incoming, out var size) || size <= 0 || size > MaxBytes)
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "size");

        string? preview = null;
        if (incoming.ContainsKey("preview") && incoming["preview"] is not null)
        {
            preview = ReadString(incoming, "preview");
            if (!string.Equals(preview, $"mods/{id}/preview.png", StringComparison.Ordinal))
                throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "preview");
        }

        var root = ParseRoot(catalogJson);
        var mods = root["mods"] as JsonArray ?? new JsonArray();
        root["mods"] = mods;

        JsonObject? existing = null;
        foreach (var node in mods)
        {
            if (node is JsonObject obj && string.Equals(ReadString(obj, "id"), id, StringComparison.Ordinal))
            {
                existing = obj;
                break;
            }
        }

        var entry = existing ?? new JsonObject();
        entry["id"] = id;
        entry["name"] = name;
        CopyOptional(incoming, entry, "summary");
        CopyOptional(incoming, entry, "version");
        entry["file"] = file;
        entry["sha256"] = sha;
        entry["size"] = size;
        var updatedAt = NormalizeTimestamp(ReadString(incoming, "updatedAt"));
        entry["updatedAt"] = updatedAt;
        if (preview is not null)
            entry["preview"] = preview;
        if (existing is null)
            mods.Add(entry);

        root["updatedAt"] = updatedAt;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static async Task<string> DownloadEntryAsync(HttpClient http, string mirrorBaseUrl, string id, CancellationToken ct)
    {
        var safeId = id.Trim();
        if (!IsSafeId(safeId))
            throw new MirrorPublishException(DirectUploadErrorCode.ValidationFailed, "id");
        var root = mirrorBaseUrl.Trim().TrimEnd('/');
        using var res = await http.GetAsync(root + "/" + EntryKeyFor(safeId), ct).ConfigureAwait(false);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new MirrorPublishException(DirectUploadErrorCode.EntryNotFound, safeId);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public static async Task<string> DownloadCatalogAsync(HttpClient http, string mirrorBaseUrl, CancellationToken ct)
    {
        var root = mirrorBaseUrl.Trim().TrimEnd('/');
        using var res = await http.GetAsync(root + "/" + CatalogKey, ct).ConfigureAwait(false);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            return """{"mods":[]}""";
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    static JsonObject ParseRoot(string? catalogJson)
    {
        if (string.IsNullOrWhiteSpace(catalogJson))
            return new JsonObject { ["mods"] = new JsonArray() };
        try
        {
            return JsonNode.Parse(catalogJson) as JsonObject
                ?? throw new MirrorPublishException(DirectUploadErrorCode.Internal, "catalog");
        }
        catch (JsonException ex)
        {
            throw new MirrorPublishException(DirectUploadErrorCode.Internal, ex.Message);
        }
    }

    public static bool IsSafeId(string id) =>
        id.Length is > 0 and <= 64
        && char.IsAsciiLetterOrDigit(id[0])
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    static bool IsOwnedRelativeFile(string id, string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return false;
        var prefix = "mods/" + id + "/";
        if (!file.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var name = file[prefix.Length..];
        return name.Length > 0
            && name is not "." and not ".."
            && name.IndexOfAny(['/', '\\']) < 0
            && !name.Contains("..", StringComparison.Ordinal);
    }

    static string? ReadString(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue value)
            return null;
        return value.TryGetValue<string>(out var text) ? text : null;
    }

    static bool TryReadSize(JsonObject obj, out long size)
    {
        size = 0;
        if (obj["size"] is not JsonValue value)
            return false;
        if (value.TryGetValue<long>(out size))
            return true;
        if (value.TryGetValue<int>(out var asInt))
        {
            size = asInt;
            return true;
        }

        return false;
    }

    static void CopyOptional(JsonObject from, JsonObject to, string key)
    {
        var text = ReadString(from, key)?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
            to[key] = text;
    }

    static string NormalizeTimestamp(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            return parsed.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    static string SafeFileName(string? name, string fallback)
    {
        var file = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        file = Path.GetFileName(file);
        if (string.IsNullOrWhiteSpace(file) || file is "." or ".." || file.IndexOfAny(['/', '\\']) >= 0)
            return fallback;
        return file;
    }
}

public sealed class MirrorPublishException : Exception
{
    public MirrorPublishException(DirectUploadErrorCode code, string? detail)
        : base(detail)
    {
        Code = code;
    }

    public DirectUploadErrorCode Code { get; }
}
