using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Canonical Steam dual-folder names under steamapps/common.
/// </summary>
public static class SteamBranchLayout
{
    public const string LinkFolderName = "Mechabellum";
    public const string OfficialStoreFolderName = "Mechabellum_official";
    public const string BetaStoreFolderName = "Mechabellum_beta";

    public static bool IsBranchStoreFolderName(string? folderName) =>
        string.Equals(folderName, OfficialStoreFolderName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(folderName, BetaStoreFolderName, StringComparison.OrdinalIgnoreCase);

    public static bool IsBranchStorePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            return IsBranchStoreFolderName(Path.GetFileName(Path.GetFullPath(path)));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// If <paramref name="path"/> is Mechabellum_official/beta under …/common, returns sibling Mechabellum link path.
    /// </summary>
    public static bool TryGetCanonicalSteamLink(string? path, out string steamLink)
    {
        steamLink = "";
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsBranchStoreFolderName(Path.GetFileName(full)))
                return false;

            var common = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(common)
                || !string.Equals(Path.GetFileName(common), "common", StringComparison.OrdinalIgnoreCase))
                return false;

            steamLink = Path.Combine(common, LinkFolderName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IEnumerable<string> EnumerateExistingStoreFolders(string? anyPathUnderCommon)
    {
        if (string.IsNullOrWhiteSpace(anyPathUnderCommon))
            yield break;

        string common;
        try
        {
            var full = Path.GetFullPath(anyPathUnderCommon);
            var name = Path.GetFileName(full);
            common = string.Equals(name, "common", StringComparison.OrdinalIgnoreCase)
                ? full
                : Path.GetDirectoryName(full) ?? "";
            if (string.IsNullOrEmpty(common)
                || !string.Equals(Path.GetFileName(common), "common", StringComparison.OrdinalIgnoreCase))
                yield break;
        }
        catch
        {
            yield break;
        }

        foreach (var folder in new[] { OfficialStoreFolderName, BetaStoreFolderName })
        {
            var candidate = Path.Combine(common, folder);
            if (Directory.Exists(candidate))
                yield return candidate;
        }
    }
}
