using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void CanApplyProfile_false_when_only_library_selection_and_not_dirty()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.IsDirty.Should().BeFalse();
        vm.LibrarySelectionCount = 3;

        vm.CanApplyProfile.Should().BeFalse();
        vm.ApplyProfileCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanApplyProfile_true_when_dirty_and_ready()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.Mods[0].IsEnabled = true;
        vm.IsDirty.Should().BeTrue();
        vm.CanApplyProfile.Should().BeTrue();
    }

    [Fact]
    public void Toggle_enable_marks_dirty_and_Apply_clears_when_deploy_succeeds()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.IsDirty.Should().BeFalse();
        vm.Mods.Should().ContainSingle(m => m.Package.Id == "cam-aaaaaaaa");

        vm.Mods[0].IsEnabled = true;
        vm.IsDirty.Should().BeTrue();

        vm.ApplyProfileCommand.Execute(null);
        vm.IsDirty.Should().BeFalse();
        File.Exists(Path.Combine(fx.GameRoot, "Mods", "Cam.dll")).Should().BeTrue();
    }

    [Fact]
    public void ApplyAndLaunch_does_not_launch_when_Apply_fails()
    {
        using var fx = Fixture.CreateReady();
        // Unmanaged collision → Apply fails.
        Directory.CreateDirectory(Path.Combine(fx.GameRoot, "Mods"));
        File.WriteAllText(Path.Combine(fx.GameRoot, "Mods", "Cam.dll"), "unmanaged");

        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirmHighRisk: _ => true, starter: starter);
        vm.Mods[0].IsEnabled = true;

        vm.ApplyAndLaunchCommand.Execute(null);

        starter.Starts.Should().BeEmpty();
        vm.LogText.Should().Contain("非托管");
    }

    [Fact]
    public void ApplyAndLaunch_does_not_launch_when_game_not_Ready()
    {
        using var fx = Fixture.CreateReady();
        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirmHighRisk: _ => true, starter: starter);
        vm.Mods[0].IsEnabled = true;
        vm.GamePath = Path.Combine(Path.GetTempPath(), "mmm-missing-" + Guid.NewGuid().ToString("N"));

        vm.ApplyAndLaunchCommand.Execute(null);

        starter.Starts.Should().BeEmpty();
    }

    [Fact]
    public void HighRisk_enable_cancelled_when_confirm_returns_false()
    {
        using var fx = Fixture.CreateReady(highRisk: true);
        var vm = fx.CreateVm(confirmHighRisk: _ => false);

        vm.Mods[0].IsEnabled = true;

        vm.Mods[0].IsEnabled.Should().BeFalse();
        vm.LogText.Should().Contain("已取消启用高风险");
        vm.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Default_confirmHighRisk_denies_high_risk_enable()
    {
        using var fx = Fixture.CreateReady(highRisk: true);
        var vm = fx.CreateVm(confirmHighRisk: null);

        vm.Mods[0].IsEnabled = true;

        vm.Mods[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_after_apply_marks_dirty_and_enables_Apply()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.Mods[0].IsEnabled = true;
        vm.ApplyProfileCommand.Execute(null);
        vm.IsDirty.Should().BeFalse();
        vm.CanApplyProfile.Should().BeFalse();

        vm.Mods[0].IsEnabled = false;
        vm.IsDirty.Should().BeTrue();
        vm.CanApplyProfile.Should().BeTrue();
        vm.ApplyProfileCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ImportDll_auto_enables_and_marks_dirty()
    {
        using var fx = Fixture.CreateReady();
        var stub = Path.Combine(fx.DataRoot, "fresh.dll");
        File.WriteAllBytes(stub, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            pickPackageType: () => ModPackageType.MelonMod,
            openDll: () => stub);

        vm.ImportDllCommand.Execute(null);

        var imported = vm.Mods.Should().ContainSingle(m =>
            m.Package.Files.Any(f =>
                f.RelativePathInPackage.EndsWith("fresh.dll", StringComparison.OrdinalIgnoreCase))).Subject;
        imported.IsEnabled.Should().BeTrue();
        vm.IsDirty.Should().BeTrue();
        vm.CanApplyProfile.Should().BeTrue();
    }

    [Fact]
    public void ImportDll_NeedsType_pick_completes_via_CommitStaging()
    {
        using var fx = Fixture.CreateReady();
        var stub = Path.Combine(fx.DataRoot, "mystery.dll");
        File.WriteAllBytes(stub, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            pickPackageType: () => ModPackageType.MelonUserLibs,
            openDll: () => stub);

        vm.ImportDllCommand.Execute(null);

        fx.Library.List().Should().Contain(p => p.Type == ModPackageType.MelonUserLibs);
        vm.Mods.Should().Contain(m => m.Package.Type == ModPackageType.MelonUserLibs);
    }

    [Fact]
    public void ImportDll_NeedsType_cancel_discards_staging()
    {
        using var fx = Fixture.CreateReady();
        var stub = Path.Combine(fx.DataRoot, "mystery.dll");
        File.WriteAllBytes(stub, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

        var before = fx.Library.List().Count;
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            pickPackageType: () => null,
            openDll: () => stub);

        vm.ImportDllCommand.Execute(null);

        fx.Library.List().Should().HaveCount(before);
        vm.LogText.Should().Contain("\u5df2\u53d6\u6d88");
    }

    [Fact]
    public void SetCatalogSelection_updates_CatalogSelectionCount()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);
        var mod = new CatalogMod { Id = "test-mod", Name = "Test", File = "mods/test/Test.dll" };
        var item = new CatalogModItemViewModel(mod, isInLibrary: false);

        vm.SetCatalogSelection(Array.Empty<CatalogModItemViewModel>());
        vm.CatalogSelectionCount.Should().Be(0);

        vm.SetCatalogSelection(new[] { item });
        vm.CatalogSelectionCount.Should().Be(1);
    }

    [Fact]
    public void CanAddCatalogMod_false_when_no_addable_selection()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);
        var inLib = new CatalogModItemViewModel(
            new CatalogMod { Id = "cam", Name = "Cam", File = "mods/cam/Cam.dll" },
            isInLibrary: true);

        vm.SetCatalogSelection(Array.Empty<CatalogModItemViewModel>());
        vm.AddCatalogModToLibraryCommand.CanExecute(null).Should().BeFalse();

        vm.SetCatalogSelection(new[] { inLib });
        vm.AddCatalogModToLibraryCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanAddCatalogMod_true_when_any_selected_not_in_library()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);
        var inLib = new CatalogModItemViewModel(
            new CatalogMod { Id = "cam", Name = "Cam", File = "mods/cam/Cam.dll" },
            isInLibrary: true);
        var fresh = new CatalogModItemViewModel(
            new CatalogMod { Id = "new-mod", Name = "New", File = "mods/new/New.dll" },
            isInLibrary: false);

        vm.SetCatalogSelection(new[] { inLib, fresh });
        vm.AddCatalogModToLibraryCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ClearCatalogModSelection_clears_catalog_selection_count()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);
        var item = new CatalogModItemViewModel(
            new CatalogMod { Id = "new-mod", Name = "New", File = "mods/new/New.dll" },
            isInLibrary: false);

        vm.SetCatalogSelection(new[] { item });
        vm.SelectedCatalogMod = item;
        vm.ClearCatalogModSelectionCommand.Execute(null);

        vm.CatalogSelectionCount.Should().Be(0);
        vm.SelectedCatalogMod.Should().BeNull();
    }

    [Fact]
    public void Missing_enabled_package_ids_appear_in_warning_and_mods()
    {
        using var fx = Fixture.CreateReady();
        fx.Profiles.SetEnabled("default", "ghost-deadbeef", true);

        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.MissingEnabledPackagesWarning.Should().Contain("ghost-deadbeef");
        vm.Mods.Should().Contain(m => m.IsMissing && m.Package.Id == "ghost-deadbeef");
        vm.LogText.Should().Contain("ghost-deadbeef");
    }

    [Fact]
    public void NeedsMelonLoaderInstall_true_when_LoaderPresentAssembliesMissing()
    {
        using var fx = Fixture.CreateLoaderPresentAssembliesMissing();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.GameStatus!.Kind.Should().Be(GameStatusKind.LoaderPresentAssembliesMissing);
        vm.NeedsMelonLoaderInstall.Should().BeTrue();
        vm.InstallMelonLoaderCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void RefreshStatus_seeds_UnityDependencies_then_enables_offline_for_assemblies_missing()
    {
        using var fx = Fixture.CreateLoaderPresentAssembliesMissing();
        WriteFakeGlobalgamemanagers(fx.GameRoot, "2022.3.62f3");

        var redistRoot = Path.Combine(AppContext.BaseDirectory, "installer-redist");
        var unityDeps = Path.Combine(redistRoot, "unity-deps");
        Directory.CreateDirectory(unityDeps);
        var zipName = "UnityDependencies_2022.3.62.zip";
        var stagedZip = Path.Combine(unityDeps, zipName);
        File.WriteAllText(stagedZip, "deps-payload");

        try
        {
            var vm = fx.CreateVm(confirmHighRisk: _ => true);
            vm.RefreshStatusCommand.Execute(null);

            File.Exists(Path.Combine(
                fx.GameRoot,
                "MelonLoader",
                "Dependencies",
                "Il2CppAssemblyGenerator",
                zipName)).Should().BeTrue();
            var cfg = File.ReadAllText(Path.Combine(fx.GameRoot, "UserData", "Loader.cfg"));
            cfg.Should().MatchRegex(@"(?im)^\s*force_offline_generation\s*=\s*true\s*$");
        }
        finally
        {
            try { File.Delete(stagedZip); } catch { /* ignore */ }
            try { Directory.Delete(unityDeps, true); } catch { /* ignore */ }
            try { Directory.Delete(redistRoot, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void RefreshStatus_seed_fallback_version_enables_offline_when_unity_resolve_fails()
    {
        // No globalgamemanagers — seed uses sole redist zip version; Apply must still force offline.
        using var fx = Fixture.CreateLoaderPresentAssembliesMissing();

        var redistRoot = Path.Combine(AppContext.BaseDirectory, "installer-redist");
        var unityDeps = Path.Combine(redistRoot, "unity-deps");
        Directory.CreateDirectory(unityDeps);
        var zipName = "UnityDependencies_2022.3.62.zip";
        var stagedZip = Path.Combine(unityDeps, zipName);
        File.WriteAllText(stagedZip, "deps-payload");

        try
        {
            var vm = fx.CreateVm(confirmHighRisk: _ => true);
            vm.RefreshStatusCommand.Execute(null);

            File.Exists(Path.Combine(
                fx.GameRoot,
                "MelonLoader",
                "Dependencies",
                "Il2CppAssemblyGenerator",
                zipName)).Should().BeTrue();
            var cfg = File.ReadAllText(Path.Combine(fx.GameRoot, "UserData", "Loader.cfg"));
            cfg.Should().MatchRegex(@"(?im)^\s*force_offline_generation\s*=\s*true\s*$");
        }
        finally
        {
            try { File.Delete(stagedZip); } catch { /* ignore */ }
            try { Directory.Delete(unityDeps, true); } catch { /* ignore */ }
            try { Directory.Delete(redistRoot, true); } catch { /* ignore */ }
        }
    }

    static void WriteFakeGlobalgamemanagers(string game, string unityVersion)
    {
        var data = Path.Combine(game, "Mechabellum_Data");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(
            Path.Combine(data, "globalgamemanagers"),
            System.Text.Encoding.ASCII.GetBytes("xxxx" + unityVersion + "yyyy"));
    }

    sealed class RecordingStarter : IProcessStarter
    {
        public List<string> Starts { get; } = new();
        public void StartShell(string uriOrPath) => Starts.Add(uriOrPath);
    }

    sealed class Fixture : IDisposable
    {
        public string DataRoot { get; }
        public string GameRoot { get; }
        readonly PathsService _paths;
        readonly JsonStore _store;
        readonly ProfileService _profiles;
        readonly ModLibraryService _library;
        readonly GameDetector _detector;
        readonly DeployService _deploy;

        Fixture(string dataRoot, string gameRoot)
        {
            DataRoot = dataRoot;
            GameRoot = gameRoot;
            _paths = new PathsService(dataRoot);
            _paths.EnsureCreated();
            _store = new JsonStore();
            _profiles = new ProfileService(_paths, _store);
            _profiles.EnsureDefaults();
            _library = new ModLibraryService(_paths, new AssemblyInspector(), _store, _profiles);
            _detector = new GameDetector();
            _deploy = new DeployService(_paths, _store, new DeployPlanner(), _detector, new ProcessProbe());
        }

        public static Fixture CreateReady(bool highRisk = false)
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-vm-" + Guid.NewGuid().ToString("N"));
            var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-vm-game-" + Guid.NewGuid().ToString("N"));
            CreateReadyGame(gameRoot);
            return CreateWithGame(dataRoot, gameRoot, highRisk);
        }

        public static Fixture CreateLoaderPresentAssembliesMissing(bool highRisk = false)
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-vm-" + Guid.NewGuid().ToString("N"));
            var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-vm-game-" + Guid.NewGuid().ToString("N"));
            CreateLoaderMissingAssembliesGame(gameRoot);
            return CreateWithGame(dataRoot, gameRoot, highRisk);
        }

        static Fixture CreateWithGame(string dataRoot, string gameRoot, bool highRisk)
        {
            var fx = new Fixture(dataRoot, gameRoot);
            var pkgId = highRisk ? "cheat-aaaaaaaa" : "cam-aaaaaaaa";
            var displayName = highRisk ? "CheatCam" : "Cam";
            var dllName = highRisk ? "CheatCam.dll" : "Cam.dll";
            var pkgDir = Path.Combine(fx._paths.LibraryRoot, "mods", pkgId);
            Directory.CreateDirectory(pkgDir);
            File.WriteAllBytes(Path.Combine(pkgDir, dllName), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            var highRiskJson = highRisk ? "true" : "false";
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                $$"""
                {
                  "id": "{{pkgId}}",
                  "displayName": "{{displayName}}",
                  "type": "melon_mod",
                  "highRisk": {{highRiskJson}},
                  "files": [ { "relativePathInPackage": "{{dllName}}", "sha256": "aabbccdd" } ]
                }
                """);
            fx._store.Save(Path.Combine(fx._paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });
            fx._store.Save(fx._paths.ConfigPath, new AppConfig
            {
                GamePath = gameRoot,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            return fx;
        }

        public MainViewModel CreateVm(
            Func<string, bool>? confirmHighRisk,
            IProcessStarter? starter = null,
            Func<ModPackageType?>? pickPackageType = null,
            Func<string?>? openDll = null,
            Func<string?>? openZip = null)
        {
            var launcher = new GameLauncher(starter ?? new RecordingStarter(), () => false);
            return new MainViewModel(
                _paths,
                _store,
                _detector,
                _library,
                _profiles,
                _deploy,
                launcher,
                new RiskGate(),
                confirmHighRisk: confirmHighRisk,
                pickPackageType: pickPackageType,
                openDll: openDll,
                openZip: openZip);
        }

        public ModLibraryService Library => _library;
        public ProfileService Profiles => _profiles;
        public PathsService Paths => _paths;

        public void Dispose()
        {
            if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, true);
            if (Directory.Exists(GameRoot)) Directory.Delete(GameRoot, true);
        }

        static void CreateReadyGame(string root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
            File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "Il2CppAssemblies"));
            File.WriteAllText(Path.Combine(root, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "asm");
            File.WriteAllText(Path.Combine(root, "version.dll"), "");
        }

        static void CreateLoaderMissingAssembliesGame(string root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
            File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "net6"));
            File.WriteAllBytes(Path.Combine(root, "MelonLoader", "net6", "MelonLoader.dll"), new byte[] { 0x4D, 0x5A });
            File.WriteAllBytes(Path.Combine(root, "version.dll"), new byte[] { 0x4D, 0x5A });
        }
    }
}
