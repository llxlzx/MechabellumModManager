namespace MechabellumModManager.Services;

public readonly record struct BranchStoreHealthResult(
    bool IsGameRoot,
    bool AcfHardFault,
    bool AcfAllowsSnapshot,
    bool CanPromoteReady,
    string? PrimaryReason);

/// <summary>
/// Pure health check for dual-folder promote/settle: game root + snapshot-safe ACF.
/// </summary>
public static class BranchStoreHealth
{
    public static BranchStoreHealthResult Evaluate(string? storePath, string? acfText)
    {
        var root = SteamGameLocator.LooksLikeGameRoot(storePath);
        var hard = !string.IsNullOrWhiteSpace(acfText) && SteamBetaKeyEditor.LooksAcfHardFault(acfText);
        var snap = !string.IsNullOrWhiteSpace(acfText)
                   && SteamBetaKeyEditor.LooksSettledForSnapshot(acfText);

        string? reason = null;
        if (!root)
            reason = "store_not_game_root";
        else if (hard)
            reason = "acf_hard_fault";
        else if (!snap)
            reason = "acf_not_settled";

        return new BranchStoreHealthResult(root, hard, snap, root && snap, reason);
    }

    /// <summary>
    /// Wizard WaitingDownloadB auto-continue: complete root + settled ACF + Steam/game not running.
    /// </summary>
    public static bool IsWizardDownloadReady(string? steamLinkPath, string? acfText, bool steamOrGameRunning)
    {
        if (steamOrGameRunning)
            return false;
        var health = Evaluate(steamLinkPath, acfText);
        return health.CanPromoteReady;
    }
}
