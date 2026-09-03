namespace MechabellumModManager.Models;

public enum LaunchMode { SteamThenExe, SteamOnly, ExeOnly }

public sealed class AppConfig
{
    public string GamePath { get; set; } = "";
    public LaunchMode LaunchMode { get; set; } = LaunchMode.SteamThenExe;
    public string ActiveProfileId { get; set; } = "default";
    public string? DataRoot { get; set; }

    /// <summary>
    /// UI language: "system" or culture code (zh-CN, en, ru, ja, de).
    /// </summary>
    public string UiLanguage { get; set; } = "system";
}
