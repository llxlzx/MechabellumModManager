using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class PathsService
{
    public string DataRoot { get; }

    public PathsService(string? overrideRoot = null)
    {
        DataRoot = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MechabellumModManager")
            : overrideRoot;
    }

    public string ConfigPath => Path.Combine(DataRoot, "config.json");
    public string LibraryRoot => Path.Combine(DataRoot, "library");
    public string ProfilesDir => Path.Combine(DataRoot, "profiles");
    public string DeployManifestPath => Path.Combine(DataRoot, "deploy-manifest.json");
    public string DeployManifestPrevPath => Path.Combine(DataRoot, "deploy-manifest.prev.json");
    public string BranchSwitchConfigPath => Path.Combine(DataRoot, "branch-switch.json");
    public string BranchSwitchJournalPath => Path.Combine(DataRoot, "branch-switch-journal.json");
    public string LogsDir => Path.Combine(DataRoot, "logs");
    public string SteamAcfSnapshotsDir => Path.Combine(DataRoot, "steam-acf-snapshots");

    public string GetSteamAcfSnapshotPath(GameBranch branch) =>
        Path.Combine(
            SteamAcfSnapshotsDir,
            branch == GameBranch.Official ? "official.acf" : "beta.acf");

    public string GetDeployManifestPath(GameBranch? branch, bool enabled)
    {
        if (!enabled || branch is null)
            return DeployManifestPath;
        return branch == GameBranch.Official
            ? Path.Combine(DataRoot, "deploy-manifest.official.json")
            : Path.Combine(DataRoot, "deploy-manifest.beta.json");
    }

    public string GetDeployManifestPrevPath(GameBranch? branch, bool enabled)
    {
        if (!enabled || branch is null)
            return DeployManifestPrevPath;
        return branch == GameBranch.Official
            ? Path.Combine(DataRoot, "deploy-manifest.official.prev.json")
            : Path.Combine(DataRoot, "deploy-manifest.beta.prev.json");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(LibraryRoot);
        foreach (var sub in new[] { "mods", "plugins", "userlibs", "userdata" })
            Directory.CreateDirectory(Path.Combine(LibraryRoot, sub));
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(LogsDir);
    }
}
