namespace MechabellumModManager.Services;

/// <summary>
/// Curated workshop picks, in display order. The first id leads the strip.
/// Names and status still come from the catalog; a missing id is skipped.
/// </summary>
public static class FeaturedCatalog
{
    public static readonly string[] Ids =
    [
        "battle-suite",
        "friend-overlay",
        "replay-reset",
        "sales-calculation",
        "team-beacon",
    ];

    public static IReadOnlyList<T> Pick<T>(IEnumerable<T> mods, Func<T, string?> id)
    {
        var byId = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            var key = id(mod);
            if (string.IsNullOrWhiteSpace(key) || byId.ContainsKey(key))
                continue;
            byId[key] = mod;
        }

        var picked = new List<T>();
        foreach (var featuredId in Ids)
        {
            if (byId.TryGetValue(featuredId, out var mod))
                picked.Add(mod);
        }

        return picked;
    }
}
