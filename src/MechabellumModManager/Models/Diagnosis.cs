namespace MechabellumModManager.Models;

public static class DiagnosisCodes
{
    public const string CriticalOpInterrupted = "critical_op_interrupted";
    public const string WizardOrSettleBlocking = "wizard_or_settle_blocking";
    public const string GameOrLinkIncomplete = "game_or_link_incomplete";
    public const string SteamUpdateUnhealthy = "steam_update_unhealthy";
    public const string MelonNeedsUpgrade = "melon_needs_upgrade";
    public const string LoaderNotInjected = "loader_not_injected";
    public const string CatalogOrUpdateNetwork = "catalog_or_update_network";
    public const string DeployBlockedOrFailed = "deploy_blocked_or_failed";
    public const string AcfBetaMismatch = "acf_beta_mismatch";
    public const string Healthy = "healthy";
    public const string DualFolderBroken = "dual_folder_broken";
}

public sealed class DiagnosisEvent
{
    public DateTimeOffset Time { get; init; }
    public string Code { get; init; } = "";
    public IReadOnlyDictionary<string, string?>? Data { get; init; }
}

public sealed class DiagnosisSnapshot
{
    public bool HasInterruptedCriticalOp { get; init; }
    public string? CriticalOpKind { get; init; }
    public string? CriticalOpDetail { get; init; }

    public bool IsWizardOrSettleBlocking { get; init; }
    public string? BranchWizardStep { get; init; }
    public bool IsAwaitingSteamSettle { get; init; }
    public bool SettleAbandonedUnaligned { get; init; }

    public bool GameMissing { get; init; }
    public bool LinkIncompleteButStoreValid { get; init; }

    public bool SteamUpdateUnhealthy { get; init; }
    public bool AcfNotSettled { get; init; }

    public bool MelonNeedsUpgrade { get; init; }
    public string? MelonVersion { get; init; }

    public bool LoaderNotInjected { get; init; }
    public DateTimeOffset? LastLaunchRequestedAt { get; init; }
    public TimeSpan? LatestLogAge { get; init; }

    public bool CatalogOrUpdateNetworkFailed { get; init; }
    public string? NetworkFailureSource { get; init; }

    public bool DeployBlockedOrFailed { get; init; }
    public string? DeployFailure { get; init; }

    public bool AcfBetaMismatch { get; init; }
    public string? SteamBetaKey { get; init; }
    public string? ActiveGameBranch { get; init; }

    public bool DualFolderLooksBroken { get; init; }

    public IReadOnlyList<DiagnosisEvent> RecentEvents { get; init; } = Array.Empty<DiagnosisEvent>();
}

public sealed class Diagnosis
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RuledOut { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Also { get; init; } = Array.Empty<string>();
    public required string Action { get; init; }
}
