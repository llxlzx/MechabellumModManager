using Microsoft.Win32;

namespace MechabellumModManager.Services;

/// <summary>
/// Steam can keep Apps\669330 Running=1 and RunningAppID after the game
/// process is killed or crashes, then refuse steam://rungameid with "already running".
/// </summary>
public static class SteamAppRunningState
{
    public const int MechabellumAppId = 669330;

    public static bool IsStaleWithoutProcess(bool gameProcessRunning, int? appRunning, int? runningAppId)
    {
        if (gameProcessRunning)
            return false;
        if (appRunning == 1)
            return true;
        return runningAppId == MechabellumAppId;
    }

    public static bool ReadIsStaleWithoutProcess(bool gameProcessRunning)
    {
        try
        {
            return IsStaleWithoutProcess(
                gameProcessRunning,
                ReadDword(@"Software\Valve\Steam\Apps\" + MechabellumAppId, "Running"),
                ReadDword(@"Software\Valve\Steam", "RunningAppID"));
        }
        catch
        {
            return false;
        }
    }

    static int? ReadDword(string subKey, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        return key?.GetValue(name) is int value ? value : null;
    }
}
