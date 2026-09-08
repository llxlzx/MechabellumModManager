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

    /// <summary>Start of the last ApplyAndLaunch (Il2Cpp wait can write Latest.log before Launch stamps).</summary>
    public DateTimeOffset? LastApplyStartedAt { get; set; }

    /// <summary>
    /// First-party mirror root (no trailing slash).
    /// <c>null</c> = never configured (manager applies <see cref="Services.DomesticMirrorDefaults.BaseUrl"/>).
    /// <c>""</c> = player cleared the field (GitHub only; do not re-fill).
    /// Expected layout: {mirror}/MechabellumModManager/latest.json and {mirror}/MechabellumMods/...
    /// </summary>
    public string? MirrorBaseUrl { get; set; }

    /// <summary>
    /// Fetch the catalog on launch and say which installed mods are outdated. Never installs
    /// anything — swapping an injected DLL behind the player's back is not worth the convenience.
    /// On by default: a player who is not told is a player running a version the author has
    /// already fixed, and the cost is one catalog fetch.
    /// </summary>
    public bool CheckModUpdatesOnStartup { get; set; } = true;
}
