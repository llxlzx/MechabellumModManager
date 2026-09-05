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
}

public sealed class ProcessProbe : IProcessProbe
{
    public bool IsGameRunning()
    {
        return HasProcess("Mechabellum");
    }

    public bool IsSteamRunning()
    {
        // steam.exe can exit briefly while steamwebhelper is still shutting down / holding files.
        // Editing appmanifest in that window commonly crashes the Steam client on next start.
        return HasProcess("steam")
            || HasProcess("steamwebhelper")
            || HasProcess("GameOverlayUI")
            || HasProcess("steamerrorreporter");
    }

    public bool IsGameOrSteamRunning() => IsGameRunning() || IsSteamRunning();

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
}
