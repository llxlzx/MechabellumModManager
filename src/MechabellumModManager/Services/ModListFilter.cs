using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public enum ModSortMode
{
    NameAsc,
    UpdatedAtDesc
}

public static class ModListFilter
{
    public static bool MatchesSearch(string? search, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var q = search.Trim();
        foreach (var field in fields)
        {
            if (!string.IsNullOrEmpty(field) &&
                field.Contains(q, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool MatchesCategory(ModCategory? filter, ModCategory effective) =>
        filter is null || filter == effective;

    public static bool MatchesTag(string? filterTag, IEnumerable<string> effectiveTags)
    {
        if (string.IsNullOrWhiteSpace(filterTag)) return true;
        var needle = filterTag.Trim();
        return effectiveTags.Any(t => string.Equals(t, needle, StringComparison.Ordinal));
    }

    public static int CompareUpdatedAtDesc(string? a, string? b)
    {
        var da = TryParseDate(a);
        var db = TryParseDate(b);
        if (da is null && db is null) return 0;
        if (da is null) return 1;
        if (db is null) return -1;
        return db.Value.CompareTo(da.Value);
    }

    static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AllowWhiteSpaces,
            out var dt)
            ? dt
            : null;
    }
}
