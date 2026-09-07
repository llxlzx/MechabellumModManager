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

    /// <summary>When the manager last requested a game launch (for Melon injection checks).</summary>
    public DateTimeOffset? LastLaunchRequestedAt { get; set; }

    /// <summary>
    /// Optional first-party mirror root (no trailing slash). Empty skips the mirror and uses GitHub only.
    /// Expected layout: {mirror}/MechabellumModManager/latest.json and {mirror}/MechabellumMods/...
    /// </summary>
    public string? MirrorBaseUrl { get; set; }
}
