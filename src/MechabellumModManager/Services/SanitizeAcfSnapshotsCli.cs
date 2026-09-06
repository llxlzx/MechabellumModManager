using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Headless Setup helper:
/// MechabellumModManager.exe --sanitize-acf-snapshots [--snapshots-dir "..."] [--beta-branch-name "public_test"]
/// Deletes dirty official.acf / beta.acf under the manager snapshot folder.
/// </summary>
public static class SanitizeAcfSnapshotsCli
{
    public const string Arg = "--sanitize-acf-snapshots";

    public static bool TryParseArgs(string[] args, out string? snapshotsDir, out string? betaBranchName)
    {
        snapshotsDir = null;
        betaBranchName = null;
        if (args is null || args.Length == 0)
            return false;
        if (!args.Any(a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase)))
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--snapshots-dir", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                snapshotsDir = args[++i];
            else if (string.Equals(args[i], "--beta-branch-name", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
                betaBranchName = args[++i];
        }

        return true;
    }

    /// <returns>0 always (non-fatal); deletions logged to AppData sanitize-acf.log</returns>
    public static int Run(string? snapshotsDir = null, string? betaBranchName = null)
    {
        try
        {
            SteamAcfSnapshotSanitizeResult result;
            if (!string.IsNullOrWhiteSpace(snapshotsDir))
            {
                result = SteamAcfSnapshotSanitizer.SanitizeDirectory(snapshotsDir, betaBranchName);
            }
            else
            {
                result = SteamAcfSnapshotSanitizer.SanitizeDefault();
            }

            Log($"deleted={result.DeletedFiles.Count}");
            foreach (var f in result.DeletedFiles)
                Log("deleted " + f);
            return 0;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            return 0; // never fail Setup
        }
    }

    static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MechabellumModManager");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "sanitize-acf.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // ignore
        }
    }
}
