using System.IO;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Builds a read-only <see cref="DiagnosisSnapshot"/> from disk + live UI flags.
/// Does not write disk.
/// </summary>
public static class DiagnosisSnapshotBuilder
{
    public static DiagnosisSnapshot Capture(
        PathsService paths,
        string? gamePath,
        GameStatus? status,
        DateTimeOffset? lastLaunchRequestedAt,
        BranchSwitchConfig? branch,
        bool isAwaitingSteamSettle,
        bool settleAbandonedUnaligned = false,
        IReadOnlyList<DiagnosisEvent>? events = null,
        bool deployBlockedOrFailed = false,
        string? deployFailure = null,
        CriticalOpMarker? interruptedCriticalOp = null)
    {
        events ??= Array.Empty<DiagnosisEvent>();
        branch ??= TryLoadBranch(paths.BranchSwitchConfigPath);
        status ??= string.IsNullOrWhiteSpace(gamePath) ? null : new GameDetector().Detect(gamePath, lastLaunchRequestedAt);

        var marker = interruptedCriticalOp ?? TryLoadCriticalOp(paths.CriticalOpMarkerPath);
        var enabled = branch?.Enabled == true;
        var step = branch?.WizardStep ?? BranchWizardStep.None;
        var wizardBlocking = isAwaitingSteamSettle
            || settleAbandonedUnaligned
            || (step is not BranchWizardStep.None and not BranchWizardStep.Ready);

        var link = branch?.SteamLinkPath ?? "";
        var official = branch?.OfficialStorePath ?? "";
        var beta = branch?.BetaStorePath ?? "";
        var linkOk = SteamGameLocator.LooksLikeGameRoot(link);
        var officialOk = SteamGameLocator.LooksLikeGameRoot(official);
        var betaOk = SteamGameLocator.LooksLikeGameRoot(beta);
        var linkIncomplete = enabled && !linkOk && (officialOk || betaOk);

        var gameMissing = status?.Kind == GameStatusKind.GameMissing
            || string.IsNullOrWhiteSpace(gamePath)
            || !SteamGameLocator.LooksLikeGameRoot(gamePath);

        var junctions = new JunctionService();
        var isJunction = !string.IsNullOrWhiteSpace(link) && junctions.IsJunction(link);
        var dualBroken = enabled && (!isJunction || linkIncomplete);

        string? steamBetaKey = null;
        var steamUnhealthy = false;
        var acfNotSettled = false;
        if (enabled && !string.IsNullOrWhiteSpace(link))
        {
            try
            {
                var acfPath = SteamBetaKeyEditor.FindAppManifestPath(link);
                if (File.Exists(acfPath))
                {
                    var text = File.ReadAllText(acfPath);
                    steamUnhealthy = SteamBetaKeyEditor.LooksUpdateUnhealthy(text);
                    acfNotSettled = !SteamBetaKeyEditor.LooksSettledForSnapshot(text);
                    steamBetaKey = new SteamBetaKeyEditor(new StaticSteamProbe()).ReadBetaKey(acfPath);
                }
            }
            catch
            {
                // leave ACF flags false
            }
        }

        var active = branch?.ActiveBranch.ToString() ?? "";
        var mismatch = IsAcfBetaMismatch(steamBetaKey, active);

        var melonNeeds = !string.IsNullOrWhiteSpace(gamePath)
            && MelonLoaderVersionGate.ShouldForceUpgradeStore(gamePath, status?.MelonLoaderVersion);

        var networkFailed = events.Any(e =>
            e.Code is ManagerEventLog.CatalogFetchFailed or ManagerEventLog.UpdateCheckFailed);

        var lastNetwork = events.LastOrDefault(e =>
            e.Code is ManagerEventLog.CatalogFetchFailed or ManagerEventLog.UpdateCheckFailed);
        string? networkSource = null;
        if (lastNetwork?.Data is not null && lastNetwork.Data.TryGetValue("source", out var src))
            networkSource = src;
        networkSource ??= lastNetwork?.Code;

        return new DiagnosisSnapshot
        {
            HasInterruptedCriticalOp = marker is not null,
            CriticalOpKind = marker?.Kind.ToString(),
            CriticalOpDetail = marker?.Detail,
            IsWizardOrSettleBlocking = wizardBlocking,
            BranchWizardStep = step.ToString(),
            IsAwaitingSteamSettle = isAwaitingSteamSettle,
            SettleAbandonedUnaligned = settleAbandonedUnaligned,
            GameMissing = gameMissing,
            LinkIncompleteButStoreValid = linkIncomplete,
            SteamUpdateUnhealthy = steamUnhealthy,
            AcfNotSettled = acfNotSettled,
            MelonNeedsUpgrade = melonNeeds,
            MelonVersion = status?.MelonLoaderVersion,
            LoaderNotInjected = status?.LoaderInjected == false,
            LastLaunchRequestedAt = lastLaunchRequestedAt,
            LatestLogAge = status?.LatestLogAge,
            CatalogOrUpdateNetworkFailed = networkFailed,
            NetworkFailureSource = networkSource,
            DeployBlockedOrFailed = deployBlockedOrFailed,
            DeployFailure = deployFailure,
            AcfBetaMismatch = mismatch,
            SteamBetaKey = steamBetaKey,
            ActiveGameBranch = string.IsNullOrWhiteSpace(active) ? null : active,
            DualFolderLooksBroken = dualBroken,
            RecentEvents = events
        };
    }

    public static bool IsAcfBetaMismatch(string? steamBetaKey, string? activeGameBranch)
    {
        var key = (steamBetaKey ?? "").Trim();
        var hasBetaKey = key.Length > 0
            && !string.Equals(key, "public", StringComparison.OrdinalIgnoreCase);
        var managerBeta = string.Equals(activeGameBranch, nameof(GameBranch.Beta), StringComparison.OrdinalIgnoreCase);
        return hasBetaKey != managerBeta;
    }

    static CriticalOpMarker? TryLoadCriticalOp(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<CriticalOpMarker>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new CriticalOpMarker { Kind = CriticalOpKind.BranchDiskWrite, Detail = "unreadable" };
        }
    }

    static BranchSwitchConfig? TryLoadBranch(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<BranchSwitchConfig>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    sealed class StaticSteamProbe : ISteamRunningProbe
    {
        public bool IsSteamRunning() => false;
    }
}
