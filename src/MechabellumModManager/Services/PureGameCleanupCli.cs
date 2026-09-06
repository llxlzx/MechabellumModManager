using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// MechabellumModManager.exe --pure-game-cleanup [--game-path "..."] [--dry-run]
///   [--confirm-delete-other-store] [--skip-appdata] [--skip-melon]
/// </summary>
public static class PureGameCleanupCli
{
    public const string Arg = "--pure-game-cleanup";

    public static bool TryParseArgs(
        string[] args,
        out string? gamePath,
        out bool dryRun,
        out bool confirmDeleteOtherStore,
        out bool skipAppData,
        out bool skipMelon)
    {
        gamePath = null;
        dryRun = false;
        confirmDeleteOtherStore = false;
        skipAppData = false;
        skipMelon = false;
        if (args is null || args.Length == 0)
            return false;
        if (!args.Any(a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase)))
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--game-path", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                gamePath = args[++i];
            else if (string.Equals(args[i], "--dry-run", StringComparison.OrdinalIgnoreCase))
                dryRun = true;
            else if (string.Equals(args[i], "--confirm-delete-other-store", StringComparison.OrdinalIgnoreCase))
                confirmDeleteOtherStore = true;
            else if (string.Equals(args[i], "--skip-appdata", StringComparison.OrdinalIgnoreCase))
                skipAppData = true;
            else if (string.Equals(args[i], "--skip-melon", StringComparison.OrdinalIgnoreCase))
                skipMelon = true;
        }

        return true;
    }

    public static int Run(
        string? gamePathOverride,
        bool dryRun,
        bool confirmDeleteOtherStore,
        bool skipAppData,
        bool skipMelon)
    {
        try
        {
            var paths = new PathsService();
            var store = new JsonStore();
            var config = store.LoadOrDefault(paths.ConfigPath, () => new AppConfig());
            var branch = store.LoadOrDefault(paths.BranchSwitchConfigPath, () => new BranchSwitchConfig());

            var request = new PureGameCleanupRequest
            {
                GamePathOverride = gamePathOverride,
                ConfigGamePath = config.GamePath,
                Branch = branch,
                ManagerAppDataRoot = paths.DataRoot,
                SkipAppData = skipAppData,
                SkipMelon = skipMelon,
                ConfirmDeleteOtherStore = confirmDeleteOtherStore
            };

            BranchSwitchService? branchSvc = null;
            if (branch.Enabled)
            {
                var probe = new ProcessProbe();
                branchSvc = new BranchSwitchService(
                    paths,
                    store,
                    probe,
                    new JunctionService(),
                    new SteamBetaKeyEditor(probe));
            }

            var executor = new PureGameCleanupExecutor(
                new ProcessProbe(),
                branchSvc,
                msg => Log(msg));

            var result = executor.Execute(request, dryRun);
            Log($"exit={result.ExitCode} success={result.Success} {result.Message}");
            return result.ExitCode;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            return 1;
        }
    }

    static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                PathsService.AppFolderName);
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "pure-game-cleanup.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // ignore
        }

        try { Console.Error.WriteLine(message); }
        catch { /* ignore */ }
    }
}
