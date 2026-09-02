using System.IO;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class DeployResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public DeployPlan Plan { get; init; } = new();
}

public sealed class DeployService
{
    private readonly PathsService _paths;
    private readonly JsonStore _store;
    private readonly DeployPlanner _planner;
    private readonly GameDetector _detector;
    private readonly ProcessProbe _processProbe;

    public DeployService(
        PathsService paths,
        JsonStore store,
        DeployPlanner planner,
        GameDetector detector,
        ProcessProbe processProbe)
    {
        _paths = paths;
        _store = store;
        _planner = planner;
        _detector = detector;
        _processProbe = processProbe;
    }

    public DeployResult Apply(
        Profile profile,
        IReadOnlyDictionary<string, ModPackage> packages,
        string gamePath,
        bool allowOverwriteUnmanaged)
    {
        var emptyPlan = new DeployPlan();

        if (_processProbe.IsGameRunning())
            return Fail("游戏正在运行，请先关闭 Mechabellum 后再部署。", emptyPlan);

        var status = _detector.Detect(gamePath);
        if (status.Kind != GameStatusKind.Ready)
            return Fail(status.Message, emptyPlan);

        var existing = _store.LoadOrDefault(_paths.DeployManifestPath, () => new DeployManifest());
        var plan = _planner.Build(gamePath, profile, packages, existing, allowOverwriteUnmanaged);

        if (plan.IntraProfileNameCollisions.Count > 0)
            return Fail("方案内存在同名文件冲突，已中止部署。", plan);

        if (plan.ConflictsUnmanaged.Count > 0 && !allowOverwriteUnmanaged)
            return Fail("存在非托管同名文件，默认拒绝覆盖。", plan);

        var prev = CloneManifest(existing);
        _store.Save(_paths.DeployManifestPrevPath, prev);

        EnsureGameDirs(gamePath);

        var writtenThisAttempt = new List<string>();
        try
        {
            foreach (var del in plan.Deletes)
            {
                if (File.Exists(del))
                    File.Delete(del);
            }

            foreach (var copy in plan.Copies)
            {
                var destDir = Path.GetDirectoryName(copy.DestAbsolute);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                File.Copy(copy.SourceAbsolute, copy.DestAbsolute, overwrite: true);
                writtenThisAttempt.Add(copy.DestAbsolute);
            }
        }
        catch (Exception ex)
        {
            Rollback(prev, writtenThisAttempt, packages, gamePath);
            return Fail($"部署失败并已回滚：{ex.Message}", plan);
        }

        var newManifest = new DeployManifest
        {
            GamePath = gamePath,
            ProfileId = profile.Id,
            Files = plan.Copies.Select(c => new ManifestFileEntry
            {
                RelativePath = c.RelativeGamePath.Replace('\\', '/'),
                PackageId = c.PackageId,
                Sha256 = c.Sha256
            }).ToList()
        };
        _store.Save(_paths.DeployManifestPath, newManifest);

        return new DeployResult
        {
            Success = true,
            Message = "部署成功。",
            Plan = plan
        };
    }

    private void Rollback(
        DeployManifest prev,
        List<string> writtenThisAttempt,
        IReadOnlyDictionary<string, ModPackage> packages,
        string gamePath)
    {
        try
        {
            // Stale GamePath: treat as empty-prev — do not resync old-root files into the new root
            // or restore the old-path manifest as active (forensics prev file already saved).
            var prevUsable = prev.Files.Count > 0 && PathsEqual(prev.GamePath, gamePath);

            if (prevUsable)
            {
                var prevKeys = new HashSet<string>(
                    prev.Files.Select(f => NormalizeRelative(f.RelativePath)),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var written in writtenThisAttempt)
                {
                    var rel = NormalizeRelative(Path.GetRelativePath(gamePath, written));
                    if (!prevKeys.Contains(rel) && File.Exists(written))
                        File.Delete(written);
                }

                foreach (var entry in prev.Files)
                {
                    if (!packages.TryGetValue(entry.PackageId, out var package))
                        continue;

                    var source = ResolveLibrarySource(package, entry);
                    if (source is null || !File.Exists(source))
                        continue;

                    var dest = Path.GetFullPath(Path.Combine(
                        gamePath,
                        entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Copy(source, dest, overwrite: true);
                }

                _store.Save(_paths.DeployManifestPath, prev);
            }
            else
            {
                foreach (var written in writtenThisAttempt)
                {
                    if (File.Exists(written))
                        File.Delete(written);
                }

                _store.Save(_paths.DeployManifestPath, new DeployManifest());
            }
        }
        catch
        {
            // Spec §7.5.4: keep prev + logs; do not pretend success
        }
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

    private static string? ResolveLibrarySource(ModPackage package, ManifestFileEntry entry)
    {
        var byHash = package.Files.FirstOrDefault(f =>
            string.Equals(f.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(f.Sha256));
        if (byHash != null)
            return Path.Combine(package.PackageDirectory, byHash.RelativePathInPackage);

        var fileName = Path.GetFileName(entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var byName = package.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.RelativePathInPackage), fileName, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return Path.Combine(package.PackageDirectory, byName.RelativePathInPackage);

        if (package.Type == ModPackageType.MelonUserData)
        {
            const string prefix = "UserData/";
            var rel = NormalizeRelative(entry.RelativePath);
            if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var inPkg = rel[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(package.PackageDirectory, inPkg);
            }
        }

        return Path.Combine(package.PackageDirectory, fileName);
    }

    private static DeployManifest CloneManifest(DeployManifest source) =>
        new()
        {
            GamePath = source.GamePath,
            ProfileId = source.ProfileId,
            Files = source.Files.Select(f => new ManifestFileEntry
            {
                RelativePath = f.RelativePath,
                PackageId = f.PackageId,
                Sha256 = f.Sha256
            }).ToList()
        };

    private static void EnsureGameDirs(string gamePath)
    {
        foreach (var sub in new[] { "Mods", "Plugins", "UserLibs", "UserData" })
            Directory.CreateDirectory(Path.Combine(gamePath, sub));
    }

    private static string NormalizeRelative(string relativePath) =>
        relativePath.Replace('\\', '/').Trim('/');

    private static DeployResult Fail(string message, DeployPlan plan) =>
        new() { Success = false, Message = message, Plan = plan };
}
