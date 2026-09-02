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

    static void CreateReadyGame(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        File.WriteAllText(Path.Combine(root, "version.dll"), "");
    }
}
