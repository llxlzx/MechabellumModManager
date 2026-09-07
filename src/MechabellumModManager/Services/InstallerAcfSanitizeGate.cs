using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Shared skip rule for Setup post-install ACF sanitize.
/// Inno Pascal must mirror this: no snapshots → do not launch the manager exe.
/// </summary>
public static class InstallerAcfSanitizeGate
{
    public static string DefaultSnapshotsDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MechabellumModManager",
            "steam-acf-snapshots");

    public static bool HasSnapshotsToSanitize(string? snapshotsDir)
    {
        if (string.IsNullOrWhiteSpace(snapshotsDir))
            return false;
        try
        {
            if (!Directory.Exists(snapshotsDir))
                return false;
            return File.Exists(Path.Combine(snapshotsDir, "official.acf"))
                   || File.Exists(Path.Combine(snapshotsDir, "beta.acf"));
        }
        catch
        {
            return false;
        }
    }
}
