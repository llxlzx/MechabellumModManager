using System.Globalization;
using System.Resources;

namespace MechabellumModManager.Services;

public static class LocalizationService
{
    public static readonly string[] SupportedCultures = ["zh-CN", "en", "ru", "ja", "de"];

    static readonly ResourceManager Resources =
        new("MechabellumModManager.Resources.Strings", typeof(LocalizationService).Assembly);

    public static string ResolveSystemLanguage()
    {
        var ui = CultureInfo.CurrentUICulture;
        return MatchSupported(ui) ?? MatchSupported(ui.Parent) ?? "zh-CN";
    }

    public static string ResolveConfiguredLanguage(string? uiLanguage)
    {
        if (string.IsNullOrWhiteSpace(uiLanguage) ||
            string.Equals(uiLanguage, "system", StringComparison.OrdinalIgnoreCase))
            return ResolveSystemLanguage();

        return MatchSupported(new CultureInfo(uiLanguage.Trim())) ?? "zh-CN";
    }

    public static void Apply(string cultureName)
    {
        var name = MatchSupported(new CultureInfo(cultureName)) ?? "zh-CN";
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        try
        {
            return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    public static string T(string key) => GetString(key);

    static string? MatchSupported(CultureInfo? culture)
    {
        if (culture is null || string.IsNullOrWhiteSpace(culture.Name))
            return null;

        foreach (var supported in SupportedCultures)
        {
            if (string.Equals(culture.Name, supported, StringComparison.OrdinalIgnoreCase))
                return supported;
        }

        // zh / zh-Hans → zh-CN
        if (culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";

        foreach (var supported in SupportedCultures)
        {
            if (supported.StartsWith(culture.TwoLetterISOLanguageName + "-", StringComparison.OrdinalIgnoreCase) ||
                supported.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                return supported;
        }

        return null;
    }
}
