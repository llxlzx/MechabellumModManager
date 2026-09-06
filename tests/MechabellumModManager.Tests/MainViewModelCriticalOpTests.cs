using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using MechabellumModManager.Tests.Support;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class MainViewModelCriticalOpTests
{
    [Fact]
    public async Task CheckForUpdates_hard_blocks_when_guard_running()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
        WriteInterruptedMarker(fx.Paths);
        var vm = fx.CreateVm(criticalOp: fx.Guard);

        vm.ShouldOfferCriticalRecovery(fx.Guard).Should().BeTrue();
    }

    [Fact]
    public void ShouldOfferCriticalRecovery_false_when_no_marker()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var vm = fx.CreateVm(criticalOp: fx.Guard);

        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
        vm.ShouldOfferCriticalRecovery(fx.Guard).Should().BeFalse();
    }

    [Fact]
    public void ApplyRecoveryContinue_mid_wizard_Linked_is_SoftConfirm_not_Allow()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
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
    public void ApplyRecoveryContinue_Enabled_Ready_clears_gate_when_repair_not_offered()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        WriteInterruptedMarker(fx.Paths);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            ActiveBranch = GameBranch.Official,
            SteamLinkPath = fx.GameRoot,
            OfficialStorePath = fx.GameRoot,
            BetaStorePath = fx.GameRoot
        });
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.IsRecoveryGateActive = true;
        vm.ShowRepairOrphanDualLayout.Should().BeFalse();

        vm.ApplyRecoveryContinue();

        vm.IsRecoveryGateActive.Should().BeFalse();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
    }

    [Fact]
    public void CanOfferAssemblyGeneratePrompt_false_when_recovery_gate_active()
    {
        using var fx = Fixture.CreateMissingAssemblies();
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.GameStatus?.Kind.Should().Be(GameStatusKind.LoaderPresentAssembliesMissing);
        vm.IsRecoveryGateActive = true;
        vm.CanOfferAssemblyGeneratePrompt.Should().BeFalse();
    }

    [Fact]
    public void ApplyProfile_sync_does_not_generate_when_assemblies_missing()
    {
        using var fx = Fixture.CreateMissingAssemblies();
        var vm = fx.CreateVm(criticalOp: fx.Guard);
        vm.GameStatus?.Kind.Should().Be(GameStatusKind.LoaderPresentAssembliesMissing);

        vm.ApplyProfile().Should().BeFalse();
        vm.GameStatus?.Kind.Should().Be(GameStatusKind.LoaderPresentAssembliesMissing);
    }

    [Fact]
    public void ApplyRecoveryContinue_clears_gate_routes_settle_ui()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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
    public void TryHandleWindowClosing_soft_busy_cancels_work_and_requests_exit()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var exit = 0;
        var vm = fx.CreateVm(
            confirm: _ => true,
            criticalOp: fx.Guard,
            requestProcessExit: () => exit++);
        vm.IsBranchSwitchBusy = true;

        vm.EvaluateCloseOrUpdateGate(busyDialogOpen: true).Should().Be(CriticalOpGateLevel.SoftConfirm);

        var handled = vm.TryHandleWindowClosing(busyDialogOpen: true, out var cancel);

        handled.Should().BeFalse();
        cancel.Should().BeFalse();
        exit.Should().Be(1);
    }

    [Fact]
    public void TryHandleWindowClosing_hard_cancels_when_guard_running()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
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
        using var fx = Fixture.CreateReadyForCriticalOp();
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

}
