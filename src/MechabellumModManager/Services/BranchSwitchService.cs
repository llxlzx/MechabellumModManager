using System.IO;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>Stable English failure tokens for branch ops (map to UI in ViewModel).</summary>
public static class BranchOpMessages
{
    public const string GameRunning = "Game is running.";
    public const string GameOrSteamRunning = "Game or Steam is running.";
    public const string SteamLinkMissing = "Steam link path is not configured.";
    public const string StoreNotGameRoot = "Branch store is not a valid game root.";
    public const string ManifestMissing = "Manifest not found.";
    public const string ManifestNotSettled = "Manifest is not settled; skip snapshot.";
    public const string SnapshotNotAligned = "Live install is not aligned with the branch being snapshotted.";
    public const string SnapshotBetaKeyInvalid = "ACF BetaKey does not match the branch snapshot.";
    public const string SnapshotBuildIdCollision = "Official and beta ACF snapshots share the same buildid; refuse fake skip-download snapshot.";
}

public sealed class BranchOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool DegradeToManualBeta { get; init; }

    public static BranchOperationResult Ok(string message = "") =>
        new() { Success = true, Message = message };

    public static BranchOperationResult Fail(string message, bool degradeToManualBeta = false) =>
        new() { Success = false, Message = message, DegradeToManualBeta = degradeToManualBeta };

    public bool IsManifestNotSettled =>
        Message.Contains("not settled", StringComparison.OrdinalIgnoreCase);

    public bool IsGameRunningBlock =>
        string.Equals(Message, BranchOpMessages.GameRunning, StringComparison.Ordinal)
        || Message.Contains("Game is running", StringComparison.OrdinalIgnoreCase);
}


public sealed class OrphanDualLayoutInfo
{
    public static OrphanDualLayoutInfo None { get; } = new(false, false, "", null, Array.Empty<string>());

    public OrphanDualLayoutInfo(
        bool isOrphan,
        bool linkIsJunction,
        string steamLinkPath,
        string? junctionTarget,
        IReadOnlyList<string> leftoverStorePaths)
    {
        IsOrphan = isOrphan;
        LinkIsJunction = linkIsJunction;
        SteamLinkPath = steamLinkPath;
        JunctionTarget = junctionTarget;
        LeftoverStorePaths = leftoverStorePaths;
    }

    public bool IsOrphan { get; }
    public bool LinkIsJunction { get; }
    public string SteamLinkPath { get; }
    public string? JunctionTarget { get; }
    public IReadOnlyList<string> LeftoverStorePaths { get; }
}

public sealed class BranchSwitchService
{
    const string PhaseUnlinking = "unlinking";
    const string PhaseUnlinked = "unlinked";

    static readonly JsonSerializerOptions JournalJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    readonly PathsService _paths;
    readonly JsonStore _store;
    readonly IProcessProbe _probe;
    readonly IJunctionService _junctions;
    readonly SteamBetaKeyEditor _betaEditor;

    public BranchSwitchService(
        PathsService paths,
        JsonStore store,
        IProcessProbe probe,
        IJunctionService junctions,
        SteamBetaKeyEditor betaEditor)
    {
        _paths = paths;
        _store = store;
        _probe = probe;
        _junctions = junctions;
        _betaEditor = betaEditor;
    }

    public BranchSwitchConfig LoadConfig()
    {
        var cfg = _store.LoadOrDefault(_paths.BranchSwitchConfigPath, () => new BranchSwitchConfig());
        if (string.IsNullOrWhiteSpace(cfg.BetaBranchName))
            cfg.BetaBranchName = BranchSwitchConfig.DefaultSteamBetaBranchName;
        return cfg;
    }

    public void SaveConfig(BranchSwitchConfig config) =>
        _store.Save(_paths.BranchSwitchConfigPath, config);

