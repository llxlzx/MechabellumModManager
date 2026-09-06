using System.Diagnostics;

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
