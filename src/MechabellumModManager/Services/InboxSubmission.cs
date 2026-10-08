using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public sealed record InboxSubmissionFile(
    string Role,
    string Name,
    string FullPath,
    long Size,
    string Sha256,
    bool Main);

public sealed record InboxGrantRequestFile(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("main")] bool Main);

public sealed record InboxGrantRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("files")] IReadOnlyList<InboxGrantRequestFile> Files);

public sealed record InboxBuildResult(bool Ok, DirectUploadErrorCode Error, InboxGrantRequest? Request);

/// <summary>
/// Client-side copy of the worker <c>inspectGrant</c> rules.
/// The returned request is what <see cref="InboxGrantClient.RequestAsync"/> posts.
/// It carries file names and hashes, not local paths.
/// </summary>
public static class InboxSubmission
{
    const long MaxFileBytes = 104857599;
    const long MaxPreviewBytes = 8388608;
    const int MaxFileCount = 20;
    const long MaxTotalBytes = 536870912;

    static readonly Regex ModId = new("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex DllName = new("^[A-Za-z0-9._-]+\\.dll$", RegexOptions.CultureInvariant);
    static readonly Regex PartName = new("^\\d{4}$", RegexOptions.CultureInvariant);
    static readonly Regex Sha256Hex = new("^[a-f0-9]{64}$", RegexOptions.CultureInvariant);

    static readonly Dictionary<string, HashSet<string>> Roles = new(StringComparer.Ordinal)
    {
        ["single"] = new HashSet<string>(StringComparer.Ordinal) { "dll", "preview" },
        ["parts"] = new HashSet<string>(StringComparer.Ordinal) { "part", "preview" },
        ["bundle"] = new HashSet<string>(StringComparer.Ordinal) { "bundle", "preview" }
    };

    public static InboxBuildResult Build(
        string kind,
        string id,
        string name,
        string? summary,
        string? version,
        IReadOnlyList<InboxSubmissionFile> files)
    {
        if (kind is not ("single" or "parts" or "bundle"))
            return Fail(DirectUploadErrorCode.ValidationFailed);
        if (string.IsNullOrEmpty(id) || !ModId.IsMatch(id))
            return Fail(DirectUploadErrorCode.ValidationFailed);
        if (string.IsNullOrEmpty(name) || name.Length > 80 || name.Contains('\r') || name.Contains('\n'))
            return Fail(DirectUploadErrorCode.ValidationFailed);

        if (summary is null)
            summary = "";
        else if (summary.Length > 400)
            return Fail(DirectUploadErrorCode.ValidationFailed);

        if (version is null)
            version = "";
        else if (version.Length > 32)
            return Fail(DirectUploadErrorCode.ValidationFailed);

        if (files is null)
            return Fail(DirectUploadErrorCode.ValidationFailed);

        var parsed = new List<InboxGrantRequestFile>(files.Count);
        foreach (var file in files)
        {
            if (!TryParseFile(kind, file, out var grantFile))
                return Fail(DirectUploadErrorCode.ValidationFailed);
            parsed.Add(grantFile);
        }

        if (!RolesMatchKind(kind, parsed))
            return Fail(DirectUploadErrorCode.ValidationFailed);

        if (parsed.Count > MaxFileCount)
            return Fail(DirectUploadErrorCode.PayloadTooLarge);

        long total = 0;
        foreach (var file in parsed)
        {
            if (file.Size < 1 || file.Size > MaxFileBytes)
                return Fail(DirectUploadErrorCode.PayloadTooLarge);
            if (file.Role == "preview" && file.Size > MaxPreviewBytes)
                return Fail(DirectUploadErrorCode.PayloadTooLarge);
            total += file.Size;
        }

        if (total > MaxTotalBytes)
            return Fail(DirectUploadErrorCode.PayloadTooLarge);

        return new InboxBuildResult(
            true,
            DirectUploadErrorCode.None,
            new InboxGrantRequest(id, name, summary, version, kind, parsed));
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    static bool TryParseFile(string kind, InboxSubmissionFile file, out InboxGrantRequestFile grantFile)
    {
        grantFile = null!;
        if (file is null)
            return false;
        if (!Roles.TryGetValue(kind, out var allowed) || !allowed.Contains(file.Role))
            return false;
        if (string.IsNullOrEmpty(file.Name) || !NameMatchesRole(file.Role, file.Name))
            return false;
        if (string.IsNullOrEmpty(file.Sha256) || !Sha256Hex.IsMatch(file.Sha256))
            return false;

        var main = file.Role == "bundle" && file.Main;
        grantFile = new InboxGrantRequestFile(file.Role, file.Name, file.Size, file.Sha256, main);
        return true;
    }

    static bool NameMatchesRole(string role, string name)
    {
        if (role == "preview")
            return name == "preview.png";
        if (role == "part")
            return PartName.IsMatch(name);
        return DllName.IsMatch(name);
    }

    static bool RolesMatchKind(string kind, List<InboxGrantRequestFile> files)
    {
        var dlls = files.FindAll(file => file.Role == "dll");
        var parts = files.FindAll(file => file.Role == "part");
        var bundles = files.FindAll(file => file.Role == "bundle");
        var previews = files.FindAll(file => file.Role == "preview");
        if (previews.Count > 1)
            return false;
        if (files.Select(file => file.Name).Distinct(StringComparer.Ordinal).Count() != files.Count)
            return false;

        if (kind == "single")
            return dlls.Count == 1 && parts.Count == 0 && bundles.Count == 0;
        if (kind == "parts")
        {
            if (parts.Count < 1 || dlls.Count != 0 || bundles.Count != 0)
                return false;
            var names = parts.Select(file => file.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
            for (var index = 0; index < names.Count; index++)
            {
                if (names[index] != index.ToString("D4"))
                    return false;
            }

            return true;
        }

        return bundles.Count >= 2
            && dlls.Count == 0
            && parts.Count == 0
            && bundles.FindAll(file => file.Main).Count == 1;
    }

    static InboxBuildResult Fail(DirectUploadErrorCode error) => new(false, error, null);
}
