using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class MainViewModelTests
{
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

            var fx = new Fixture(dataRoot, gameRoot);
            var pkgDir = Path.Combine(fx._paths.LibraryRoot, "mods", "cam-aaaaaaaa");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllBytes(Path.Combine(pkgDir, "Cam.dll"), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            var highRiskJson = highRisk ? "true" : "false";
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                $$"""
                {
                  "id": "cam-aaaaaaaa",
                  "displayName": "Cam",
                  "type": "melon_mod",
                  "highRisk": {{highRiskJson}},
                  "files": [ { "relativePathInPackage": "Cam.dll", "sha256": "aabbccdd" } ]
                }
                """);
            fx._store.Save(Path.Combine(fx._paths.LibraryRoot, "index.json"), new { packageIds = new[] { "cam-aaaaaaaa" } });
            fx._store.Save(fx._paths.ConfigPath, new AppConfig
            {
                GamePath = gameRoot,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            return fx;
        }

        public MainViewModel CreateVm(Func<string, bool>? confirmHighRisk, IProcessStarter? starter = null)
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
                confirmHighRisk: confirmHighRisk);
        }

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
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
            File.WriteAllText(Path.Combine(root, "version.dll"), "");
        }
    }
}
