namespace MechabellumModManager.Models;

/// <summary>
/// Machine-wide install seed written under ProgramData by the elevated installer.
/// Merged into the daily user's AppData config on first launch when fields are blank.
/// </summary>
public sealed class InstallDefaults
{
    public string? GamePath { get; set; }
    public string? UiLanguage { get; set; }
}
