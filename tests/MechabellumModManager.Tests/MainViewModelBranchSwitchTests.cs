using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class MainViewModelBranchSwitchTests
{
    [Fact]
    public void AwaitingSteamSettle_disables_ApplyAndLaunch()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.IsReady.Should().BeTrue();
        vm.IsAwaitingSteamSettle = true;

        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeFalse();
        vm.ApplyProfileCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Feature_disabled_does_not_change_Ready_ApplyAndLaunch()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.BranchSwitchEnabled.Should().BeFalse();
        vm.IsAwaitingSteamSettle.Should().BeFalse();
        vm.IsReady.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeTrue();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeTrue();
        vm.ApplyProfileCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Corrupt_branch_switch_json_does_not_throw_in_ctor()
    {
        using var fx = Fixture.CreateReady();
        File.WriteAllText(fx.Paths.BranchSwitchConfigPath, "{not-valid-json");

        var act = () => fx.CreateVm();
        act.Should().NotThrow();
        act().BranchSwitchEnabled.Should().BeFalse();
    }

    [Fact]
    public void SelectedProfile_change_while_enabled_updates_active_branch_binding()
    {
        using var fx = Fixture.CreateReady();
        var extra = fx.Profiles.Create("Beta Build");
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta"
        });

        var vm = fx.CreateVm();
        vm.BranchSwitchEnabled.Should().BeTrue();
        vm.ActiveGameBranch.Should().Be(GameBranch.Official);

        var other = vm.Profiles.Should().ContainSingle(p => p.Id == extra.Id).Subject;
        vm.SelectedProfile = other;

        vm.OfficialProfileId.Should().Be(extra.Id);
        fx.LoadBranchConfig().OfficialProfileId.Should().Be(extra.Id);
        fx.LoadBranchConfig().BetaProfileId.Should().Be("default");
    }

    [Fact]
    public async Task SwitchToBeta_silent_fail_sets_degrade_and_settle_gate()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = ""
        });

        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirm: _ => true, starter: starter);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.IsReady.Should().BeTrue();

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        vm.DegradeToManualBeta.Should().BeTrue();
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeFalse();
        starter.Starts.Should().Contain("steam://open/games");
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmManualBeta_deploys_bound_profile_and_clears_settle()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = ""
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();

        vm.ConfirmManualBetaCommand.Execute(null);

        vm.IsAwaitingSteamSettle.Should().BeFalse();
        vm.DegradeToManualBeta.Should().BeFalse();
        vm.CanDeployOrLaunch.Should().BeTrue();
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeTrue();
        File.Exists(fx.Paths.DeployManifestPath).Should().BeFalse();
    }

    [Fact]
    public void Branch_switch_commands_exist()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.SwitchToOfficialCommand.Should().NotBeNull();
        vm.SwitchToBetaCommand.Should().NotBeNull();
        vm.StartBranchWizardCommand.Should().NotBeNull();
        vm.TeardownBranchSwitchCommand.Should().NotBeNull();
        vm.ConfirmManualBetaCommand.Should().NotBeNull();
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
        public string SteamLink { get; set; } = "";
        public string OfficialStore { get; set; } = "";
        public string BetaStore { get; set; } = "";
        public PathsService Paths { get; }
        public JsonStore Store { get; }
        public ProfileService Profiles { get; }
        public FakeProcessProbe Probe { get; } = new();
        readonly ModLibraryService _library;
        readonly GameDetector _detector;
        readonly DeployService _deploy;
        readonly BranchSwitchService _branchSwitch;
        readonly JunctionService _junctions = new();

        Fixture(string dataRoot, string gameRoot)
        {
            DataRoot = dataRoot;
            GameRoot = gameRoot;
            Paths = new PathsService(dataRoot);
            Paths.EnsureCreated();
            Store = new JsonStore();
            Profiles = new ProfileService(Paths, Store);
            Profiles.EnsureDefaults();
            _library = new ModLibraryService(Paths, new AssemblyInspector(), Store, Profiles);
            _detector = new GameDetector();
            _deploy = new DeployService(Paths, Store, new DeployPlanner(), _detector, new ProcessProbe());
            _branchSwitch = new BranchSwitchService(
                Paths, Store, Probe, _junctions, new SteamBetaKeyEditor(Probe));
        }

        public static Fixture CreateReady()
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-bsvm-" + Guid.NewGuid().ToString("N"));
            var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-bsvm-game-" + Guid.NewGuid().ToString("N"));
            CreateReadyGame(gameRoot);

            var fx = new Fixture(dataRoot, gameRoot);
            SeedLibrary(fx);
            fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
            {
                GamePath = gameRoot,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            return fx;
        }

        public static Fixture CreateReadyDualFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), "mmm-bsvm-d-" + Guid.NewGuid().ToString("N"));
            var dataRoot = Path.Combine(root, "data");
            var steamapps = Path.Combine(root, "steamapps");
            var common = Path.Combine(steamapps, "common");
            Directory.CreateDirectory(common);

            var official = Path.Combine(common, "Mechabellum_official");
            var beta = Path.Combine(common, "Mechabellum_beta");
            var steamLink = Path.Combine(common, "Mechabellum");
            CreateReadyGame(official);
            CreateReadyGame(beta);
            File.WriteAllText(Path.Combine(official, "marker.txt"), "official");
            File.WriteAllText(Path.Combine(beta, "marker.txt"), "beta");

            var fx = new Fixture(dataRoot, steamLink);
            fx.SteamLink = steamLink;
            fx.OfficialStore = official;
            fx.BetaStore = beta;
            fx._junctions.CreateJunction(steamLink, official);
            File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
                """
                "AppState"
                {
                	"appid"		"669330"
                	"UserConfig"
                	{
                		"language"		"english"
                		"BetaKey"		"oldbeta"
                	}
                }
                """);

            SeedLibrary(fx);
            fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
            {
                GamePath = steamLink,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            return fx;
        }

        static void SeedLibrary(Fixture fx)
        {
            const string pkgId = "cam-aaaaaaaa";
            var pkgDir = Path.Combine(fx.Paths.LibraryRoot, "mods", pkgId);
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
            fx.Store.Save(Path.Combine(fx.Paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });
        }

        public MainViewModel CreateVm(
            Func<string, bool>? confirm = null,
            IProcessStarter? starter = null)
        {
            var launcher = new GameLauncher(starter ?? new RecordingStarter(), () => false);
            return new MainViewModel(
                Paths,
                Store,
                _detector,
                _library,
                Profiles,
                _deploy,
                launcher,
                new RiskGate(),
                confirmHighRisk: _ => true,
                confirm: confirm ?? (_ => false),
                branchSwitch: _branchSwitch,
                processProbe: Probe,
                processStarter: starter ?? new RecordingStarter());
        }

        public void WriteBranchConfig(BranchSwitchConfig cfg) =>
            _branchSwitch.SaveConfig(cfg);

        public BranchSwitchConfig LoadBranchConfig() => _branchSwitch.LoadConfig();

        public void Dispose()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(SteamLink) && _junctions.IsJunction(SteamLink))
                    _junctions.DeleteJunction(SteamLink);
            }
            catch
            {
                // Best-effort unlink.
            }

            TryDelete(DataRoot);
            TryDelete(GameRoot);
            if (!string.IsNullOrWhiteSpace(SteamLink))
            {
                var root = Path.GetFullPath(Path.Combine(SteamLink, "..", "..", ".."));
                TryDelete(root);
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // Temp leftover is non-fatal.
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
}
