using MechabellumModManager.Services;

namespace MechabellumModManager.Models;

/// <summary>China or Global edition. One binary; the Setup picks it on a fresh install, Settings can switch it.</summary>
public enum AppEdition
{
    Cn,
    Global
}

public static class EditionPolicy
{
    public const string CnCode = "cn";
    public const string GlobalCode = "global";

    public static bool TryParse(string? raw, out AppEdition edition)
    {
        edition = AppEdition.Cn;
        var code = (raw ?? "").Trim();
        if (string.Equals(code, CnCode, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.Equals(code, GlobalCode, StringComparison.OrdinalIgnoreCase))
            return false;
        edition = AppEdition.Global;
        return true;
    }

    public static string Format(AppEdition edition) => edition == AppEdition.Cn ? CnCode : GlobalCode;

    public static int DefaultRoute(AppEdition edition) => edition == AppEdition.Cn
        ? (int)DownloadRoutePreference.AlwaysMirror
        : (int)DownloadRoutePreference.AlwaysGitHub;

    /// <summary>UI language used when the Windows display language is not one the manager ships.</summary>
    public static string LanguageFallback(AppEdition edition) => edition == AppEdition.Cn ? "zh-CN" : "en";

    public static MailProvider PreferredMail(AppEdition edition) =>
        edition == AppEdition.Cn ? MailProvider.Qq : MailProvider.Gmail;

    /// <summary>
    /// Edition for a config written before editions existed. An explicit route is the player's
    /// own statement about where they are; otherwise the Windows language decides.
    /// </summary>
    public static AppEdition Infer(int downloadRoutePreference, string systemLanguage)
    {
        if (downloadRoutePreference == (int)DownloadRoutePreference.AlwaysGitHub)
            return AppEdition.Global;
        if (downloadRoutePreference == (int)DownloadRoutePreference.AlwaysMirror)
            return AppEdition.Cn;
        return (systemLanguage ?? "").StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? AppEdition.Cn
            : AppEdition.Global;
    }

    /// <summary>
    /// The config's edition, inferring and recording one when it is missing or unreadable.
    /// Inference changes nothing else in the config. Returns true when the config must be saved.
    /// </summary>
    public static bool Resolve(AppConfig config, string systemLanguage, out AppEdition edition)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (TryParse(config.Edition, out edition))
            return false;

        edition = Infer(config.DownloadRoutePreference, systemLanguage);
        config.Edition = Format(edition);
        return true;
    }
}
