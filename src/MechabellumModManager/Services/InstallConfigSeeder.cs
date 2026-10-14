using System.Globalization;
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

    public static bool TryParseArgs(
        string[] args,
        out string? gamePath,
        out string? uiLanguage,
        out int? downloadRoute,
        out string? edition)
    {
        gamePath = null;
        uiLanguage = null;
        downloadRoute = null;
        edition = null;
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
            else if (string.Equals(args[i], "--download-route", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
            {
                var raw = args[++i];
                if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var route)
                    && DownloadRoutePolicy.IsKnownPreference(route))
                    downloadRoute = route;
            }
            else if (string.Equals(args[i], "--edition", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
            {
                if (EditionPolicy.TryParse(args[++i], out var parsed))
                    edition = EditionPolicy.Format(parsed);
            }
        }

        return !string.IsNullOrWhiteSpace(gamePath);
    }

    /// <param name="downloadRoute">
    /// Route preset of the Setup flavor. Written only into a config file that does not exist yet,
    /// so a route the player already chose survives reinstalls.
    /// </param>
    public static void Seed(
        string gamePath,
        string? uiLanguage,
        int? downloadRoute = null,
        string? appDataRoot = null,
        string? programDataRoot = null,
        string? edition = null)
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
        var configExisted = File.Exists(configPath);
        var config = store.LoadOrDefault(configPath, () => new AppConfig());
        config.GamePath = gamePath.Trim();
        if (string.IsNullOrWhiteSpace(config.ActiveProfileId))
            config.ActiveProfileId = "default";
        if (!string.IsNullOrWhiteSpace(uiLanguage))
            config.UiLanguage = uiLanguage.Trim();
        var presetRoute = DownloadRoutePolicy.IsKnownPreference(downloadRoute);
        if (!configExisted && presetRoute)
            config.DownloadRoutePreference = downloadRoute!.Value;
        // Only a fresh config takes the flavor's edition; an update delivered as the other
        // package must not relabel a player who never chose it.
        var flavorEdition = EditionPolicy.TryParse(edition, out var parsedEdition) ? EditionPolicy.Format(parsedEdition) : null;
        if (!configExisted && flavorEdition is not null)
            config.Edition = flavorEdition;
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
                UiLanguage = string.IsNullOrWhiteSpace(config.UiLanguage) ? null : config.UiLanguage,
                DownloadRoutePreference = configExisted || presetRoute ? config.DownloadRoutePreference : null,
                Edition = EditionPolicy.TryParse(config.Edition, out var seedEdition)
                    ? EditionPolicy.Format(seedEdition)
                    : flavorEdition
            });

        var profilePath = Path.Combine(appRoot, "profiles", "default.json");
        if (!File.Exists(profilePath))
        {
            store.Save(profilePath, new Profile
            {
                Id = "default",
                Name = LocalizationService.T("DefaultProfileName"),
                EnabledPackageIds = []
            });
        }
    }
}
