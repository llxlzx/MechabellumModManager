using MechabellumModManager.Services;

namespace MechabellumModManager.Tests.Support;

public sealed class FakeProcessProbe : IProcessProbe
{
    public bool GameRunning { get; set; }
    public bool SteamRunning { get; set; }
    public int ForceCloseCalls { get; private set; }
    /// <summary>When true, ForceClose clears running flags (simulates successful kill).</summary>
    public bool ForceCloseClearsRunning { get; set; }

    public bool IsGameRunning() => GameRunning;
    public bool IsSteamRunning() => SteamRunning;
    public bool IsGameOrSteamRunning() => GameRunning || SteamRunning;

    public void ForceCloseSteamClientAndGame()
    {
        ForceCloseCalls++;
        if (ForceCloseClearsRunning)
        {
            SteamRunning = false;
            GameRunning = false;
        }
    }
}
