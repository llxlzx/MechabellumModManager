namespace MechabellumModManager.Services;

public enum CatalogMissFollowUp
{
    ShowError,
    Blocked,
    DownloadParts
}

/// <summary>One whole-file 404 refresh per download action. The second miss must not refresh again.</summary>
public struct WholeFileMissBudget
{
    public bool Consumed { get; private set; }

    public bool TryUse()
    {
        if (Consumed)
            return false;
        Consumed = true;
        return true;
    }
}

public static class CatalogWholeFileMiss
{
    public static CatalogMissFollowUp AfterRefresh(CatalogMod? refreshed, string? localVersion)
    {
        if (refreshed is null)
            return CatalogMissFollowUp.ShowError;
        var floor = ManagerVersionFloor.Evaluate(refreshed.MinManagerVersion, localVersion);
        if (!floor.Allowed)
            return CatalogMissFollowUp.Blocked;
        if (refreshed.Parts is { Count: > 0 })
            return CatalogMissFollowUp.DownloadParts;
        return CatalogMissFollowUp.ShowError;
    }
}
