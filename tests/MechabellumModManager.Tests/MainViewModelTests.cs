using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Toggle_enable_marks_dirty_and_Apply_clears_when_deploy_succeeds()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-vm-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-vm-game-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateReadyGame(gameRoot);

            var paths = new PathsService(dataRoot);
            paths.EnsureCreated();
            var store = new JsonStore();
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var inspector = new AssemblyInspector();
            var library = new ModLibraryService(paths, inspector, store, profiles);
            var detector = new GameDetector();
            var deploy = new DeployService(paths, store, new DeployPlanner(), detector, new ProcessProbe());
            var launcher = new GameLauncher(new ShellProcessStarter(), () => false);
            var risk = new RiskGate();

            // Seed one library package without going through import (stub dll).
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", "cam-aaaaaaaa");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllBytes(Path.Combine(pkgDir, "Cam.dll"), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                """
                {
                  "id": "cam-aaaaaaaa",
                  "displayName": "Cam",
                  "type": "melon_mod",
                  "highRisk": false,
                  "files": [ { "relativePathInPackage": "Cam.dll", "sha256": "aabbccdd" } ]
                }
                """);
            store.Save(Path.Combine(paths.LibraryRoot, "index.json"), new { packageIds = new[] { "cam-aaaaaaaa" } });

            // Point config at Ready game root.
            store.Save(paths.ConfigPath, new AppConfig
            {
                GamePath = gameRoot,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });

            var vm = new MainViewModel(
                paths,
                store,
                detector,
                library,
                profiles,
                deploy,
                launcher,
                risk,
                confirmHighRisk: _ => true);

            vm.IsDirty.Should().BeFalse();
            vm.Mods.Should().ContainSingle(m => m.Package.Id == "cam-aaaaaaaa");

            vm.Mods[0].IsEnabled = true;
            vm.IsDirty.Should().BeTrue();

            vm.ApplyProfileCommand.Execute(null);
            vm.IsDirty.Should().BeFalse();
            File.Exists(Path.Combine(gameRoot, "Mods", "Cam.dll")).Should().BeTrue();
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
