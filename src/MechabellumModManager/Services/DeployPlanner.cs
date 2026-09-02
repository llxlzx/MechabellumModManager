using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class DeployPlanner
{
    public DeployPlan Build(
        string gamePath,
        Profile profile,
        IReadOnlyDictionary<string, ModPackage> packagesById,
        DeployManifest? existingManifest,
        bool allowOverwriteUnmanaged)
    {
        var manifestInvalid = false;
        IReadOnlyList<ManifestFileEntry> manifestFiles = Array.Empty<ManifestFileEntry>();

        if (existingManifest != null)
        {
            if (!PathsEqual(existingManifest.GamePath, gamePath))
            {
                manifestInvalid = true;
            }
            else
            {
                manifestFiles = existingManifest.Files;
            }
        }

        var desired = new Dictionary<string, DesiredFile>(StringComparer.OrdinalIgnoreCase);
        var collisions = new List<string>();

        foreach (var packageId in profile.EnabledPackageIds)
        {
            if (!packagesById.TryGetValue(packageId, out var package))
                continue;

            foreach (var file in package.Files)
            {
                var relativeGamePath = MapRelativeGamePath(package.Type, file.RelativePathInPackage);
                var key = NormalizeRelative(relativeGamePath);

                if (desired.ContainsKey(key))
                {
                    if (!collisions.Contains(key, StringComparer.OrdinalIgnoreCase))
                        collisions.Add(key);
                    continue;
                }

                desired[key] = new DesiredFile(
                    package.Id,
                    Path.Combine(package.PackageDirectory, file.RelativePathInPackage),
                    relativeGamePath,
                    file.Sha256);
            }
        }

        if (collisions.Count > 0)
        {
            return new DeployPlan
            {
                ManifestInvalidDueToGamePath = manifestInvalid,
                IntraProfileNameCollisions = collisions
            };
        }

        var manifestKeys = new HashSet<string>(
            manifestFiles.Select(f => NormalizeRelative(f.RelativePath)),
            StringComparer.OrdinalIgnoreCase);

        var deletes = new List<string>();
        foreach (var entry in manifestFiles)
        {
            var key = NormalizeRelative(entry.RelativePath);
            if (!desired.ContainsKey(key))
            {
                deletes.Add(ToAbsoluteUnderGame(gamePath, entry.RelativePath));
            }
        }

        var copies = new List<PlannedCopy>();
        var conflicts = new List<string>();

        foreach (var item in desired.Values)
        {
            var destAbsolute = ToAbsoluteUnderGame(gamePath, item.RelativeGamePath);
            if (File.Exists(destAbsolute)
                && !manifestKeys.Contains(NormalizeRelative(item.RelativeGamePath))
                && !allowOverwriteUnmanaged)
            {
                conflicts.Add(destAbsolute);
            }

            copies.Add(new PlannedCopy
            {
                SourceAbsolute = Path.GetFullPath(item.SourceAbsolute),
                DestAbsolute = destAbsolute,
                RelativeGamePath = item.RelativeGamePath,
                PackageId = item.PackageId,
                Sha256 = item.Sha256
            });
        }

        return new DeployPlan
        {
            Deletes = deletes,
            Copies = copies,
            ConflictsUnmanaged = conflicts,
            ManifestInvalidDueToGamePath = manifestInvalid
        };
    }

    private static string MapRelativeGamePath(ModPackageType type, string relativePathInPackage)
    {
        return type switch
        {
            ModPackageType.MelonMod =>
                Path.Combine("Mods", Path.GetFileName(relativePathInPackage)),
            ModPackageType.MelonPlugin =>
                Path.Combine("Plugins", Path.GetFileName(relativePathInPackage)),
            ModPackageType.MelonUserLibs =>
                Path.Combine("UserLibs", Path.GetFileName(relativePathInPackage)),
            ModPackageType.MelonUserData => MapUserDataRelative(relativePathInPackage),
            _ => throw new InvalidOperationException($"Unsupported package type: {type}")
        };
    }

    private static string MapUserDataRelative(string relativePathInPackage)
    {
        var normalized = NormalizeRelative(relativePathInPackage);
        if (string.Equals(normalized, "Loader.cfg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Deploying UserData/Loader.cfg is not allowed.");

        return Path.Combine("UserData", relativePathInPackage);
    }

    private static string NormalizeRelative(string relativePath) =>
        relativePath.Replace('\\', '/').Trim('/');

    private static string ToAbsoluteUnderGame(string gamePath, string relativePath)
    {
        var combined = Path.Combine(
            gamePath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetFullPath(combined);
    }

    private static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        try
        {
            return string.Equals(
                Path.GetFullPath(a.Trim()),
                Path.GetFullPath(b.Trim()),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record DesiredFile(
        string PackageId,
        string SourceAbsolute,
        string RelativeGamePath,
        string Sha256);
}
