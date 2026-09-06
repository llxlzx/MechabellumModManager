using System.Diagnostics;
using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Bundled MelonLoader redist is 0.7.3; older installs (e.g. 0.7.1) should be upgraded.
/// </summary>
public static class MelonLoaderVersionGate
{
    public static readonly Version BundledMinimum = new(0, 7, 3);

    /// <summary>
    /// True when a parseable installed FileVersion is strictly below the bundled minimum.
    /// Unreadable/empty versions return false (do not force upgrade loops).
    /// </summary>
    public static bool ShouldUpgradeInstalled(string? fileVersion)
    {
        if (string.IsNullOrWhiteSpace(fileVersion))
            return false;

        var normalized = fileVersion.Trim();
        var plus = normalized.IndexOf('+');
        if (plus >= 0)
            normalized = normalized[..plus];

        if (!Version.TryParse(normalized, out var installed))
            return false;

        return installed < BundledMinimum;
    }

    public static bool ShouldUpgradeFromGamePath(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;
        return ShouldUpgradeInstalled(TryReadInstalledVersion(gamePath));
    }

    public static string? TryReadInstalledVersion(string gamePath)
    {
        var melonRoot = Path.Combine(gamePath, "MelonLoader");
        foreach (var candidate in new[]
                 {
                     Path.Combine(melonRoot, "net6", "MelonLoader.dll"),
                     Path.Combine(melonRoot, "net472", "MelonLoader.dll"),
                     Path.Combine(melonRoot, "net35", "MelonLoader.dll"),
                     Path.Combine(melonRoot, "MelonLoader.dll")
                 })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                return FileVersionInfo.GetVersionInfo(candidate).FileVersion;
            }
            catch
            {
                // try next
            }
        }

        return null;
    }
}
