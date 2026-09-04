using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DeployServiceBranchManifestTests
{
    [Fact]
    public void Apply_branch_paths_write_per_branch_manifests_and_leave_other_store_files()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-deploy-branch-" + Guid.NewGuid().ToString("N"));
        var officialRoot = Path.Combine(Path.GetTempPath(), "mmm-game-off-" + Guid.NewGuid().ToString("N"));
        var betaRoot = Path.Combine(Path.GetTempPath(), "mmm-game-beta-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateReadyGame(officialRoot);
            CreateReadyGame(betaRoot);

            var paths = new PathsService(dataRoot);
            paths.EnsureCreated();
            var store = new JsonStore();

            var officialPackage = CreateMelonMod(paths, "off", "Off.dll");
            var betaPackage = CreateMelonMod(paths, "beta", "Beta.dll");
            var packages = new Dictionary<string, ModPackage>
            {
                ["off"] = officialPackage,
                ["beta"] = betaPackage
            };

            var svc = new DeployService(
                paths,
                store,
                new DeployPlanner(),
                new GameDetector(),
                new ProcessProbe());

            var officialProfile = new Profile { Id = "p-off", Name = "official", EnabledPackageIds = { "off" } };
            var r1 = svc.Apply(
                officialProfile,
                packages,
                officialRoot,
                allowOverwriteUnmanaged: false,
                manifestPath: paths.GetDeployManifestPath(GameBranch.Official, enabled: true),
                manifestPrevPath: paths.GetDeployManifestPrevPath(GameBranch.Official, enabled: true));

            r1.Success.Should().BeTrue(r1.Message);
            File.Exists(paths.GetDeployManifestPath(GameBranch.Official, enabled: true)).Should().BeTrue();
            File.Exists(paths.DeployManifestPath).Should().BeFalse();
            File.Exists(Path.Combine(officialRoot, "Mods", "Off.dll")).Should().BeTrue();

            var betaProfile = new Profile { Id = "p-beta", Name = "beta", EnabledPackageIds = { "beta" } };
            var r2 = svc.Apply(
                betaProfile,
                packages,
                betaRoot,
                allowOverwriteUnmanaged: false,
                manifestPath: paths.GetDeployManifestPath(GameBranch.Beta, enabled: true),
                manifestPrevPath: paths.GetDeployManifestPrevPath(GameBranch.Beta, enabled: true));

            r2.Success.Should().BeTrue(r2.Message);
            File.Exists(paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeTrue();
            File.Exists(paths.DeployManifestPath).Should().BeFalse();

            File.Exists(Path.Combine(officialRoot, "Mods", "Off.dll")).Should().BeTrue();
            File.Exists(Path.Combine(officialRoot, "Mods", "Beta.dll")).Should().BeFalse();
            File.Exists(Path.Combine(betaRoot, "Mods", "Beta.dll")).Should().BeTrue();
            File.Exists(Path.Combine(betaRoot, "Mods", "Off.dll")).Should().BeFalse();

            var officialManifest = store.LoadOrDefault(
                paths.GetDeployManifestPath(GameBranch.Official, enabled: true),
                () => new DeployManifest());
            officialManifest.Files.Should().ContainSingle(f => f.RelativePath == "Mods/Off.dll");

            var betaManifest = store.LoadOrDefault(
                paths.GetDeployManifestPath(GameBranch.Beta, enabled: true),
                () => new DeployManifest());
            betaManifest.Files.Should().ContainSingle(f => f.RelativePath == "Mods/Beta.dll");
        }
        finally
        {
            if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
            if (Directory.Exists(officialRoot)) Directory.Delete(officialRoot, true);
            if (Directory.Exists(betaRoot)) Directory.Delete(betaRoot, true);
        }
    }

    static ModPackage CreateMelonMod(PathsService paths, string id, string fileName)
    {
        var pkgDir = Path.Combine(paths.LibraryRoot, "mods", id);
        Directory.CreateDirectory(pkgDir);
        File.WriteAllBytes(Path.Combine(pkgDir, fileName), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
        return new ModPackage
        {
            Id = id,
            DisplayName = id,
            Type = ModPackageType.MelonMod,
            PackageDirectory = pkgDir,
            Files =
            {
                new DeployableFile { RelativePathInPackage = fileName, Sha256 = id + "-hash" }
            }
        };
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
