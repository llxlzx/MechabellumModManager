namespace MechabellumModManager.Services;

/// <summary>
/// Uninstall defaults to removing the manager only.
/// Opt-in Setup dialog may still run full --pure-game-cleanup.
/// </summary>
public static class UninstallCleanupPolicy
{
    public static PureGameCleanupRequest CreateManagerOnlyRequest() =>
        new()
        {
            SkipAppData = true,
            SkipMelon = true,
            ConfirmDeleteOtherStore = false
        };
}
