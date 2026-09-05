using System.Diagnostics;
using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Headless MelonLoader install for Setup / repair:
/// MechabellumModManager.exe --install-melon-loader --game-path "..." [--redist-dir "..."]
/// Prefers offline MelonLoader.x64.zip (no GitHub / no PowerShell).
/// </summary>
public static class InstallMelonLoaderCli
{
    public const string Arg = "--install-melon-loader";

    public static bool TryParseArgs(string[] args, out string? gamePath, out string? redistDir)
    {
        gamePath = null;
        redistDir = null;
        if (args is null || args.Length == 0)
            return false;
        if (!args.Any(a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase)))
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--game-path", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                gamePath = args[++i];
            else if (string.Equals(args[i], "--redist-dir", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
                redistDir = args[++i];
        }

        return !string.IsNullOrWhiteSpace(gamePath);
    }

    /// <returns>0 ok, 1 fail, 2 game running, 3 missing zip, 4 invalid game path</returns>
    public static int Run(string gamePath, string? redistDir = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gamePath) || !SteamGameLocator.LooksLikeGameRoot(gamePath))
            {
                Log($"invalid game path: {gamePath}");
                return 4;
            }

            if (IsProcessRunning("Mechabellum"))
            {
                Log("Mechabellum is running");
                return 2;
            }

            var preferredZip = string.IsNullOrWhiteSpace(redistDir)
                ? null
                : Path.Combine(redistDir.Trim(), "melonloader", "MelonLoader.x64.zip");
            var zip = MelonLoaderDualStoreSync.ResolveLocalZip(preferredZip);
            if (zip is null)
            {
                Log("MelonLoader.x64.zip not found under redist / app folder");
                return 3;
            }

            var sync = new MelonLoaderDualStoreSync();
            var result = sync.InstallFromZip(gamePath.Trim(), zip);
            Log(result.Message);
            if (!result.Success)
                return 1;

            var status = new GameDetector().Detect(gamePath.Trim());
            if (status.Kind is GameStatusKind.Ready or GameStatusKind.LoaderPresentAssembliesMissing)
                return 0;

            Log($"post-install detect: {status.Kind} {status.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            return 1;
        }
    }

    static bool IsProcessRunning(string name)
    {
        try
        {
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MechabellumModManager");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "install-melon.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // ignore
        }
    }
}
