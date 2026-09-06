using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Hard allow-list for pure-game cleanup. Prefer abort over guessing.
/// </summary>
public static class PureGameCleanupWhitelist
{
    public static readonly string[] MelonDirectoryNames =
        ["MelonLoader", "Mods", "Plugins", "UserLibs", "UserData"];

    public static readonly string[] MelonFileNames =
        ["version.dll", "winhttp.dll"];

    public static bool TryNormalizeFullPath(string? path, out string full)
    {
        full = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            full = Path.GetFullPath(path.Trim());
            return !string.IsNullOrWhiteSpace(full);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsExactChild(string parentFull, string childFull)
    {
        if (!TryNormalizeFullPath(parentFull, out var p) || !TryNormalizeFullPath(childFull, out var c))
            return false;
        if (string.Equals(p, c, StringComparison.OrdinalIgnoreCase))
            return false;

        var rel = Path.GetRelativePath(p, c);
        if (string.IsNullOrWhiteSpace(rel) || rel == ".")
            return false;
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            return false;

        // Only one path segment below parent (no nested escape via multi-level for melon roots).
        return rel.IndexOf(Path.DirectorySeparatorChar) < 0
               && rel.IndexOf(Path.AltDirectorySeparatorChar) < 0;
    }

    public static bool IsAllowedMelonDirectory(string gameRoot, string candidate)
    {
        if (!TryNormalizeFullPath(gameRoot, out var root) || !TryNormalizeFullPath(candidate, out var path))
            return false;
        if (!IsExactChild(root, path))
            return false;
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return MelonDirectoryNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowedMelonFile(string gameRoot, string candidate)
    {
        if (!TryNormalizeFullPath(gameRoot, out var root) || !TryNormalizeFullPath(candidate, out var path))
            return false;
        if (!IsExactChild(root, path))
            return false;
        if (Directory.Exists(path))
            return false;
        var name = Path.GetFileName(path);
        return MelonFileNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Other store must be sibling under the same parent as official, named Mechabellum_beta
    /// (or match configured beta path with same parent + beta folder prefix).
    /// </summary>
    public static bool IsAllowedOtherStoreDelete(string officialStore, string otherStore)
    {
        if (!TryNormalizeFullPath(officialStore, out var official)
            || !TryNormalizeFullPath(otherStore, out var other))
            return false;
        if (string.Equals(official, other, StringComparison.OrdinalIgnoreCase))
            return false;

        var officialParent = Path.GetDirectoryName(official);
        var otherParent = Path.GetDirectoryName(other);
        if (string.IsNullOrWhiteSpace(officialParent) || string.IsNullOrWhiteSpace(otherParent))
            return false;
        if (!string.Equals(
                Path.GetFullPath(officialParent),
                Path.GetFullPath(otherParent),
                StringComparison.OrdinalIgnoreCase))
            return false;

        var otherName = Path.GetFileName(other.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(otherName, SteamBranchLayout.OfficialStoreFolderName, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(otherName, SteamBranchLayout.LinkFolderName, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(otherName, SteamBranchLayout.BetaStoreFolderName, StringComparison.OrdinalIgnoreCase)
               || otherName.StartsWith("Mechabellum_", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedManagerAppData(string candidate)
    {
        if (!TryNormalizeFullPath(candidate, out var path))
            return false;
        var expected = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PathsService.AppFolderName));
        return string.Equals(path, expected, StringComparison.OrdinalIgnoreCase);
    }
}
