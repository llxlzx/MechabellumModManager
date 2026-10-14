namespace MechabellumModManager.Models;

/// <summary>
/// Machine-wide install seed written under ProgramData by the elevated installer.
/// Merged into the daily user's AppData config on first launch when fields are blank.
/// </summary>
public sealed class InstallDefaults
{
    public string? GamePath { get; set; }
    public string? UiLanguage { get; set; }

    /// <summary>Route preset of the Setup flavor (1 = CN, 2 = Global). Null from older installers.</summary>
    public int? DownloadRoutePreference { get; set; }

    /// <summary>"cn" or "global" from the Setup flavor. Null from older installers.</summary>
    public string? Edition { get; set; }
}
