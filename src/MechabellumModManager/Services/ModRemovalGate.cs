using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed record ModRemovalHit(ModPackage Package, CatalogRemoval Removal);

/// <summary>
/// Decides which library packages and deployed files a catalog takedown applies to.
/// Only catalog id and file hash count. Display names are never used: a player's own mod that
/// happens to share a name must survive.
/// </summary>
public static class ModRemovalGate
{
    public static bool Matches(ModPackage pkg, CatalogRemoval removal)
    {
        ArgumentNullException.ThrowIfNull(pkg);
        ArgumentNullException.ThrowIfNull(removal);

        var id = (removal.Id ?? "").Trim();
        if (id.Length > 0 &&
            (string.Equals(pkg.CatalogId?.Trim(), id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pkg.Id?.Trim(), id, StringComparison.OrdinalIgnoreCase)))
            return true;

        var hashes = HashSet(removal);
        return hashes.Count > 0 && pkg.Files.Any(f => hashes.Contains((f.Sha256 ?? "").Trim()));
    }

    public static IReadOnlyList<ModRemovalHit> Plan(
        IEnumerable<ModPackage> library,
        IReadOnlyList<CatalogRemoval>? removed)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (removed is not { Count: > 0 })
            return [];

        var hits = new List<ModRemovalHit>();
        foreach (var pkg in library)
        {
            var removal = removed.FirstOrDefault(r => r is not null && Matches(pkg, r));
            if (removal is not null)
                hits.Add(new ModRemovalHit(pkg, removal));
        }

        return hits;
    }

    /// <summary>
    /// Deployed entries to clean up: files of a matched package, or bytes on the takedown list.
    /// The hash rule still finds a copy whose library package was already deleted.
    /// </summary>
    public static IReadOnlyList<ManifestFileEntry> ManifestHits(
        DeployManifest manifest,
        IReadOnlyList<CatalogRemoval>? removed,
        IReadOnlySet<string> hitPackageIds)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(hitPackageIds);

        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var removal in removed ?? [])
            hashes.UnionWith(HashSet(removal));

        return manifest.Files
            .Where(f => hitPackageIds.Contains(f.PackageId ?? "") ||
                        hashes.Contains((f.Sha256 ?? "").Trim()))
            .ToList();
    }

    /// <summary>Catalog entries that are also on the takedown list. Takedown wins.</summary>
    public static IReadOnlyList<CatalogMod> ListedAndRemoved(
        IEnumerable<CatalogMod> mods,
        IReadOnlyList<CatalogRemoval>? removed)
    {
        ArgumentNullException.ThrowIfNull(mods);
        if (removed is not { Count: > 0 })
            return [];

        var ids = new HashSet<string>(
            removed.Select(r => (r?.Id ?? "").Trim()).Where(id => id.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        return mods.Where(m => ids.Contains((m.Id ?? "").Trim())).ToList();
    }

    /// <summary>SHA-256 of zero bytes. Any package with an empty file would match it.</summary>
    public const string EmptyFileSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>
    /// Copy of the list that is safe to enforce: only full lowercase SHA-256 values, never the
    /// empty-file hash, and never bytes a listed catalog mod still ships (a renamed mod keeps its
    /// DLL, and listing the old id's history must not delete the new id).
    /// </summary>
    public static IReadOnlyList<CatalogRemoval> Sanitize(
        IReadOnlyList<CatalogRemoval>? removed,
        IEnumerable<CatalogMod> listed,
        out List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(listed);
        warnings = [];
        if (removed is not { Count: > 0 })
            return [];

        var shippedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in listed)
        {
            foreach (var hash in new[] { mod.Sha256 }.Concat(mod.Bundle?.Select(b => b.Sha256) ?? []))
            {
                var normalized = (hash ?? "").Trim();
                if (normalized.Length > 0)
                    shippedBy.TryAdd(normalized, mod.Id);
            }
        }

        var clean = new List<CatalogRemoval>(removed.Count);
        foreach (var removal in removed)
        {
            if (removal is null)
                continue;
            var id = (removal.Id ?? "").Trim();
            var label = id.Length > 0 ? id : removal.Name ?? "?";
            var hashes = new List<string>();
            foreach (var raw in removal.Sha256 ?? [])
            {
                var hash = (raw ?? "").Trim().ToLowerInvariant();
                if (hash.Length == 0)
                    continue;
                if (!IsSha256(hash) || hash == EmptyFileSha256)
                {
                    warnings.Add($"{label}: ignored hash {raw}");
                    continue;
                }

                if (shippedBy.TryGetValue(hash, out var listedId))
                {
                    warnings.Add($"{label}: ignored hash {hash} still shipped by {listedId}");
                    continue;
                }

                hashes.Add(hash);
            }

            if (id.Length == 0 && hashes.Count == 0)
                continue;
            clean.Add(new CatalogRemoval
            {
                Id = id,
                Name = removal.Name,
                Sha256 = hashes,
                Reason = removal.Reason,
                Since = removal.Since,
                Locales = removal.Locales
            });
        }

        return clean;
    }

    static bool IsSha256(string value) =>
        value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    static HashSet<string> HashSet(CatalogRemoval removal) =>
        new(
            (removal.Sha256 ?? []).Select(h => (h ?? "").Trim()).Where(h => h.Length > 0),
            StringComparer.OrdinalIgnoreCase);
}
