using System.Diagnostics;

namespace MechabellumModManager.Services;

public sealed class ProcessProbe
{
    public bool IsGameRunning()
    {
        return Process.GetProcessesByName("Mechabellum").Length > 0;
    }
}
