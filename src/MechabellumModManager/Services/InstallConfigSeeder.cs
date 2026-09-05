using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Writes first-run AppData config + ProgramData install-defaults without PowerShell.
/// Used by the installer via: MechabellumModManager.exe --seed-install-config ...
/// </summary>
public static class InstallConfigSeeder
{
    public const string SeedArg = "--seed-install-config";

    public static bool TryParseArgs(string[] args, out string? gamePath, out string? uiLanguage)
    {
        gamePath = null;
        uiLanguage = null;
        if (args is null || args.Length == 0)
            return false;
        if (!args.Any(a => string.Equals(a, SeedArg, StringComparison.OrdinalIgnoreCase)))
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--game-path", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                gamePath = args[++i];
            else if (string.Equals(args[i], "--ui-language", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
                uiLanguage = args[++i];
        }

        return !string.IsNullOrWhiteSpace(gamePath);
    }

    public static void Seed(string gamePath, string? uiLanguage, string? appDataRoot = null, string? programDataRoot = null)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            throw new ArgumentException("Game path is required.", nameof(gamePath));

        var appRoot = appDataRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MechabellumModManager");
        Directory.CreateDirectory(Path.Combine(appRoot, "library", "mods"));
        Directory.CreateDirectory(Path.Combine(appRoot, "library", "plugins"));
        Directory.CreateDirectory(Path.Combine(appRoot, "library", "userlibs"));
        Directory.CreateDirectory(Path.Combine(appRoot, "library", "userdata"));
        Directory.CreateDirectory(Path.Combine(appRoot, "profiles"));
        Directory.CreateDirectory(Path.Combine(appRoot, "logs"));

        var store = new JsonStore();
        var configPath = Path.Combine(appRoot, "config.json");
        var config = store.LoadOrDefault(configPath, () => new AppConfig());
        config.GamePath = gamePath.Trim();
        if (string.IsNullOrWhiteSpace(config.ActiveProfileId))
            config.ActiveProfileId = "default";
        if (!string.IsNullOrWhiteSpace(uiLanguage))
            config.UiLanguage = uiLanguage.Trim();
        store.Save(configPath, config);

        var seedRoot = programDataRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "MechabellumModManager");
        Directory.CreateDirectory(seedRoot);
        store.Save(
            Path.Combine(seedRoot, "install-defaults.json"),
            new InstallDefaults
            {
                GamePath = config.GamePath,
                UiLanguage = string.IsNullOrWhiteSpace(config.UiLanguage) ? null : config.UiLanguage
            });

        var profilePath = Path.Combine(appRoot, "profiles", "default.json");
        if (!File.Exists(profilePath))
        {
            store.Save(profilePath, new Profile
            {
                Id = "default",
                Name = "默认",
                EnabledPackageIds = []
            });
        }
    }
}
