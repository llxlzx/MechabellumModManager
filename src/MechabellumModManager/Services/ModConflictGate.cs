using System.Globalization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public enum ModConflictCause
{
    PartnerInLibrary,
    PartnerInSelection
}

public sealed record ModConflictBlock(
    string IncomingId,
    string PartnerId,
    ModConflictCause Cause,
    string IncomingName,
    string PartnerName,
    string? Reason);

public sealed record ModConflictText(
    string InstalledWithReason,
    string InstalledGeneric,
    string SelectionWithReason,
    string SelectionGeneric);

public sealed class ModConflictDecision
{
    public ModConflictDecision(
        IReadOnlyList<string> allowedIds,
        IReadOnlyList<ModConflictBlock> blocks)
    {
        AllowedIds = allowedIds;
        Blocks = blocks;
    }

    public IReadOnlyList<string> AllowedIds { get; }
    public IReadOnlyList<ModConflictBlock> Blocks { get; }
}

/// <summary>
/// Decides which catalog mods may be added together. The library argument is the
/// library from before this operation, so packages imported moments ago are not
/// treated as updates.
/// </summary>
public static class ModConflictGate
{
    public static ModConflictDecision Evaluate(
        IReadOnlyList<CatalogMod> catalog,
        IEnumerable<ModPackage> library,
        IReadOnlyList<string> requestedIds,
        string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(requestedIds);

        var byId = Index(catalog);
        var libraryList = library as IReadOnlyList<ModPackage> ?? library.ToList();

        bool InLibrary(string id) =>
            byId.TryGetValue(id, out var mod) && ModCatalogService.IsInLibrary(libraryList, mod);

        var requested = new List<string>();
        var seenRequested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in requestedIds)
        {
            var id = (raw ?? "").Trim();
            if (id.Length == 0 || !byId.ContainsKey(id) || !seenRequested.Add(id))
                continue;
            requested.Add(byId[id].Id);
        }

        var fresh = new HashSet<string>(
            requested.Where(id => !InLibrary(id)),
            StringComparer.OrdinalIgnoreCase);

