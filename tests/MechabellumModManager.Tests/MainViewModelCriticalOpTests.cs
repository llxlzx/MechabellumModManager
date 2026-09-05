using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class MainViewModelCriticalOpTests
{
    [Fact]
    public async Task CheckForUpdates_hard_blocks_when_guard_running()
    {
        using var fx = Fixture.CreateReady();
        var handler = new RecordingHttpHandler();
        var checker = new UpdateChecker(new HttpClient(handler), () => "1.0.0");
        var confirms = new List<string>();
        var notes = new List<string>();
        var vm = fx.CreateVm(
            confirm: msg =>
            {
                confirms.Add(msg);
                return true;
            },
            notify: notes.Add,
            updateChecker: checker,
            criticalOp: fx.Guard);

        fx.Guard.Begin(CriticalOpKind.BranchDiskWrite, "SwitchToBeta");

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        notes.Should().Contain(LocalizationService.T("CriticalOpBlockUpdate"));
        confirms.Should().BeEmpty();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CheckForUpdates_soft_requires_confirm_when_awaiting_settle()
    {
        using var fx = Fixture.CreateReady();
        var handler = new RecordingHttpHandler();
        var checker = new UpdateChecker(new HttpClient(handler), () => "1.0.0");
        var confirms = new List<string>();
        var confirmResult = false;
        var vm = fx.CreateVm(
            confirm: msg =>
            {
                confirms.Add(msg);
                // Accept only the soft-update gate; never confirm opening a download URL.
                return confirmResult && msg.Contains(SoftUpdateNeedle(), StringComparison.Ordinal);
            },
            notify: _ => { },
            updateChecker: checker,
            criticalOp: fx.Guard);

        vm.IsAwaitingSteamSettle = true;

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        confirms.Should().ContainSingle(msg =>
            msg.Contains(SoftUpdateNeedle(), StringComparison.Ordinal));
        handler.Calls.Should().Be(0);

        confirms.Clear();
        confirmResult = true;
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        confirms.Should().Contain(msg =>
            msg.Contains(SoftUpdateNeedle(), StringComparison.Ordinal));
        handler.Calls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunWithCriticalOpAsync_action_throws_still_completes()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        var began = false;

        var act = async () => await vm.RunWithCriticalOpAsync(
            CriticalOpKind.BranchDiskWrite,
            "SwitchToBeta",
            () =>
            {
                began = fx.Guard.IsRunning && File.Exists(fx.Paths.CriticalOpMarkerPath);
                throw new InvalidOperationException("boom");
            });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        began.Should().BeTrue();
        fx.Guard.IsRunning.Should().BeFalse();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
    }

    [Fact]
    public void IsRecoveryGateActive_locks_deploy()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(criticalOp: fx.Guard);

        vm.IsReady.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeTrue();

        vm.IsRecoveryGateActive = true;

        vm.IsSessionLocked.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeFalse();
        vm.IsDirty = true;
        vm.ApplyProfileCommand.CanExecute(null).Should().BeFalse();
        vm.ApplyProfile().Should().BeFalse();
        vm.EvaluateCloseOrUpdateGate(busyDialogOpen: false).Should().Be(CriticalOpGateLevel.HardBlock);
    }

    [Fact]
    public void ApplyRecoveryContinue_clears_stale_Ready_marker()
    {
        using var fx = Fixture.CreateReady();
        WriteInterruptedMarker(fx.Paths);
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.IsRecoveryGateActive = true;
        vm.IsAwaitingSteamSettle.Should().BeFalse();
        vm.IsBranchWizardInProgress.Should().BeFalse();
        fx.Guard.TryLoadInterrupted(out _).Should().BeTrue();

        vm.ApplyRecoveryContinue();

        vm.IsRecoveryGateActive.Should().BeFalse();
        vm.IsSessionLocked.Should().BeFalse();
        vm.CanDeployOrLaunch.Should().BeTrue();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
        fx.Guard.TryLoadInterrupted(out _).Should().BeFalse();
    }

    [Fact]
    public void ShouldOfferCriticalRecovery_true_when_interrupted_marker()
    {
        using var fx = Fixture.CreateReady();
        WriteInterruptedMarker(fx.Paths);
        var vm = fx.CreateVm(criticalOp: fx.Guard);

        vm.ShouldOfferCriticalRecovery(fx.Guard).Should().BeTrue();
    }

    [Fact]
    public void ShouldOfferCriticalRecovery_false_when_no_marker()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(criticalOp: fx.Guard);

        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
        vm.ShouldOfferCriticalRecovery(fx.Guard).Should().BeFalse();
    }

    [Fact]
    public void ApplyRecoveryContinue_mid_wizard_Linked_is_SoftConfirm_not_Allow()
    {
        using var fx = Fixture.CreateReady();
        WriteInterruptedMarker(fx.Paths);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Linked,
            ActiveBranch = GameBranch.Official
        });
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.IsRecoveryGateActive = true;
        vm.IsBranchWizardInProgress.Should().BeTrue();
        vm.IsWizardWaitingSteam.Should().BeFalse();

        vm.ApplyRecoveryContinue();

        vm.IsRecoveryGateActive.Should().BeFalse();
        vm.IsBranchWizardInProgress.Should().BeTrue();
        vm.EvaluateCloseOrUpdateGate(busyDialogOpen: false).Should().Be(CriticalOpGateLevel.SoftConfirm);
    }

    [Fact]
    public void ApplyRecoveryContinue_clears_gate_routes_settle_ui()
    {
        using var fx = Fixture.CreateReady();
        WriteInterruptedMarker(fx.Paths);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.AwaitingSteamSettle,
            ActiveBranch = GameBranch.Official
        });
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.IsRecoveryGateActive = true;
        vm.IsAwaitingSteamSettle.Should().BeTrue();

        vm.ApplyRecoveryContinue();

        vm.IsRecoveryGateActive.Should().BeFalse();
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.IsSessionLocked.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
        fx.Guard.TryLoadInterrupted(out _).Should().BeFalse();
        vm.ActiveContentPage.Should().Be(MainContentPage.Settings);
        vm.ShowConfirmManualBeta.Should().BeTrue();
        vm.EvaluateCloseOrUpdateGate(busyDialogOpen: false).Should().Be(CriticalOpGateLevel.SoftConfirm);
    }

    [Fact]
    public async Task ApplyRecoveryRepair_clears_stale_Ready_marker()
    {
        using var fx = Fixture.CreateReady();
        WriteInterruptedMarker(fx.Paths);
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.IsRecoveryGateActive = true;

        await vm.ApplyRecoveryRepair();

        vm.IsRecoveryGateActive.Should().BeFalse();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
    }

    [Fact]
    public async Task ApplyRecoveryRepair_clears_gate_when_settle_or_wizard()
    {
        using var fx = Fixture.CreateReady();
        WriteInterruptedMarker(fx.Paths);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.AwaitingSteamSettle,
            ActiveBranch = GameBranch.Official
        });
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.IsRecoveryGateActive = true;
        vm.IsAwaitingSteamSettle.Should().BeTrue();

        await vm.ApplyRecoveryRepair();

        vm.IsRecoveryGateActive.Should().BeFalse();
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.IsSessionLocked.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
        fx.Guard.TryLoadInterrupted(out _).Should().BeFalse();
        vm.ActiveContentPage.Should().Be(MainContentPage.Settings);
        vm.ShowConfirmManualBeta.Should().BeTrue();
        vm.EvaluateCloseOrUpdateGate(busyDialogOpen: false).Should().Be(CriticalOpGateLevel.SoftConfirm);
    }

    static void WriteInterruptedMarker(PathsService paths)
    {
        Directory.CreateDirectory(paths.DataRoot);
        File.WriteAllText(
            paths.CriticalOpMarkerPath,
            """{"kind":"BranchDiskWrite","startedUtc":"2026-09-05T12:00:00+00:00","detail":"stale","pid":1}""");
    }

    [Fact]
    public void TryHandleWindowClosing_hard_cancels_when_guard_running()
    {
        using var fx = Fixture.CreateReady();
        var notes = new List<string>();
        var vm = fx.CreateVm(notify: notes.Add, criticalOp: fx.Guard);
        fx.Guard.Begin(CriticalOpKind.MelonInstall, "MelonInstall");

        var handled = vm.TryHandleWindowClosing(busyDialogOpen: false, out var cancel);

        handled.Should().BeTrue();
        cancel.Should().BeTrue();
        notes.Should().Contain(LocalizationService.T("CriticalOpBlockClose"));
    }

    [Fact]
    public void TryHandleWindowClosing_soft_respects_confirm_when_awaiting_settle()
    {
        using var fx = Fixture.CreateReady();
        var confirmResult = false;
        var confirms = new List<string>();
        var vm = fx.CreateVm(
            confirm: msg =>
            {
                confirms.Add(msg);
                return confirmResult;
            },
            criticalOp: fx.Guard);
        vm.IsAwaitingSteamSettle = true;

        vm.TryHandleWindowClosing(busyDialogOpen: false, out var cancelDecline);
        cancelDecline.Should().BeTrue();
        confirms.Should().Contain(msg =>
            msg.Contains(SoftCloseNeedle(), StringComparison.Ordinal));

        confirmResult = true;
        vm.TryHandleWindowClosing(busyDialogOpen: false, out var cancelAccept);
        cancelAccept.Should().BeFalse();
    }

    static string SoftUpdateNeedle()
    {
        var formatted = string.Format(LocalizationService.T("CriticalOpSoftUpdateConfirm"), "\u0000");
        var prefix = formatted.Split('\u0000')[0];
        return string.IsNullOrEmpty(prefix)
            ? LocalizationService.T("CriticalOpSoftUpdateConfirm")
            : prefix;
    }

    static string SoftCloseNeedle()
    {
        var formatted = string.Format(LocalizationService.T("CriticalOpSoftCloseConfirm"), "\u0000");
        var prefix = formatted.Split('\u0000')[0];
        return string.IsNullOrEmpty(prefix)
            ? LocalizationService.T("CriticalOpSoftCloseConfirm")
            : prefix;
    }

    sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            var json = """{"version":"99.0.0","notes":"n","setupUrl":"https://example.test/setup.exe"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    sealed class Fixture : IDisposable
    {
        public string DataRoot { get; }
        public string GameRoot { get; }
        public PathsService Paths { get; }
        public CriticalOpGuard Guard { get; }
        readonly JsonStore _store;
        readonly ProfileService _profiles;
        readonly ModLibraryService _library;
        readonly GameDetector _detector;
        readonly DeployService _deploy;

        Fixture(string dataRoot, string gameRoot)
        {
            DataRoot = dataRoot;
            GameRoot = gameRoot;
            Paths = new PathsService(dataRoot);
            Paths.EnsureCreated();
            Guard = new CriticalOpGuard(Paths);
            _store = new JsonStore();
            _profiles = new ProfileService(Paths, _store);
            _profiles.EnsureDefaults();
            _library = new ModLibraryService(Paths, new AssemblyInspector(), _store, _profiles);
            _detector = new GameDetector();
            _deploy = new DeployService(Paths, _store, new DeployPlanner(), _detector, new ProcessProbe());
        }

        public static Fixture CreateReady()
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-critop-vm-" + Guid.NewGuid().ToString("N"));
            var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-critop-game-" + Guid.NewGuid().ToString("N"));
            CreateReadyGame(gameRoot);
            var fx = new Fixture(dataRoot, gameRoot);
            SeedLibrary(fx);
            fx._store.Save(fx.Paths.ConfigPath, new AppConfig
            {
                GamePath = gameRoot,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            return fx;
        }

        public MainViewModel CreateVm(
            Func<string, bool>? confirm = null,
            Action<string>? notify = null,
            UpdateChecker? updateChecker = null,
            CriticalOpGuard? criticalOp = null)
        {
            var starter = new NoopStarter();
            var launcher = new GameLauncher(starter, () => false);
            return new MainViewModel(
                Paths,
                _store,
                _detector,
                _library,
                _profiles,
                _deploy,
                launcher,
                new RiskGate(),
                updateChecker: updateChecker,
                confirmHighRisk: _ => true,
                confirm: confirm ?? (_ => false),
                notify: notify,
                processStarter: starter,
                criticalOp: criticalOp ?? Guard);
        }

        public void WriteBranchConfig(BranchSwitchConfig cfg) =>
            _store.Save(Paths.BranchSwitchConfigPath, cfg);

        public void Dispose()
        {
            TryDelete(DataRoot);
            TryDelete(GameRoot);
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
            fx._store.Save(Path.Combine(fx.Paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });
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
    }

    sealed class NoopStarter : IProcessStarter
    {
        public void StartShell(string uriOrPath) { }
    }
}
