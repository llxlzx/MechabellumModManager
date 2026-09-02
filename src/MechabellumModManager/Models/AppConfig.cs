namespace MechabellumModManager.Models;

public enum LaunchMode { SteamThenExe, SteamOnly, ExeOnly }

public sealed class AppConfig
{
    public string GamePath { get; set; } = "";
    public LaunchMode LaunchMode { get; set; } = LaunchMode.SteamThenExe;
    public string ActiveProfileId { get; set; } = "default";
    public string? DataRoot { get; set; }
}
