using System.Diagnostics;

namespace MechabellumModManager.Services;

public interface ISteamRunningProbe
{
    bool IsSteamRunning();
}

public sealed class ProcessProbe : ISteamRunningProbe
{
    public bool IsGameRunning()
    {
        var processes = Process.GetProcessesByName("Mechabellum");
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

    public bool IsSteamRunning()
    {
        // Steam main process name is "steam" on Windows
        var processes = Process.GetProcessesByName("steam");
        try { return processes.Length > 0; }
        finally { foreach (var p in processes) p.Dispose(); }
    }

    public bool IsGameOrSteamRunning() => IsGameRunning() || IsSteamRunning();
}
