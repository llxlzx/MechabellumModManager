using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public static class InstallDefaultsMerger
{
    /// <summary>
    /// Fills blank user config fields from the machine-wide install seed.
    /// UiLanguage is applied only when the user config file did not exist yet
    /// (so an existing "system" preference is not overwritten).
    /// </summary>
    public static bool TryMerge(AppConfig user, InstallDefaults? seed, bool userConfigExisted)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (seed is null)
            return false;

        var changed = false;

        if (string.IsNullOrWhiteSpace(user.GamePath) && !string.IsNullOrWhiteSpace(seed.GamePath))
        {
            user.GamePath = seed.GamePath.Trim();
            changed = true;
        }

        if (!userConfigExisted && !string.IsNullOrWhiteSpace(seed.UiLanguage))
        {
            user.UiLanguage = seed.UiLanguage.Trim();
            changed = true;
        }

        return changed;
    }
}
