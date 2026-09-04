using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public static class ModTaxonomy
{
    public static readonly IReadOnlyList<ModCategory> CatalogWritableCategories =
    [
        ModCategory.OverlayUI, ModCategory.QoL, ModCategory.Camera,
        ModCategory.CombatAssist, ModCategory.Economy, ModCategory.ReplayDebug,
        ModCategory.Misc
    ];

    public static IReadOnlyList<ModCategory> AllFilterCategories { get; } =
        CatalogWritableCategories.Append(ModCategory.Uncategorized).ToArray();

    public static bool IsCatalogWritable(ModCategory c) =>
        c != ModCategory.Uncategorized;

    public static bool TryParseCategory(string? value, out ModCategory category)
    {
        category = ModCategory.Uncategorized;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Enum.TryParse(value.Trim(), ignoreCase: false, out ModCategory parsed))
            return false;
        if (!IsCatalogWritable(parsed)) return false;
        category = parsed;
        return true;
    }

    public static ModCategory ParseCategoryOrUncategorized(string? value) =>
        TryParseCategory(value, out var c) ? c : ModCategory.Uncategorized;

    public static ModCategory ResolveEffectiveCategory(string? categoryOverride, string? catalogCategory)
    {
        if (TryParseCategory(categoryOverride, out var o)) return o;
        if (TryParseCategory(catalogCategory, out var c)) return c;
        return ModCategory.Uncategorized;
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return Array.Empty<string>();
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in tags)
        {
            var s = (t ?? "").Trim();
            if (s.Length == 0) continue;
            if (seen.Add(s)) list.Add(s);
        }
        return list;
    }

    public static IReadOnlyList<string> ResolveEffectiveTags(
        IEnumerable<string>? catalogTags,
        IEnumerable<string>? extraTags) =>
        NormalizeTags((catalogTags ?? Array.Empty<string>()).Concat(extraTags ?? Array.Empty<string>()));

    /// <summary>
    /// Resource key for a catalog/library tag (stable English id → Tag_damage).
    /// </summary>
    public static string TagResourceKey(string tag)
    {
        var trimmed = (tag ?? "").Trim();
        if (trimmed.Length == 0) return "Tag_";
        var chars = trimmed.ToLowerInvariant().Select(c =>
            char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return "Tag_" + new string(chars).Trim('_');
    }

    /// <summary>
    /// Localized label for a tag id. Uses current UI culture; falls back to the raw tag if untranslated.
    /// </summary>
    public static string GetTagDisplayName(string tag)
    {
        var trimmed = (tag ?? "").Trim();
        if (trimmed.Length == 0) return "";
        var key = TagResourceKey(trimmed);
        var localized = LocalizationService.T(key);
        if (string.IsNullOrWhiteSpace(localized) ||
            string.Equals(localized, key, StringComparison.Ordinal))
            return trimmed;
        return localized;
    }

    public static string FormatTagsDisplay(IEnumerable<string>? tags)
    {
        var list = NormalizeTags(tags);
        if (list.Count == 0) return "";
        return string.Join(", ", list.Select(GetTagDisplayName));
    }
}
