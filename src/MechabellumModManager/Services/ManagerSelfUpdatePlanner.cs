namespace MechabellumModManager.Services;

/// <summary>
/// Decides whether a manifest can drive one-click apply for the current deployment channel.
/// </summary>
public static class ManagerSelfUpdatePlanner
{
    public static bool CanApplyOneClick(ManagerDeploymentKind kind, UpdateManifest? manifest)
    {
        if (manifest is null)
            return false;

        return kind switch
        {
            ManagerDeploymentKind.Installed =>
                IsHttps(manifest.SetupUrl) && IsSha256(manifest.SetupSha256),
            ManagerDeploymentKind.Portable =>
                IsHttps(manifest.PortableUrl) && IsSha256(manifest.PortableSha256),
            _ => false
        };
    }

    public static string? OpenUrlFallback(ManagerDeploymentKind kind, UpdateManifest manifest) =>
        kind == ManagerDeploymentKind.Portable && IsHttps(manifest.PortableUrl)
            ? manifest.PortableUrl!.Trim()
            : IsHttps(manifest.SetupUrl)
                ? manifest.SetupUrl!.Trim()
                : null;

    static bool IsHttps(string? url) => ExternalUrlPolicy.IsHttpsUrl(url);

    static bool IsSha256(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..].Trim();
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }

    public static string NormalizeSha256(string raw)
    {
        var value = raw.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..].Trim();
        return value.ToLowerInvariant();
    }
}