        var blocks = new List<ModConflictBlock>();
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var incomingId in fresh)
        {
            foreach (var partnerId in PartnerIds(byId, incomingId))
            {
                ModConflictCause cause;
                if (InLibrary(partnerId))
                    cause = ModConflictCause.PartnerInLibrary;
                else if (fresh.Contains(partnerId))
                    cause = ModConflictCause.PartnerInSelection;
                else
                    continue;

                blocked.Add(incomingId);
                var incoming = byId[incomingId];
                var partner = byId[partnerId];
                blocks.Add(new ModConflictBlock(
                    incoming.Id,
                    partner.Id,
                    cause,
                    CatalogLocaleResolver.ResolveName(incoming, culture),
                    CatalogLocaleResolver.ResolveName(partner, culture),
                    ResolveReason(byId, incoming.Id, partner.Id, culture)));
            }
        }

        var allowed = requested.Where(id => !blocked.Contains(id)).ToList();
        return new ModConflictDecision(allowed, blocks);
    }

    public static string Format(IReadOnlyList<ModConflictBlock> blocks, ModConflictText text)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(text);

        var lines = new List<string>();
        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in blocks)
        {
            if (block.Cause == ModConflictCause.PartnerInLibrary)
            {
                lines.Add(string.IsNullOrWhiteSpace(block.Reason)
                    ? string.Format(CultureInfo.InvariantCulture, text.InstalledGeneric, block.IncomingName, block.PartnerName)
                    : string.Format(CultureInfo.InvariantCulture, text.InstalledWithReason, block.IncomingName, block.PartnerName, block.Reason));
                continue;
            }

            var pairKey = PairKey(block.IncomingId, block.PartnerId);
            if (!seenPairs.Add(pairKey))
                continue;

            var group = blocks.Where(item =>
                item.Cause == ModConflictCause.PartnerInSelection &&
                string.Equals(PairKey(item.IncomingId, item.PartnerId), pairKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var (leftId, rightId) = OrderedIds(block.IncomingId, block.PartnerId);
            var reason = group.Select(item => item.Reason).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
            lines.Add(string.IsNullOrWhiteSpace(reason)
                ? string.Format(CultureInfo.InvariantCulture, text.SelectionGeneric, NameFor(group, leftId), NameFor(group, rightId))
                : string.Format(CultureInfo.InvariantCulture, text.SelectionWithReason, NameFor(group, leftId), NameFor(group, rightId), reason));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Imported packages that do not match a blocked catalog id. A package matching
    /// no catalog mod is kept. A package matching any blocked id is dropped.
    /// </summary>
    public static IReadOnlyList<ModPackage> PackagesToKeep(
        IReadOnlyList<ModPackage> imported,
        IReadOnlyList<CatalogMod> catalog,
        ModConflictDecision decision)
    {
        ArgumentNullException.ThrowIfNull(imported);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(decision);

        var blocked = decision.Blocks
            .Select(block => block.IncomingId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return imported.Where(pkg =>
        {
            var matched = catalog.Where(mod => ModCatalogService.Matches(pkg, mod)).Select(mod => mod.Id);
            return matched.All(id => !blocked.Contains(id));
        }).ToList();
    }

    static Dictionary<string, CatalogMod> Index(IReadOnlyList<CatalogMod> catalog)
    {
        var byId = new Dictionary<string, CatalogMod>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in catalog)
        {
            var id = (mod.Id ?? "").Trim();
            if (id.Length == 0 || byId.ContainsKey(id))
                continue;
            byId[id] = mod;
        }

        return byId;
    }

    static IEnumerable<string> PartnerIds(Dictionary<string, CatalogMod> byId, string incomingId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (byId.TryGetValue(incomingId, out var self) && self.Conflicts is not null)
        {
            foreach (var edge in self.Conflicts)
            {
                var partner = (edge.Id ?? "").Trim();
                if (!Usable(byId, incomingId, partner) || !seen.Add(partner))
                    continue;
                yield return byId[partner].Id;
            }
        }

        foreach (var other in byId.Values)
        {
            if (string.Equals(other.Id, incomingId, StringComparison.OrdinalIgnoreCase) || other.Conflicts is null)
                continue;
            foreach (var edge in other.Conflicts)
            {
                var target = (edge.Id ?? "").Trim();
                if (!string.Equals(target, incomingId, StringComparison.OrdinalIgnoreCase))
                    continue;
                var partner = (other.Id ?? "").Trim();
                if (!Usable(byId, incomingId, partner) || !seen.Add(partner))
                    continue;
                yield return byId[partner].Id;
            }
        }
    }

    static bool Usable(Dictionary<string, CatalogMod> byId, string selfId, string partnerId) =>
        partnerId.Length > 0 &&
        !string.Equals(partnerId, selfId, StringComparison.OrdinalIgnoreCase) &&
        byId.ContainsKey(partnerId);

    static string? ResolveReason(
        Dictionary<string, CatalogMod> byId,
        string fromId,
        string toId,
        string? culture)
    {
        var direct = Localize(FirstEdge(byId, fromId, toId), culture);
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;
        var reverse = Localize(FirstEdge(byId, toId, fromId), culture);
        return string.IsNullOrWhiteSpace(reverse) ? null : reverse;
    }

    static CatalogConflict? FirstEdge(Dictionary<string, CatalogMod> byId, string fromId, string toId)
    {
        if (!byId.TryGetValue(fromId, out var mod) || mod.Conflicts is null)
            return null;
        foreach (var edge in mod.Conflicts)
        {
            var partner = (edge.Id ?? "").Trim();
            if (Usable(byId, fromId, partner) &&
                string.Equals(partner, toId, StringComparison.OrdinalIgnoreCase))
                return edge;
        }

        return null;
    }

    static string? Localize(CatalogConflict? edge, string? culture)
    {
        if (edge is null)
            return null;

        var key = LocalizationService.ResolveConfiguredLanguage(
            string.IsNullOrWhiteSpace(culture) ? "zh-CN" : culture);
        if (edge.Locales is not null)
        {
            foreach (var pair in edge.Locales)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(pair.Value?.Reason))
                    return pair.Value!.Reason!.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(edge.Reason) ? null : edge.Reason.Trim();
    }

    static string PairKey(string left, string right)
    {
        var (a, b) = OrderedIds(left, right);
        return a + "\n" + b;
    }

    static (string Left, string Right) OrderedIds(string left, string right) =>
        string.Compare(left, right, StringComparison.OrdinalIgnoreCase) <= 0
            ? (left, right)
            : (right, left);

    static string NameFor(IReadOnlyList<ModConflictBlock> group, string id)
    {
        foreach (var block in group)
        {
            if (string.Equals(block.IncomingId, id, StringComparison.OrdinalIgnoreCase))
                return block.IncomingName;
            if (string.Equals(block.PartnerId, id, StringComparison.OrdinalIgnoreCase))
                return block.PartnerName;
        }

        return id;
    }
}
