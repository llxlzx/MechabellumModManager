using System.Diagnostics;
using System.IO;

namespace MechabellumModManager.Services;

public interface ISteamRunningProbe
{
    bool IsSteamRunning();
}

public interface IProcessProbe : ISteamRunningProbe
{
    bool IsGameRunning();
    bool IsGameOrSteamRunning();

    /// <summary>
    /// Best-effort kill of Steam client UI processes and the game.
    /// Does not touch steamservice (Windows service often stays after client exit).
    /// Needed because steam://exit often ignores the multi-account “Who’s playing?” picker.
    /// </summary>
    void ForceCloseSteamClientAndGame();

    /// <summary>Best-effort path of a running Mechabellum.exe; null if none or inaccessible.</summary>
    string? TryGetRunningGameExePath();
}

public sealed class ProcessProbe : IProcessProbe
{
    static readonly string[] SteamClientProcessNames =
    [
        "steam",
        "steamwebhelper",
        "GameOverlayUI",
        "steamerrorreporter"
    ];

    public bool IsGameRunning()
    {
        return HasProcess("Mechabellum");
    }

    public string? TryGetRunningGameExePath()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("Mechabellum");
        }
        catch
        {
            return null;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
                catch
                {
                    // Access denied / process exiting.
                }
            }

            return null;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    /// <summary>
    /// True when we cannot tell, or the running exe is the manager GamePath.
    /// False when Steam (or another launcher) opened a different library.
    /// </summary>
    public static bool IsManagedGameExe(string gamePath, string? runningExe)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || string.IsNullOrWhiteSpace(runningExe))
            return true;

        try
        {
            var expected = Path.GetFullPath(Path.Combine(gamePath.Trim(), GameLauncher.GameExeName));
            var actual = Path.GetFullPath(runningExe.Trim());
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    public bool IsSteamRunning()
    {
        // Client / UI only. Do NOT treat steamservice as "Steam running":
        // that Windows service often stays up after the client exits; counting it
        // caused WaitForExit to call steam://exit and relaunch the account picker.
        foreach (var name in SteamClientProcessNames)
        {
            if (HasProcess(name))
                return true;
        }

        return false;
    }

    public bool IsGameOrSteamRunning() => IsGameRunning() || IsSteamRunning();

    public void ForceCloseSteamClientAndGame()
    {
        foreach (var name in SteamClientProcessNames)
            TryKillByName(name);
        TryKillByName("Mechabellum");
    }

    static bool HasProcess(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    static void TryKillByName(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            return;
        }

        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort; access denied / already exiting is fine.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
