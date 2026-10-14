using System.Globalization;
using System.Resources;
using System.Runtime.InteropServices;

namespace MechabellumModManager.Services;

public static class LocalizationService
{
    public static readonly string[] SupportedCultures = ["zh-CN", "en", "ru", "ja", "de"];

    static readonly ResourceManager Resources =
        new("MechabellumModManager.Resources.Strings", typeof(LocalizationService).Assembly);

    /// <summary>
    /// Test hook. Production uses the OS UI language (not thread CurrentUICulture),
    /// because Apply() overwrites CurrentUICulture and would poison "follow system".
    /// </summary>
    internal static Func<CultureInfo>? SystemUiCultureProvider { get; set; }

    public const string DefaultFallback = "zh-CN";

    /// <param name="fallback">Language when Windows uses one the manager does not ship; set by edition.</param>
    public static string ResolveSystemLanguage(string fallback = DefaultFallback)
    {
        var ui = SystemUiCultureProvider?.Invoke() ?? GetOsUiCulture();
        return MatchOrFallback(ui, fallback);
    }

    public static string MatchOrFallback(CultureInfo culture, string fallback) =>
        MatchSupported(culture) ?? MatchSupported(culture.Parent) ?? fallback;

    public static string ResolveConfiguredLanguage(string? uiLanguage, string fallback = DefaultFallback)
    {
        if (string.IsNullOrWhiteSpace(uiLanguage) ||
            string.Equals(uiLanguage, "system", StringComparison.OrdinalIgnoreCase))
            return ResolveSystemLanguage(fallback);

        return MatchSupported(new CultureInfo(uiLanguage.Trim())) ?? fallback;
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

    static CultureInfo GetOsUiCulture()
    {
        try
        {
            // User's Windows display language — independent of CurrentUICulture we may have set.
            var lcid = GetUserDefaultUILanguage();
            return CultureInfo.GetCultureInfo(lcid);
        }
        catch
        {
            return CultureInfo.InstalledUICulture;
        }
    }

    [DllImport("kernel32.dll")]
    static extern ushort GetUserDefaultUILanguage();

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
