using System.IO;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class BranchOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool DegradeToManualBeta { get; init; }

    public static BranchOperationResult Ok(string message = "") =>
        new() { Success = true, Message = message };

    public static BranchOperationResult Fail(string message, bool degradeToManualBeta = false) =>
        new() { Success = false, Message = message, DegradeToManualBeta = degradeToManualBeta };
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

    public BranchSwitchConfig LoadConfig() =>
        _store.LoadOrDefault(_paths.BranchSwitchConfigPath, () => new BranchSwitchConfig());

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

        if (string.IsNullOrWhiteSpace(previousStore) || !LooksLikeGameRoot(previousStore))
            return BranchOperationResult.Fail("Current store is not a valid game root.");

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
                if (PathExists(dest))
                {
                    if (!PathsEqual(real, dest))
                        return BranchOperationResult.Fail("Store path already exists.");

                    _junctions.DeleteJunction(link);
                }
                else
                {
                    _junctions.DeleteJunction(link);
                    Directory.Move(real, dest);
                }
            }
            else if (Directory.Exists(link))
            {
                if (PathExists(dest))
                    return BranchOperationResult.Fail("Store path already exists.");

                Directory.Move(link, dest);
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
            return BranchOperationResult.Fail(ex.Message);
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
            Directory.Move(link, dest);
            if (PathExists(link))
                return BranchOperationResult.Fail("Steam link path still exists after archive.");

            SetStorePath(cfg, branch, dest);
            cfg.WizardStep = BranchWizardStep.ArchivedB;
            SaveConfig(cfg);
            return BranchOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return BranchOperationResult.Fail(ex.Message);
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

    static void CopyOnce(string source, string dest)
    {
        if (File.Exists(dest) || !File.Exists(source))
            return;

        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.Copy(source, dest);
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
