using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DeployServiceTests
{
    [Fact]
    public void Apply_copies_and_second_empty_profile_removes_managed_only()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-deploy-svc-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-game-deploy-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateReadyGame(gameRoot);
            Directory.CreateDirectory(Path.Combine(gameRoot, "Mods"));

            var paths = new PathsService(dataRoot);
            paths.EnsureCreated();
            var store = new JsonStore();
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", "x");
            Directory.CreateDirectory(pkgDir);
            var dllPath = Path.Combine(pkgDir, "X.dll");
            File.WriteAllBytes(dllPath, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

            var package = new ModPackage
            {
                Id = "x",
                DisplayName = "X",
                Type = ModPackageType.MelonMod,
                PackageDirectory = pkgDir,
                Files =
                {
                    new DeployableFile
                    {
                        RelativePathInPackage = "X.dll",
                        Sha256 = "deadbeef"
                    }
                }
            };
            var packages = new Dictionary<string, ModPackage> { ["x"] = package };

            var svc = new DeployService(
                paths,
                store,
                new DeployPlanner(),
                new GameDetector(),
                new ProcessProbe());

            var withPkg = new Profile { Id = "p1", Name = "with", EnabledPackageIds = { "x" } };
            var r1 = svc.Apply(withPkg, packages, gameRoot, allowOverwriteUnmanaged: false);
            r1.Success.Should().BeTrue(r1.Message);
            File.Exists(Path.Combine(gameRoot, "Mods", "X.dll")).Should().BeTrue();

            File.WriteAllText(Path.Combine(gameRoot, "Mods", "Hand.dll"), "hand");

            var empty = new Profile { Id = "p2", Name = "empty" };
            var r2 = svc.Apply(empty, packages, gameRoot, allowOverwriteUnmanaged: false);
            r2.Success.Should().BeTrue(r2.Message);

            File.Exists(Path.Combine(gameRoot, "Mods", "X.dll")).Should().BeFalse();
            File.Exists(Path.Combine(gameRoot, "Mods", "Hand.dll")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
            if (Directory.Exists(gameRoot)) Directory.Delete(gameRoot, true);
        }
    }

    [Fact]
    public void Rollback_stale_manifest_gamepath_mismatch_does_not_resync_old_files()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-deploy-stale-" + Guid.NewGuid().ToString("N"));
        var oldGameRoot = Path.Combine(Path.GetTempPath(), "mmm-game-old-" + Guid.NewGuid().ToString("N"));
        var newGameRoot = Path.Combine(Path.GetTempPath(), "mmm-game-new-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateReadyGame(newGameRoot);

            var paths = new PathsService(dataRoot);
            paths.EnsureCreated();
            var store = new JsonStore();

            var oldPkgDir = Path.Combine(paths.LibraryRoot, "mods", "old");
            Directory.CreateDirectory(oldPkgDir);
            File.WriteAllBytes(Path.Combine(oldPkgDir, "Old.dll"), new byte[] { 0x4D, 0x5A, 0x01 });

            var newPkgDir = Path.Combine(paths.LibraryRoot, "mods", "new");
            Directory.CreateDirectory(newPkgDir);
            File.WriteAllBytes(Path.Combine(newPkgDir, "A.dll"), new byte[] { 0x4D, 0x5A, 0x02 });
            File.WriteAllBytes(Path.Combine(newPkgDir, "B.dll"), new byte[] { 0x4D, 0x5A, 0x03 });

            var oldPackage = new ModPackage
            {
                Id = "old",
                DisplayName = "Old",
                Type = ModPackageType.MelonMod,
                PackageDirectory = oldPkgDir,
                Files =
                {
                    new DeployableFile { RelativePathInPackage = "Old.dll", Sha256 = "oldhash" }
                }
            };
            var newPackage = new ModPackage
            {
                Id = "new",
                DisplayName = "New",
                Type = ModPackageType.MelonMod,
                PackageDirectory = newPkgDir,
                Files =
                {
                    new DeployableFile { RelativePathInPackage = "A.dll", Sha256 = "ahash" },
                    new DeployableFile { RelativePathInPackage = "B.dll", Sha256 = "bhash" }
                }
            };
            var packages = new Dictionary<string, ModPackage>
            {
                ["old"] = oldPackage,
                ["new"] = newPackage
            };

            store.Save(paths.DeployManifestPath, new DeployManifest
            {
                GamePath = oldGameRoot,
                ProfileId = "stale",
                Files =
                {
                    new ManifestFileEntry
                    {
                        RelativePath = "Mods/Old.dll",
                        PackageId = "old",
                        Sha256 = "oldhash"
                    }
                }
            });

            // Force mid-copy failure: A.dll copies, then B.dll hits a directory named B.dll
            Directory.CreateDirectory(Path.Combine(newGameRoot, "Mods", "B.dll"));

            var svc = new DeployService(
                paths,
                store,
                new DeployPlanner(),
                new GameDetector(),
                new ProcessProbe());

            var profile = new Profile { Id = "p-new", Name = "new", EnabledPackageIds = { "new" } };
            var result = svc.Apply(profile, packages, newGameRoot, allowOverwriteUnmanaged: false);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("回滚");

            File.Exists(Path.Combine(newGameRoot, "Mods", "A.dll")).Should().BeFalse(
                "writtenThisAttempt extras must be deleted");
            File.Exists(Path.Combine(newGameRoot, "Mods", "Old.dll")).Should().BeFalse(
                "must not resync old-path manifest files into the new game root");

            var active = store.LoadOrDefault(paths.DeployManifestPath, () => new DeployManifest());
            active.Files.Should().BeEmpty("must not restore old-path manifest as active for new root");
            string.IsNullOrEmpty(active.GamePath).Should().BeTrue();

            var prev = store.LoadOrDefault(paths.DeployManifestPrevPath, () => new DeployManifest());
            prev.GamePath.Should().Be(oldGameRoot);
            prev.Files.Should().ContainSingle(f => f.RelativePath == "Mods/Old.dll");
        }
        finally
        {
            if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
            if (Directory.Exists(oldGameRoot)) Directory.Delete(oldGameRoot, true);
            if (Directory.Exists(newGameRoot)) Directory.Delete(newGameRoot, true);
        }
    }

    [Fact]
    public void Apply_not_ready_game_fails_without_writes()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-deploy-gate-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-game-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(gameRoot);
            File.WriteAllText(Path.Combine(gameRoot, "Mechabellum.exe"), "");
            // Missing GameAssembly.dll / MelonLoader → not Ready

            var paths = new PathsService(dataRoot);
            paths.EnsureCreated();
            var svc = new DeployService(
                paths,
                new JsonStore(),
                new DeployPlanner(),
                new GameDetector(),
                new ProcessProbe());

            var profile = new Profile { Id = "p", Name = "p", EnabledPackageIds = { "x" } };
            var result = svc.Apply(profile, new Dictionary<string, ModPackage>(), gameRoot, false);

            result.Success.Should().BeFalse();
            Directory.Exists(Path.Combine(gameRoot, "Mods")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
            if (Directory.Exists(gameRoot)) Directory.Delete(gameRoot, true);
        }
    }

    static void CreateReadyGame(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        File.WriteAllText(Path.Combine(root, "version.dll"), "");
    }
}
