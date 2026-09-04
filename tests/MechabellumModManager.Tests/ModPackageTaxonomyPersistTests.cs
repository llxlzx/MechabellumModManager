using System.Text.Json;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class ModPackageTaxonomyPersistTests
{
    [Fact]
    public void WriteAndLoad_preserves_categoryOverride_and_extraTags()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-tax-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var store = new JsonStore();
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var lib = new ModLibraryService(paths, new AssemblyInspector(), store, profiles);

            var pkgId = "tax-test-aaaaaaaa";
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", pkgId);
            Directory.CreateDirectory(pkgDir);
            File.WriteAllBytes(Path.Combine(pkgDir, "Tax.dll"), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                """
                {
                  "id": "tax-test-aaaaaaaa",
                  "displayName": "Tax Test",
                  "type": "melon_mod",
                  "categoryOverride": "Camera",
                  "extraTags": ["mine"],
                  "files": [ { "relativePathInPackage": "Tax.dll", "sha256": "aabb" } ]
                }
                """);
            store.Save(Path.Combine(paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });

            var loaded = lib.List().Should().ContainSingle().Subject;
            loaded.CategoryOverride.Should().Be("Camera");
            loaded.ExtraTags.Should().Equal("mine");

            loaded.CategoryOverride = "QoL";
            loaded.ExtraTags = new List<string> { "mine", "extra" };
            lib.SavePackageMeta(loaded);

            var reloaded = lib.List().Should().ContainSingle().Subject;
            reloaded.CategoryOverride.Should().Be("QoL");
            reloaded.ExtraTags.Should().Equal("mine", "extra");

            var json = File.ReadAllText(Path.Combine(pkgDir, "package.json"));
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("categoryOverride").GetString().Should().Be("QoL");
            doc.RootElement.GetProperty("extraTags").EnumerateArray().Select(e => e.GetString()).Should().Equal("mine", "extra");
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
    }

    [Fact]
    public void ApplyCatalogEnrichment_does_not_clear_local_taxonomy()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-tax-vm-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(Path.GetTempPath(), "mmm-tax-game-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "Mechabellum.exe"), "");
            File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "");
            Directory.CreateDirectory(Path.Combine(game, "MelonLoader"));
            File.WriteAllText(Path.Combine(game, "version.dll"), "");

            var paths = new PathsService(data);
            paths.EnsureCreated();
            var store = new JsonStore();
            store.Save(paths.ConfigPath, new AppConfig
            {
                GamePath = game,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var library = new ModLibraryService(paths, new AssemblyInspector(), store, profiles);
            var detector = new GameDetector();
            var deploy = new DeployService(paths, store, new DeployPlanner(), detector, new ProcessProbe());
            var launcher = new GameLauncher(new NoopStarter(), () => false);
            var owner = new MainViewModel(
                paths, store, detector, library, profiles, deploy, launcher, new RiskGate(),
                confirmHighRisk: _ => true);

            var pkg = new ModPackage
            {
                Id = "x",
                DisplayName = "X",
                Type = ModPackageType.MelonMod,
                CategoryOverride = "Camera",
                ExtraTags = new List<string> { "mine" }
            };
            var item = new ModItemViewModel(owner, pkg, isEnabled: false);
            item.ApplyCatalogEnrichment(new CatalogMod
            {
                Id = "x",
                Name = "X",
                Category = "QoL",
                Tags = new List<string> { "hud" }
            });

            pkg.CategoryOverride.Should().Be("Camera");
            pkg.ExtraTags.Should().Equal("mine");
            pkg.CatalogCategory.Should().Be("QoL");
            pkg.CatalogTags.Should().Equal("hud");
            item.EffectiveCategory.Should().Be(ModCategory.Camera);
            item.EffectiveTags.Should().Equal("hud", "mine");
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (Directory.Exists(game)) Directory.Delete(game, true);
        }
    }

    sealed class NoopStarter : IProcessStarter
    {
        public void StartShell(string uriOrPath) { }
    }
}
