using System.Globalization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public static class CatalogLocaleResolver
{
    public static string ResolveName(CatalogMod mod, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return ResolveName(mod.Name, mod.Locales, culture);
    }

    public static string? ResolveSummary(CatalogMod mod, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return ResolveSummary(mod.Summary, mod.Locales, culture);
    }

    public static string ResolveName(
        string? defaultName,
        IReadOnlyDictionary<string, CatalogModLocale>? locales,
        string? culture = null)
    {
        var localized = Lookup(locales, culture)?.Name;
        return string.IsNullOrWhiteSpace(localized) ? (defaultName ?? "") : localized.Trim();
    }

    public static string? ResolveSummary(
        string? defaultSummary,
        IReadOnlyDictionary<string, CatalogModLocale>? locales,
        string? culture = null)
    {
        var localized = Lookup(locales, culture)?.Summary;
        if (!string.IsNullOrWhiteSpace(localized))
            return localized.Trim();
        return defaultSummary;
    }

    public static string? ResolvePreview(CatalogMod mod, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(mod);
        return ResolvePreview(mod.Preview, mod.Locales, culture);
    }

    public static string? ResolvePreview(
        string? defaultPreview,
        IReadOnlyDictionary<string, CatalogModLocale>? locales,
        string? culture = null)
    {
        var localized = Lookup(locales, culture)?.Preview;
        if (!string.IsNullOrWhiteSpace(localized))
            return localized.Trim();
        return string.IsNullOrWhiteSpace(defaultPreview) ? null : defaultPreview.Trim();
    }

    static CatalogModLocale? Lookup(
        IReadOnlyDictionary<string, CatalogModLocale>? locales,
        string? culture)
    {
        if (locales is null || locales.Count == 0)
            return null;

        var key = string.IsNullOrWhiteSpace(culture)
            ? LocalizationService.ResolveConfiguredLanguage(CultureInfo.CurrentUICulture.Name)
            : LocalizationService.ResolveConfiguredLanguage(culture);

        if (locales.TryGetValue(key, out var hit))
            return hit;

        // Case-insensitive fallback if dictionary comparer is ordinal
        foreach (var pair in locales)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return null;
    }
}