    public BranchOperationResult TrySwapJunction(GameBranch target)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        var link = cfg.SteamLinkPath;
        var targetStore = StorePath(cfg, target);
        var previousBranch = cfg.ActiveBranch;
        var previousStore = StorePath(cfg, previousBranch);

        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(targetStore))
            return BranchOperationResult.Fail("Branch switch paths are not configured.");

        if (!LooksLikeGameRoot(targetStore))
            return BranchOperationResult.Fail("Target store is not a valid game root.");

        string? liveTarget = null;
        if (_junctions.IsJunction(link))
        {
            liveTarget = _junctions.ResolveTarget(link);
            if (liveTarget is not null && PathsEqual(liveTarget, targetStore))
            {
                cfg.ActiveBranch = target;
                SaveConfig(cfg);
                ClearJournal();
                return BranchOperationResult.Ok();
            }
        }
        else if (PathExists(link))
        {
            return BranchOperationResult.Fail("Steam link path is not a junction.");
        }
        else
        {
            return BranchOperationResult.Fail("Steam link path is missing.");
        }

        if (!string.IsNullOrWhiteSpace(liveTarget))
            previousStore = liveTarget;

        if (string.IsNullOrWhiteSpace(previousStore))
            return BranchOperationResult.Fail("Current store path is unknown.");

        // Steam may hollow the active store mid-download (exe left, GameAssembly gone).
        // Still allow leaving that store when the target is a complete game root.
        if (!Directory.Exists(previousStore))
            return BranchOperationResult.Fail("Current store is missing.");

        if (!string.IsNullOrWhiteSpace(liveTarget))
        {
            if (PathsEqual(liveTarget, StorePath(cfg, GameBranch.Official)))
                previousBranch = GameBranch.Official;
            else if (PathsEqual(liveTarget, StorePath(cfg, GameBranch.Beta)))
                previousBranch = GameBranch.Beta;
        }

        var journal = new BranchSwitchJournal
        {
            Phase = PhaseUnlinking,
            PreviousBranch = previousBranch,
            TargetBranch = target,
            SteamLinkPath = Path.GetFullPath(link),
            PreviousStorePath = Path.GetFullPath(previousStore),
            TargetStorePath = Path.GetFullPath(targetStore)
        };
        SaveJournal(journal);

        try
        {
            _junctions.DeleteJunction(link);
            journal.Phase = PhaseUnlinked;
            SaveJournal(journal);

            _junctions.CreateJunction(link, targetStore);
            cfg.ActiveBranch = target;
            SaveConfig(cfg);
            ClearJournal();
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            TryRestoreLink(link, previousStore);
            return BranchOperationResult.Fail(ex.Message);
        }
    }

    public BranchOperationResult TrySilentSetBeta(GameBranch target)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        if (string.IsNullOrWhiteSpace(cfg.SteamLinkPath))
            return BranchOperationResult.Fail("Steam link path is not configured.", degradeToManualBeta: true);

        var betaKey = target == GameBranch.Official ? null : cfg.BetaBranchName;
        if (target == GameBranch.Beta && string.IsNullOrWhiteSpace(betaKey))
            return BranchOperationResult.Fail("Beta branch name is not configured.", degradeToManualBeta: true);

        var acf = SteamBetaKeyEditor.FindAppManifestPath(cfg.SteamLinkPath);
        if (!File.Exists(acf))
            return BranchOperationResult.Fail("Manifest not found.", degradeToManualBeta: true);

        var currentKey = _betaEditor.ReadBetaKey(acf);
        var desiredKey = string.IsNullOrWhiteSpace(betaKey) ? null : betaKey.Trim();
        if (string.Equals(currentKey ?? "", desiredKey ?? "", StringComparison.Ordinal))
            return BranchOperationResult.Ok();

        var backupDir = Path.Combine(_paths.DataRoot, "steam-manifest-backups");
        try
        {
            var edit = _betaEditor.BackupAndSetBetaKey(acf, betaKey, backupDir);
            if (!edit.Success)
                return BranchOperationResult.Fail(edit.Message, degradeToManualBeta: true);

            cfg.ManifestBackupPath = edit.BackupPath;
            SaveConfig(cfg);
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(ex.Message, degradeToManualBeta: true);
        }
    }

    /// <summary>
    /// True when the Steam link junction and appmanifest BetaKey already match <paramref name="target"/>.
    /// </summary>
    public bool IsAlignedWith(GameBranch target)
    {
        var cfg = LoadConfig();
        if (string.IsNullOrWhiteSpace(cfg.SteamLinkPath))
            return false;

        var targetStore = StorePath(cfg, target);
        if (string.IsNullOrWhiteSpace(targetStore) || !LooksLikeGameRoot(targetStore))
            return false;

        if (!_junctions.IsJunction(cfg.SteamLinkPath))
            return false;

        var live = _junctions.ResolveTarget(cfg.SteamLinkPath);
        if (live is null || !PathsEqual(live, targetStore))
            return false;

        var acf = SteamBetaKeyEditor.FindAppManifestPath(cfg.SteamLinkPath);
        if (!File.Exists(acf))
            return false;

        var currentKey = _betaEditor.ReadBetaKey(acf);
        var mountedKey = _betaEditor.ReadMountedBetaKey(acf);
        var hasMounted = _betaEditor.HasMountedConfigBlock(acf);
        if (target == GameBranch.Official)
        {
            return string.IsNullOrWhiteSpace(currentKey)
                   && (!hasMounted || string.IsNullOrWhiteSpace(mountedKey));
        }

        var expected = cfg.BetaBranchName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(expected)
            || !string.Equals(currentKey ?? "", expected, StringComparison.Ordinal))
            return false;

        return !hasMounted || string.Equals(mountedKey ?? "", expected, StringComparison.Ordinal);
    }

    public BranchOperationResult TrySnapshotSettledAcf(GameBranch branch)
    {
        // Read-only live ACF → AppData snapshot. Steam may stay open; game running can lock files.
        if (_probe.IsGameRunning())
            return BranchOperationResult.Fail(BranchOpMessages.GameRunning);

        var cfg = LoadConfig();
        if (string.IsNullOrWhiteSpace(cfg.SteamLinkPath))
            return BranchOperationResult.Fail(BranchOpMessages.SteamLinkMissing);

        var store = StorePath(cfg, branch);
        if (string.IsNullOrWhiteSpace(store) || !LooksLikeGameRoot(store))
            return BranchOperationResult.Fail(BranchOpMessages.StoreNotGameRoot);

        if (!IsAlignedWith(branch))
            return BranchOperationResult.Fail(BranchOpMessages.SnapshotNotAligned);

        var acf = SteamBetaKeyEditor.FindAppManifestPath(cfg.SteamLinkPath);
        if (!File.Exists(acf))
            return BranchOperationResult.Fail(BranchOpMessages.ManifestMissing);

        var text = File.ReadAllText(acf);
        if (!SteamBetaKeyEditor.LooksSettledForSnapshot(text))
            return BranchOperationResult.Fail(BranchOpMessages.ManifestNotSettled);

        var betaName = cfg.BetaBranchName ?? BranchSwitchConfig.DefaultSteamBetaBranchName;
        if (!SteamAcfSnapshotSanitizer.IsValidSnapshot(text, branch, betaName))
            return BranchOperationResult.Fail(BranchOpMessages.SnapshotBetaKeyInvalid);

        var otherPath = _paths.GetSteamAcfSnapshotPath(
            branch == GameBranch.Official ? GameBranch.Beta : GameBranch.Official);
        if (File.Exists(otherPath))
        {
            try
            {
                var otherText = File.ReadAllText(otherPath);
                var officialText = branch == GameBranch.Official ? text : otherText;
                var betaText = branch == GameBranch.Beta ? text : otherText;
                if (SteamAcfSnapshotSanitizer.HasBuildIdCollision(officialText, betaText))
                    return BranchOperationResult.Fail(BranchOpMessages.SnapshotBuildIdCollision);
            }
            catch
            {
                // If sibling cannot be read, do not block save of a valid live snapshot.
            }
        }

        var snapshotPath = _paths.GetSteamAcfSnapshotPath(branch);
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        File.Copy(acf, snapshotPath, overwrite: true);
        return BranchOperationResult.Ok(snapshotPath);
    }

    public BranchOperationResult TryRestoreAcfSnapshot(GameBranch branch)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        if (string.IsNullOrWhiteSpace(cfg.SteamLinkPath))
            return BranchOperationResult.Fail("Steam link path is not configured.");

        var snapshotPath = _paths.GetSteamAcfSnapshotPath(branch);
        if (!File.Exists(snapshotPath))
            return BranchOperationResult.Fail("No ACF snapshot for target branch.");

        var snapshotText = File.ReadAllText(snapshotPath);
        if (!SteamBetaKeyEditor.LooksSettledForSnapshot(snapshotText))
            return BranchOperationResult.Fail("ACF snapshot is not settled.");

        var betaName = cfg.BetaBranchName ?? BranchSwitchConfig.DefaultSteamBetaBranchName;
        if (!SteamAcfSnapshotSanitizer.IsValidSnapshot(snapshotText, branch, betaName))
            return BranchOperationResult.Fail(BranchOpMessages.SnapshotBetaKeyInvalid);

        var otherPath = _paths.GetSteamAcfSnapshotPath(
            branch == GameBranch.Official ? GameBranch.Beta : GameBranch.Official);
        if (File.Exists(otherPath))
        {
            try
            {
                var otherText = File.ReadAllText(otherPath);
                var officialText = branch == GameBranch.Official ? snapshotText : otherText;
                var betaText = branch == GameBranch.Beta ? snapshotText : otherText;
                if (SteamAcfSnapshotSanitizer.HasBuildIdCollision(officialText, betaText))
                    return BranchOperationResult.Fail(BranchOpMessages.SnapshotBuildIdCollision);
            }
            catch
            {
                // ignore unreadable sibling
            }
        }

        var acf = SteamBetaKeyEditor.FindAppManifestPath(cfg.SteamLinkPath);
        if (!File.Exists(acf) && !Directory.Exists(Path.GetDirectoryName(acf)))
            return BranchOperationResult.Fail("Manifest directory missing.");

        var backupDir = Path.Combine(_paths.DataRoot, "steam-manifest-backups");
        Directory.CreateDirectory(backupDir);
        if (File.Exists(acf))
        {
            var backupPath = Path.Combine(
                backupDir,
                SteamBetaKeyEditor.ManifestFileName + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            File.Copy(acf, backupPath, overwrite: false);
            cfg.ManifestBackupPath = backupPath;
            SaveConfig(cfg);
        }

        File.Copy(snapshotPath, acf, overwrite: true);
        return BranchOperationResult.Ok();
    }

    public BranchOperationResult TryPrepareSteamBranchMetadata(GameBranch target)
    {
        var restored = TryRestoreAcfSnapshot(target);
        if (restored.Success)
            return BranchOperationResult.Ok("restored-acf-snapshot");

        return TrySilentSetBeta(target);
    }

    public BranchOperationResult TryRepairFromJournal()
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        if (!File.Exists(_paths.BranchSwitchJournalPath))
            return BranchOperationResult.Ok();

        if (!TryReadJournal(out var journal, out var journalError) || journal is null)
            return BranchOperationResult.Fail(journalError ?? "Journal cannot be applied.");

        var link = journal.SteamLinkPath;
        if (string.IsNullOrWhiteSpace(link))
            link = LoadConfig().SteamLinkPath;
        if (string.IsNullOrWhiteSpace(link))
            return BranchOperationResult.Fail("Journal is missing the Steam link path.");

        if (_junctions.IsJunction(link))
        {
            var live = _junctions.ResolveTarget(link);
            if (live is not null && !string.IsNullOrWhiteSpace(journal.PreviousStorePath)
                && PathsEqual(live, journal.PreviousStorePath))
            {
                var cfg = LoadConfig();
                cfg.ActiveBranch = journal.PreviousBranch;
                SaveConfig(cfg);
                ClearJournal();
                return BranchOperationResult.Ok();
            }

            if (live is not null && !string.IsNullOrWhiteSpace(journal.TargetStorePath)
                && PathsEqual(live, journal.TargetStorePath))
            {
                var cfg = LoadConfig();
                cfg.ActiveBranch = journal.TargetBranch;
                SaveConfig(cfg);
                ClearJournal();
                return BranchOperationResult.Ok();
            }

            return BranchOperationResult.Fail("Live junction does not match journal store paths.");
        }

        if (PathExists(link))
            return BranchOperationResult.Fail("Steam link path exists and is not a junction.");

        if (!LooksLikeGameRoot(journal.PreviousStorePath))
            return BranchOperationResult.Fail("Journal previous store is not a valid game root.");

        try
        {
            _junctions.CreateJunction(link, journal.PreviousStorePath);
            var cfg = LoadConfig();
            cfg.ActiveBranch = journal.PreviousBranch;
            SaveConfig(cfg);
            ClearJournal();
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(ex.Message);
        }
    }

    public BranchOperationResult ArchiveCurrentAs(GameBranch branch)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        var link = cfg.SteamLinkPath;
        var dest = StorePath(cfg, branch);
        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(dest))
            return BranchOperationResult.Fail("Branch switch paths are not configured.");

        dest = Path.GetFullPath(dest);

        try
        {
            if (_junctions.IsJunction(link))
            {
                var real = _junctions.ResolveTarget(link);
                if (string.IsNullOrWhiteSpace(real) || !Directory.Exists(real))
                    return BranchOperationResult.Fail("Junction target is missing.");

                real = Path.GetFullPath(real);
                if (!LooksLikeGameRoot(real))
                    return BranchOperationResult.Fail("Current game path is not a valid game root.");
                if (PathExists(dest))
                {
                    if (!PathsEqual(real, dest))
                        return BranchOperationResult.Fail("Store path already exists.");

                    _junctions.DeleteJunction(link);
                }
                else
                {
                    _junctions.DeleteJunction(link);
                    try
                    {
                        MoveDirectoryWithRetry(real, dest);
                    }
                    catch
                    {
                        // Junction already removed — restore Steam link or the library path is hollow.
                        if (Directory.Exists(real) && !PathExists(link))
                        {
                            try { _junctions.CreateJunction(link, real); }
                            catch { /* surface original move error */ }
                        }
                        throw;
                    }
                }
            }
            else if (Directory.Exists(link))
            {
                if (PathExists(dest))
                    return BranchOperationResult.Fail("Store path already exists.");

                if (!LooksLikeGameRoot(link))
                    return BranchOperationResult.Fail("Current game path is not a valid game root.");

                if (!TryProbeDirectoryWritable(link, out var probeError))
                    return BranchOperationResult.Fail(probeError);

                MoveDirectoryWithRetry(link, dest);
            }
            else
            {
                return BranchOperationResult.Fail("Steam link path is missing.");
            }

            if (PathExists(link))
                return BranchOperationResult.Fail("Steam link path still exists after archive.");

            SetStorePath(cfg, branch, dest);
            cfg.WizardStep = BranchWizardStep.ArchivedA;
            SaveConfig(cfg);
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(MapArchiveException(ex));
        }
    }

    public BranchOperationResult ArchiveDownloadedAs(GameBranch branch)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        var link = cfg.SteamLinkPath;
        var dest = StorePath(cfg, branch);
        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(dest))
            return BranchOperationResult.Fail("Branch switch paths are not configured.");

        dest = Path.GetFullPath(dest);
        if (PathExists(dest))
            return BranchOperationResult.Fail("Store path already exists.");

        if (_junctions.IsJunction(link))
            return BranchOperationResult.Fail("Downloaded path is a junction; refusing to archive the link.");

        if (!LooksLikeGameRoot(link))
            return BranchOperationResult.Fail("Downloaded path is not a valid game root.");

        try
        {
            if (!TryProbeDirectoryWritable(link, out var probeError))
                return BranchOperationResult.Fail(probeError);

            MoveDirectoryWithRetry(link, dest);
            if (PathExists(link))
                return BranchOperationResult.Fail("Steam link path still exists after archive.");

            SetStorePath(cfg, branch, dest);
            cfg.WizardStep = BranchWizardStep.ArchivedB;
            SaveConfig(cfg);
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(MapArchiveException(ex));
        }
    }

    public BranchOperationResult CreateLinkTo(GameBranch branch)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        var link = cfg.SteamLinkPath;
        var store = StorePath(cfg, branch);
        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(store))
            return BranchOperationResult.Fail("Branch switch paths are not configured.");

        store = Path.GetFullPath(store);
        link = Path.GetFullPath(link);

        if (_junctions.IsJunction(link))
        {
            var live = _junctions.ResolveTarget(link);
            if (!string.IsNullOrWhiteSpace(live) && PathsEqual(live, store))
            {
                cfg.ActiveBranch = branch;
                cfg.WizardStep = BranchWizardStep.Linked;
                SaveConfig(cfg);
                return BranchOperationResult.Ok();
            }

            return BranchOperationResult.Fail("Steam link path already exists.");
        }

        if (PathExists(link))
            return BranchOperationResult.Fail("Steam link path already exists.");

        if (!LooksLikeGameRoot(store))
            return BranchOperationResult.Fail("Store path is not a valid game root.");

        try
        {
            _junctions.CreateJunction(link, store);
            cfg.ActiveBranch = branch;
            cfg.WizardStep = BranchWizardStep.Linked;
            SaveConfig(cfg);
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(ex.Message);
        }
    }

    public void MigrateLegacyManifestIfNeeded(GameBranch current)
    {
        CopyOnce(_paths.DeployManifestPath, _paths.GetDeployManifestPath(current, enabled: true));
        CopyOnce(_paths.DeployManifestPrevPath, _paths.GetDeployManifestPrevPath(current, enabled: true));
    }

    public BranchOperationResult TryTeardown(bool deleteOtherStore)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var cfg = LoadConfig();
        var link = cfg.SteamLinkPath;
        if (string.IsNullOrWhiteSpace(link))
            return BranchOperationResult.Fail("Steam link path is not configured.");

        link = Path.GetFullPath(link);
        var currentStore = StorePath(cfg, cfg.ActiveBranch);
        if (!string.IsNullOrWhiteSpace(currentStore))
            currentStore = Path.GetFullPath(currentStore);

        var unlinkedStore = currentStore;
        var deletedJunction = false;

        try
        {
            if (_junctions.IsJunction(link))
            {
                var live = _junctions.ResolveTarget(link);
                if (!string.IsNullOrWhiteSpace(live))
                {
                    currentStore = Path.GetFullPath(live);
                    unlinkedStore = currentStore;
                }
                _junctions.DeleteJunction(link);
                deletedJunction = true;
            }

            try
            {
                // Prefer a complete store when the junction target was hollowed by Steam download/validate.
                var materializeFrom = currentStore;
                if (string.IsNullOrWhiteSpace(materializeFrom) || !LooksLikeGameRoot(materializeFrom))
                {
                    var fallback = ResolveCompleteOtherStore(cfg, currentStore, link);
                    if (!string.IsNullOrWhiteSpace(fallback))
                        materializeFrom = fallback;
                }

                if (!string.IsNullOrWhiteSpace(materializeFrom) && Directory.Exists(materializeFrom))
                {
                    if (!PathExists(link))
                        MoveDirectoryWithRetry(materializeFrom, link);
                    else if (!PathsEqual(materializeFrom, link))
                    {
                        RollbackTeardownLink(link, unlinkedStore, deletedJunction);
                        return BranchOperationResult.Fail("Steam link path already exists.");
                    }
                }

                if (!LooksLikeGameRoot(link))
                {
                    RollbackTeardownLink(link, unlinkedStore, deletedJunction);
                    return BranchOperationResult.Fail(
                        "Restored path is not a valid game root. If Steam is downloading into the active store, wait until it finishes or switch to the complete other store first.");
                }

                var currentBranch = cfg.ActiveBranch;
                if (!string.IsNullOrWhiteSpace(materializeFrom))
                {
                    if (!string.IsNullOrWhiteSpace(cfg.OfficialStorePath) && PathsEqual(materializeFrom, cfg.OfficialStorePath))
                        currentBranch = GameBranch.Official;
                    else if (!string.IsNullOrWhiteSpace(cfg.BetaStorePath) && PathsEqual(materializeFrom, cfg.BetaStorePath))
                        currentBranch = GameBranch.Beta;
                }

                var otherStore = OtherStorePath(cfg, materializeFrom);
                if (string.IsNullOrWhiteSpace(otherStore) || !Directory.Exists(otherStore))
                {
                    // Disk leftovers when JSON paths were cleared or current was hollow.
                    otherStore = SteamBranchLayout.EnumerateExistingStoreFolders(link)
                        .Select(Path.GetFullPath)
                        .FirstOrDefault(p => !PathsEqual(p, link) && !PathsEqual(p, materializeFrom));
                }

                if (deleteOtherStore
                    && !string.IsNullOrWhiteSpace(otherStore)
                    && Directory.Exists(otherStore)
                    && !PathsEqual(otherStore, link))
                {
                    Directory.Delete(otherStore, recursive: true);
                }

                // Hollow leftover that was the old junction target (not moved).
                if (deleteOtherStore
                    && !string.IsNullOrWhiteSpace(unlinkedStore)
                    && Directory.Exists(unlinkedStore)
                    && !PathsEqual(unlinkedStore, link)
                    && !LooksLikeGameRoot(unlinkedStore))
                {
                    try { Directory.Delete(unlinkedStore, recursive: true); }
                    catch { /* best-effort */ }
                }

                RestoreLegacyManifestFrom(currentBranch);

                cfg.Enabled = false;
                cfg.WizardStep = BranchWizardStep.None;
                cfg.OfficialStorePath = "";
                cfg.BetaStorePath = "";
                cfg.SteamLinkPath = link;
                SaveConfig(cfg);
                ClearJournal();
                return BranchOperationResult.Ok();
            }
            catch (Exception ex)
            {
                RollbackTeardownLink(link, unlinkedStore, deletedJunction);
                return BranchOperationResult.Fail(ex.Message);
            }
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Disk-aware orphan detection when dual-folder config is off but junction/leftover stores remain.
    /// </summary>
    public OrphanDualLayoutInfo InspectOrphanDualLayout(string? preferredLinkPath = null)
    {
        var cfg = LoadConfig();
        var link = ResolveOrphanLinkPath(preferredLinkPath, cfg);
        if (string.IsNullOrWhiteSpace(link))
            return OrphanDualLayoutInfo.None;

        try { link = Path.GetFullPath(link); }
        catch { return OrphanDualLayoutInfo.None; }

        var isJunction = PathExists(link) && _junctions.IsJunction(link);
        string? junctionTarget = null;
        if (isJunction)
        {
            var live = _junctions.ResolveTarget(link);
            if (!string.IsNullOrWhiteSpace(live))
            {
                try { junctionTarget = Path.GetFullPath(live); }
                catch { junctionTarget = live; }
            }
        }

        var leftovers = SteamBranchLayout.EnumerateExistingStoreFolders(link)
            .Where(LooksLikeGameRoot)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var isOrphan = isJunction || leftovers.Count > 0;
        return new OrphanDualLayoutInfo(isOrphan, isJunction, link, junctionTarget, leftovers);
    }

    /// <summary>
    /// Materialize Mechabellum if it is still a junction; optionally delete leftover store folders.
    /// Other-store discovery is disk-based (JSON paths may be empty after a partial teardown).
    /// </summary>
    public BranchOperationResult TryRepairOrphanDualLayout(bool deleteOtherStore, string? preferredLinkPath = null)
    {
        if (_probe.IsGameOrSteamRunning())
            return BranchOperationResult.Fail("Game or Steam is running.");

        var info = InspectOrphanDualLayout(preferredLinkPath);
        if (!info.IsOrphan)
            return BranchOperationResult.Ok("Nothing to repair.");

        var link = info.SteamLinkPath;
        string? unlinkedStore = null;
        var deletedJunction = false;

        try
        {
            if (info.LinkIsJunction)
            {
                var currentStore = info.JunctionTarget;
                if (string.IsNullOrWhiteSpace(currentStore) || !Directory.Exists(currentStore))
                    return BranchOperationResult.Fail("Junction target is missing.");

                currentStore = Path.GetFullPath(currentStore);
                unlinkedStore = currentStore;
                _junctions.DeleteJunction(link);
                deletedJunction = true;

                try
                {
                    if (!PathExists(link))
                        MoveDirectoryWithRetry(currentStore, link);
                    else if (!PathsEqual(currentStore, link))
                    {
                        RollbackTeardownLink(link, unlinkedStore, deletedJunction);
                        return BranchOperationResult.Fail("Steam link path already exists.");
                    }

                    if (!LooksLikeGameRoot(link))
                    {
                        RollbackTeardownLink(link, unlinkedStore, deletedJunction);
                        return BranchOperationResult.Fail("Restored path is not a valid game root.");
                    }
                }
                catch (Exception ex)
                {
                    RollbackTeardownLink(link, unlinkedStore, deletedJunction);
                    return BranchOperationResult.Fail(ex.Message);
                }
            }
            else if (!LooksLikeGameRoot(link))
            {
                return BranchOperationResult.Fail("Steam link path is not a valid game root.");
            }

            if (deleteOtherStore)
            {
                foreach (var store in SteamBranchLayout.EnumerateExistingStoreFolders(link)
                             .Where(LooksLikeGameRoot)
                             .Select(Path.GetFullPath))
                {
                    if (PathsEqual(store, link))
                        continue;
                    if (Directory.Exists(store))
                        Directory.Delete(store, recursive: true);
                }
            }

            var cfg = LoadConfig();
            cfg.Enabled = false;
            cfg.WizardStep = BranchWizardStep.None;
            cfg.SteamLinkPath = link;
            cfg.OfficialStorePath = "";
            cfg.BetaStorePath = "";
            SaveConfig(cfg);
            ClearJournal();
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            RollbackTeardownLink(link, unlinkedStore, deletedJunction);
            return BranchOperationResult.Fail(ex.Message);
        }
    }

    static string ResolveOrphanLinkPath(string? preferredLinkPath, BranchSwitchConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(preferredLinkPath))
        {
            try
            {
                var preferred = Path.GetFullPath(preferredLinkPath);
                if (SteamBranchLayout.TryGetCanonicalSteamLink(preferred, out var fromStore))
                    return fromStore;
                return preferred;
            }
            catch
            {
                // fall through
            }
        }

        if (!string.IsNullOrWhiteSpace(cfg.SteamLinkPath))
        {
            try { return Path.GetFullPath(cfg.SteamLinkPath); }
            catch { /* fall through */ }
        }

        foreach (var candidate in new[] { cfg.OfficialStorePath, cfg.BetaStorePath })
        {
            if (SteamBranchLayout.TryGetCanonicalSteamLink(candidate, out var link))
                return link;
        }

        return "";
    }

    void RollbackTeardownLink(string link, string? store, bool deletedJunction)
    {
        if (!deletedJunction || string.IsNullOrWhiteSpace(store))
            return;

        try
        {
            if (PathExists(link) && !_junctions.IsJunction(link) && !PathExists(store))
                MoveDirectoryWithRetry(link, store);

            if (!PathExists(link) && Directory.Exists(store))
            {
                _junctions.CreateJunction(link, store);
                return;
            }
        }
        catch
        {
            // Fall through to journal so a hollow link can be repaired later.
        }

        try
        {
            var cfg = LoadConfig();
            SaveJournal(new BranchSwitchJournal
            {
                Phase = PhaseUnlinked,
                PreviousBranch = cfg.ActiveBranch,
                TargetBranch = cfg.ActiveBranch,
                SteamLinkPath = Path.GetFullPath(link),
                PreviousStorePath = Path.GetFullPath(store),
                TargetStorePath = Path.GetFullPath(store)
            });
        }
        catch
        {
            // Best-effort journal.
        }
    }

    void RestoreLegacyManifestFrom(GameBranch current)
    {
        CopyOverwrite(_paths.GetDeployManifestPath(current, enabled: true), _paths.DeployManifestPath);
        CopyOverwrite(_paths.GetDeployManifestPrevPath(current, enabled: true), _paths.DeployManifestPrevPath);
    }

    static string? ResolveCompleteOtherStore(BranchSwitchConfig cfg, string? currentStore, string link)
    {
        var candidates = new List<string>();
        var configuredOther = OtherStorePath(cfg, currentStore);
        if (!string.IsNullOrWhiteSpace(configuredOther))
            candidates.Add(configuredOther);

        foreach (var path in new[] { cfg.OfficialStorePath, cfg.BetaStorePath })
        {
            if (!string.IsNullOrWhiteSpace(path))
                candidates.Add(path);
        }

        candidates.AddRange(SteamBranchLayout.EnumerateExistingStoreFolders(link));

        foreach (var raw in candidates)
        {
            try
            {
                var full = Path.GetFullPath(raw);
                if (PathsEqual(full, link)) continue;
                if (!string.IsNullOrWhiteSpace(currentStore) && PathsEqual(full, currentStore)) continue;
                if (LooksLikeGameRoot(full))
                    return full;
            }
            catch
            {
                // skip bad path
            }
        }

        return null;
    }

    static string OtherStorePath(BranchSwitchConfig cfg, string? currentStore)
    {
        if (!string.IsNullOrWhiteSpace(currentStore))
        {
            if (!string.IsNullOrWhiteSpace(cfg.OfficialStorePath) && PathsEqual(currentStore, cfg.OfficialStorePath))
                return cfg.BetaStorePath;
            if (!string.IsNullOrWhiteSpace(cfg.BetaStorePath) && PathsEqual(currentStore, cfg.BetaStorePath))
                return cfg.OfficialStorePath;
        }

        return cfg.ActiveBranch == GameBranch.Official ? cfg.BetaStorePath : cfg.OfficialStorePath;
    }

    static void CopyOnce(string source, string dest)
    {
        if (File.Exists(dest) || !File.Exists(source))
            return;

        CopyOverwrite(source, dest);
    }

    static void CopyOverwrite(string source, string dest)
    {
        if (!File.Exists(source))
            return;

        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.Copy(source, dest, overwrite: true);
    }

    void TryRestoreLink(string link, string previousStore)
    {
        try
        {
            if (_junctions.IsJunction(link) || PathExists(link) || !LooksLikeGameRoot(previousStore))
                return;

            _junctions.CreateJunction(link, previousStore);
            ClearJournal();
        }
        catch
        {
            // Leave the journal so TryRepairFromJournal can restore a hollow link.
        }
    }

    bool TryReadJournal(out BranchSwitchJournal? journal, out string? error)
    {
        journal = null;
        error = null;
        try
        {
            var json = File.ReadAllText(_paths.BranchSwitchJournalPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Journal is empty.";
                return false;
            }

            journal = JsonSerializer.Deserialize<BranchSwitchJournal>(json, JournalJsonOptions);
            if (journal is null)
            {
                error = "Journal is corrupt.";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            error = "Journal is corrupt.";
            return false;
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    void SaveJournal(BranchSwitchJournal journal)
    {
        var path = _paths.BranchSwitchJournalPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(journal, JournalJsonOptions);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    void ClearJournal()
    {
        try
        {
            if (File.Exists(_paths.BranchSwitchJournalPath))
                File.Delete(_paths.BranchSwitchJournalPath);
        }
        catch
        {
            // Best-effort; a leftover complete journal is repaired as a no-op if the link exists.
        }
    }

    static string StorePath(BranchSwitchConfig cfg, GameBranch branch) =>
        branch == GameBranch.Official ? cfg.OfficialStorePath : cfg.BetaStorePath;

    static void SetStorePath(BranchSwitchConfig cfg, GameBranch branch, string path)
    {
        if (branch == GameBranch.Official)
            cfg.OfficialStorePath = path;
        else
            cfg.BetaStorePath = path;
    }

    static bool LooksLikeGameRoot(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && File.Exists(Path.Combine(path, "Mechabellum.exe"))
        && File.Exists(Path.Combine(path, "GameAssembly.dll"));

    /// <summary>
    /// Directory.Move fails when Explorer/AV/indexer still holds a handle even after Steam exits.
    /// Retry briefly before surfacing access-denied guidance.
    /// </summary>
    /// <summary>
    /// Prefer same-volume rename; if Access Denied (AV/indexer/steamservice/cwd handle),
    /// clear read-only and fall back to copy+delete so dual-folder can finish without Steam UI.
    /// </summary>
    internal static void MoveDirectoryWithRetry(
        string source,
        string dest,
        int attempts = 10,
        TimeSpan? delay = null,
        Action<TimeSpan>? sleep = null)
    {
        if (attempts < 1)
            throw new ArgumentOutOfRangeException(nameof(attempts));

        delay ??= TimeSpan.FromMilliseconds(400);
        sleep ??= static span =>
        {
            if (span > TimeSpan.Zero)
                Thread.Sleep(span);
        };

        source = Path.GetFullPath(source);
        dest = Path.GetFullPath(dest);
        if (string.Equals(source, dest, StringComparison.OrdinalIgnoreCase))
            return;

        Exception? last = null;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                ClearReadOnlyAttributes(source);
                Directory.Move(source, dest);
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                last = ex;
                if (i + 1 >= attempts)
                    break;
                sleep(delay.Value);
            }
        }

        // Rename failed after retries — copy then delete (handles root-dir locks / stubborn ACL).
        try
        {
            ClearReadOnlyAttributes(source);
            if (Directory.Exists(dest))
                throw last ?? new IOException("Destination already exists after failed rename.");

            CopyDirectoryRecursive(source, dest);
            try
            {
                ClearReadOnlyAttributes(source);
                Directory.Delete(source, recursive: true);
            }
            catch (Exception delEx) when (delEx is UnauthorizedAccessException or IOException)
            {
                // Leave dest intact; do not roll back — source may still be partially locked.
                throw new IOException(
                    "已复制到目标目录，但删除原目录失败（仍有文件被占用）。请手动确认后重试或重启后再试。\n"
                    + "原始信息：" + delEx.Message,
                    delEx);
            }

            if (Directory.Exists(source))
                throw new IOException("Copy-delete move left the source directory in place.");
            return;
        }
        catch (IOException ex) when (ex.Message.StartsWith("已复制到目标目录", StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Roll back partial copy if source still exists.
            try
            {
                if (Directory.Exists(source) && Directory.Exists(dest))
                    Directory.Delete(dest, recursive: true);
            }
            catch
            {
                // ignore rollback errors
            }

            throw last ?? ex;
        }
    }

    internal static void ClearReadOnlyAttributes(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
            catch
            {
                // Best-effort; move/copy will surface hard failures.
            }
        }

        try
        {
            var rootAttrs = File.GetAttributes(root);
            if ((rootAttrs & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(root, rootAttrs & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // ignore
        }
    }

    static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(dest, name), overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(dir);
            CopyDirectoryRecursive(dir, Path.Combine(dest, name));
        }
    }

    static bool TryProbeDirectoryWritable(string dir, out string error)
    {
        error = "";
        try
        {
            if (!Directory.Exists(dir))
            {
                error = "目录不存在。";
                return false;
            }

            var probe = Path.Combine(dir, ".mmm_write_probe_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            error = MapArchiveException(ex);
            return false;
        }
    }

    internal static string MapArchiveException(Exception ex)
    {
        var msg = ex.Message ?? "";
        if (ex is UnauthorizedAccessException
            || msg.Contains("denied", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase))
        {
            return "无法移动游戏目录（访问被拒绝）。即使已退出 Steam 客户端与游戏，仍可能被 "
                   + "Steam 后台服务（steamservice）、Windows 搜索索引、杀软或其它程序占用目录句柄。"
                   + "可尝试：任务管理器结束 steamservice、重启后再开管理器（管理员）、"
                   + "确认未把游戏目录设为终端/IDE 当前目录。"
                   + "若不需要正式服/测试服双目录，可忽略向导，直接「应用并启动」使用 Mod。\n"
                   + "原始信息：" + msg;
        }

        return msg;
    }

    static bool PathExists(string path)
    {
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    static bool PathsEqual(string a, string b)
    {
        var fa = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fb = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fa, fb, StringComparison.OrdinalIgnoreCase);
    }
}
