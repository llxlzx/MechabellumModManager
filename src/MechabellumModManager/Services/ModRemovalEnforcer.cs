using System.IO;
using System.Security.Cryptography;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed record RemovedMod(string Name, string? Reason);

/// <summary>
/// One deploy record and the physical folder its entries live in. A null root means the folder the
/// record itself names. Dual-folder records name the Steam link, which only reaches the mounted
/// store, so callers pass the store folder instead.
/// </summary>
public sealed record DeployTarget(string ManifestPath, string? GameRoot);

public sealed class ModRemovalResult
{
    public List<RemovedMod> Removed { get; } = [];

    /// <summary>Left in place this time (game running, file locked, store unreachable). Retried on the next catalog.</summary>
    public List<string> Deferred { get; } = [];

    public List<(string Name, string Error)> Failed { get; } = [];

    public bool HasWork => Removed.Count > 0 || Deferred.Count > 0 || Failed.Count > 0;
}

/// <summary>
/// Applies a catalog takedown list: deployed copies the manager put in the game folder, the
/// library package, and every profile entry. A package is only removed from the library once its
/// game-folder copies are gone, so a deferred package can still be found by id next time.
/// Game-folder files are only ever deleted when their bytes are known: the recorded deploy hash,
/// the package's own file hash, or a hash on the takedown list.
/// </summary>
public sealed class ModRemovalEnforcer
{
    enum EntryOutcome { Gone, Kept }

    readonly ModLibraryService _library;
    readonly JsonStore _store;
    readonly IProcessProbe _probe;

    public ModRemovalEnforcer(ModLibraryService library, JsonStore store, IProcessProbe probe)
    {
        _library = library;
        _store = store;
        _probe = probe;
    }

    /// <param name="fallbackRoots">Game folders searched for packages that have no deploy record.</param>
    public ModRemovalResult Run(
        IReadOnlyList<CatalogRemoval>? removed,
        IReadOnlyList<DeployTarget> targets,
        IReadOnlyList<string> fallbackRoots)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(fallbackRoots);
        var result = new ModRemovalResult();
        if (removed is not { Count: > 0 })
            return result;

        var hits = ModRemovalGate.Plan(_library.List(), removed);
        var hitIds = new HashSet<string>(hits.Select(h => h.Package.Id), StringComparer.OrdinalIgnoreCase);
        var listHashes = new HashSet<string>(
            removed.SelectMany(r => r.Sha256 ?? []).Select(h => (h ?? "").Trim()).Where(h => h.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        var manifests = targets
            .DistinctBy(t => t.ManifestPath, StringComparer.OrdinalIgnoreCase)
            .Where(t => File.Exists(t.ManifestPath))
            .Select(t =>
            {
                var manifest = _store.LoadOrDefault(t.ManifestPath, () => new DeployManifest());
                return (Target: t, Manifest: manifest, Hits: ModRemovalGate.ManifestHits(manifest, removed, hitIds));
            })
            .Where(m => m.Hits.Count > 0)
            .ToList();

        if (_probe.IsGameRunning())
        {
            result.Deferred.AddRange(hits.Select(NameOf));
            result.Deferred.AddRange(manifests
                .SelectMany(m => m.Hits)
                .Where(e => !hitIds.Contains(e.PackageId ?? ""))
                .Select(e => Path.GetFileName(e.RelativePath)));
            return result;
        }

        var keptPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recordedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (target, manifest, entries) in manifests)
        {
            // Null means "the folder the record names". An empty store path is not that: it means
            // the store is unknown, so its entries are kept rather than resolved through the link.
            var root = target.GameRoot ?? manifest.GamePath;
            var changed = false;
            foreach (var entry in entries)
            {
                var ownedByHit = hitIds.Contains(entry.PackageId ?? "");
                if (ownedByHit)
                    recordedPackages.Add(entry.PackageId!);

                if (RemoveRecorded(root, entry) == EntryOutcome.Gone)
                {
                    manifest.Files.Remove(entry);
                    changed = true;
                }
                else if (ownedByHit)
                {
                    keptPackages.Add(entry.PackageId!);
                }
                else
                {
                    result.Deferred.Add(Path.GetFileName(entry.RelativePath));
                }
            }

            if (changed)
                _store.Save(target.ManifestPath, manifest);
        }

        foreach (var hit in hits)
        {
            var name = NameOf(hit);
            if (keptPackages.Contains(hit.Package.Id) ||
                (!recordedPackages.Contains(hit.Package.Id) &&
                 RemoveUnrecorded(hit.Package, listHashes, fallbackRoots) == EntryOutcome.Kept))
            {
                result.Deferred.Add(name);
                continue;
            }

            try
            {
                _library.Delete(hit.Package.Id);
                result.Removed.Add(new RemovedMod(name, hit.Removal.ResolveReason()));
            }
            catch (Exception ex)
            {
                result.Failed.Add((name, ex.Message));
            }
        }

        return result;
    }

    /// <summary>
    /// Gone when the entry no longer needs tracking: the file was deleted, is absent, holds other
    /// bytes, or the path is not inside the recorded folder. Kept when the folder itself cannot be
    /// reached (an absent file there proves nothing) or a matching file could not be deleted.
    /// </summary>
    static EntryOutcome RemoveRecorded(string? gameRoot, ManifestFileEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RelativePath))
            return EntryOutcome.Gone;
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            return EntryOutcome.Kept;

        var full = ResolveUnder(gameRoot, entry.RelativePath);
        if (full is null || !File.Exists(full))
            return EntryOutcome.Gone;

        return DeleteIfHashIn(full, [(entry.Sha256 ?? "").Trim()]);
    }

    /// <summary>
    /// A package with no deploy record may still have a copy in the game folder (imported from it,
    /// or deployed before records existed). Only bytes we can name are deleted; a same-named file
    /// with other bytes is the player's own and stays. Every root passed in must be reachable:
    /// dropping the package while a store is unplugged would leave its copy findable only by hash.
    /// </summary>
    static EntryOutcome RemoveUnrecorded(ModPackage pkg, IReadOnlySet<string> listHashes, IReadOnlyList<string> roots)
    {
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return EntryOutcome.Kept;
            foreach (var file in pkg.Files)
            {
                string relative;
                try
                {
                    relative = DeployPlanner.MapRelativeGamePath(pkg.Type, file.RelativePathInPackage);
                }
                catch
                {
                    continue;
                }

                var full = ResolveUnder(root, relative);
                if (full is null || !File.Exists(full))
                    continue;

                var known = new HashSet<string>(listHashes, StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(file.Sha256))
                    known.Add(file.Sha256.Trim());
                if (DeleteIfHashIn(full, known) == EntryOutcome.Kept)
                    return EntryOutcome.Kept;
            }
        }

        return EntryOutcome.Gone;
    }

    static EntryOutcome DeleteIfHashIn(string full, IEnumerable<string> hashes)
    {
        try
        {
            var onDisk = HashFile(full);
            if (!hashes.Any(h => string.Equals(h, onDisk, StringComparison.OrdinalIgnoreCase)))
                return EntryOutcome.Gone;
            File.Delete(full);
            return EntryOutcome.Gone;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EntryOutcome.Kept;
        }
    }

    static string? ResolveUnder(string root, string relative)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    static string NameOf(ModRemovalHit hit)
    {
        if (!string.IsNullOrWhiteSpace(hit.Package.DisplayName))
            return hit.Package.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(hit.Removal.Name))
            return hit.Removal.Name.Trim();
        return hit.Package.Id;
    }
}
