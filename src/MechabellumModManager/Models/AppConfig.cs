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

    /// <summary>Minutes catalog stays Hot before a Warm probe. Default 15. Not shown in Settings UI.</summary>
    public int CatalogHotCacheMinutes { get; set; } = 15;

    /// <summary>
    /// When true, write MelonLoader <c>[console] hide_console = true</c> for the current
    /// game path (and dual stores when enabled). Takes effect on the next game launch.
    /// </summary>
    public bool HideMelonConsole { get; set; }

    /// <summary>Whitelist invite code for in-app direct publish (plaintext in config.json).</summary>
    public string AuthorInviteCode { get; set; } = "";

    /// <summary>
    /// Override for the direct-upload Worker base URL (no trailing slash).
    /// Null/empty = use <see cref="Services.DirectUploadDefaults.ApiBaseUrl"/>.
    /// </summary>
    public string? DirectUploadApiBaseUrl { get; set; }

    /// <summary>
    /// When true, startup opens the library instead of the getting-started page.
    /// Not tied to the manager version. The installer must keep this field, or every Setup update reopens the guide.
    /// </summary>
    public bool OnboardingDismissed { get; set; }

    /// <summary>
    /// Extra interface scale on top of Windows DPI. Null or "auto" follows the screen: 125% from 1440p,
    /// 150% for 1800p and 4K, 175% for 5K, 200% above that, when Windows itself is still near 100%.
    /// Explicit values: "1", "1.25", "1.5", "1.75", "2". The installer must keep this field.
    /// </summary>
    public string? UiScale { get; set; }
}
