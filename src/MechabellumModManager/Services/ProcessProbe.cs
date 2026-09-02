using System.Diagnostics;

namespace MechabellumModManager.Services;

public sealed class ProcessProbe
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
}
