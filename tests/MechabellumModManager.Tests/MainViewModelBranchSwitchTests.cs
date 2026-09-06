using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using MechabellumModManager.Tests.Support;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;
using System.Windows;

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
        vm.IsDirty = true;
        vm.ApplyProfileCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Start_with_stuck_WaitingDownloadB_rolls_back_instead_of_resume()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.Junctions.DeleteJunction(fx.SteamLink);
        Directory.CreateDirectory(fx.SteamLink);
        File.WriteAllText(Path.Combine(fx.SteamLink, "Mechabellum.exe"), "x");
        Directory.Delete(fx.BetaStore, recursive: true);

        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.WaitingDownloadB,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test",
            SessionOwnedOfficialStore = true
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;

        // Restart policy: mid-enable is rolled back on load — never resumed.
        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        vm.CanStartBranchWizard.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public void In_session_WaitingDownloadB_shows_enabling_status_and_emergency()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.None,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm();
        vm.GamePath = fx.SteamLink;
        vm.BranchWizardStep = BranchWizardStep.WaitingDownloadB;
        vm.RefreshStatusCommand.Execute(null);

        (vm.BranchStatusText ?? "").Should().Contain("启用中");
        vm.CanEmergencyRecoverSingle.Should().BeTrue();
        vm.CanStartBranchWizard.Should().BeFalse();
        vm.CancelBusyWork();
    }

    [Fact]
    public void WaitingDownloadB_hollow_path_does_not_banner_game_missing()
    {
        using var fx = Fixture.CreateReady();
        var hollow = Path.Combine(Path.GetDirectoryName(fx.GameRoot)!, "hollow-mech-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(hollow);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.None,
            SteamLinkPath = hollow,
            OfficialStorePath = fx.GameRoot + "_official",
            ActiveBranch = GameBranch.Official,
            SessionOwnedOfficialStore = true
        });

        var vm = fx.CreateVm();
        vm.GamePath = hollow;
        vm.BranchWizardStep = BranchWizardStep.WaitingDownloadB;
        var before = vm.LogText ?? "";
        vm.RefreshStatusCommand.Execute(null);

        vm.StatusKindLabel.Should().Be(LocalizationService.T("BranchStatusEnabling"));
        vm.DeployBlockedReason.Should().Contain("启用中");
        vm.DeployBlockedReason.Should().NotContain("未找到游戏");
        vm.NeedsMelonLoaderInstall.Should().BeFalse();
        vm.CanOfferAssemblyGeneratePrompt.Should().BeFalse();
        var added = (vm.LogText ?? "").Replace(before, "", StringComparison.Ordinal);
        added.Should().NotContain("未找到有效的 Mechabellum");
        vm.CancelBusyWork();
    }

    [Fact]
    public void WaitingDownloadB_disk_ready_steam_busy_asks_exit_and_does_not_auto_continue()
    {
        using var fx = Fixture.CreateWizardStart();
        var steamapps = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", ".."));
        var acf = """
            "AppState"
            {
            	"appid"		"669330"
            	"StateFlags"		"4"
            	"buildid"		"200"
            	"TargetBuildID"		"200"
            	"BytesToDownload"		"0"
            	"BytesDownloaded"		"0"
            	"BytesToStage"		"0"
            	"BytesStaged"		"0"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"publicbeta"
            	}
            }
            """;
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"), acf);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.None,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            BetaBranchName = "publicbeta",
            SessionOwnedOfficialStore = true
        });
        fx.Probe.SteamRunning = true;

        var vm = fx.CreateVm();
        vm.GamePath = fx.SteamLink;
        vm.BranchWizardStep = BranchWizardStep.WaitingDownloadB;
        vm.RefreshStatusCommand.Execute(null);

        vm.StatusKindLabel.Should().Be(LocalizationService.T("BranchStatusEnabling"));
        vm.DeployBlockedReason.Should().Contain("退出 Steam");
        vm.DeployBlockedReason.Should().NotContain("未找到游戏");
        vm.IsWizardDownloadReadyNow().Should().BeFalse();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.WaitingDownloadB);
        vm.CancelBusyWork();
    }

    [Fact]
    public void Leftover_mid_wizard_is_cleared_on_load_no_resume()
    {
        using var fx = Fixture.CreateReady();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.WaitingDownloadB,
            ActiveBranch = GameBranch.Official,
            SessionOwnedOfficialStore = true
        });

        var vm = fx.CreateVm();

        vm.BranchSwitchEnabled.Should().BeFalse();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        vm.IsBranchWizardInProgress.Should().BeFalse();
        vm.CanStartBranchWizard.Should().BeTrue();
        vm.CancelBusyWork();
    }

    [Fact]
    public void In_session_mid_enable_session_locks_deploy()
    {
        using var fx = Fixture.CreateReady();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.None,
            ActiveBranch = GameBranch.Official
        });

        var vm = fx.CreateVm();
        vm.BranchWizardStep = BranchWizardStep.WaitingDownloadB;

        vm.IsBranchWizardInProgress.Should().BeTrue();
        vm.IsBranchWizardBlocking.Should().BeFalse();
        vm.IsSessionLocked.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.CanEmergencyRecoverSingle.Should().BeTrue();
        vm.CancelBusyWork();
    }

    [Fact]
    public void Incomplete_wizard_with_feature_enabled_blocks_deploy()
    {
        using var fx = Fixture.CreateReady();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Linked,
            ActiveBranch = GameBranch.Official
        });

        var vm = fx.CreateVm();

        vm.BranchSwitchEnabled.Should().BeTrue();
        vm.IsBranchWizardBlocking.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeFalse();
        vm.CanTeardownBranchSwitch.Should().BeTrue();
        vm.DeployBlockedReason.Should().NotBeNullOrWhiteSpace();
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
            BetaBranchName = "public_test"
        });

        // Force silent Beta write failure (manifest missing).
        var acf = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(fx.SteamLink))!, "appmanifest_669330.acf");
        if (File.Exists(acf))
            File.Delete(acf);

        var starter = new RecordingStarter();
        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, starter: starter, notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.IsReady.Should().BeTrue();

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        vm.DegradeToManualBeta.Should().BeTrue();
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeFalse();
        starter.Starts.Should().NotContain("steam://open/games");
        (vm.LogText + string.Join('\n', notes)).Should().Contain("请勿启动 Steam");
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
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        WriteSettledAcf(fx, betaKey: "public_test");

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeFalse();
        vm.DegradeToManualBeta.Should().BeFalse();
        vm.CanDeployOrLaunch.Should().BeTrue();
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeTrue();
        File.Exists(fx.Paths.DeployManifestPath).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmManualBeta_keeps_settle_when_game_missing()
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
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        WriteSettledAcf(fx, betaKey: "public_test");

        File.Delete(Path.Combine(fx.BetaStore, "Mechabellum.exe"));

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.AwaitingSteamSettle);
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmManualBeta_keeps_settle_when_acf_not_settled()
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
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        // Fixture ACF remains unsettled (no buildid / TargetBuildID alignment).

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.AwaitingSteamSettle);
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeTrue(
            "Apply may succeed; unsettle ACF must still block ClearSteamSettle");
    }

    [Fact]
    public async Task ConfirmManualBeta_blocks_when_update_unhealthy_518()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.AwaitingSteamSettle,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Beta,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta"
        });

        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.IsAwaitingSteamSettle = true;
        vm.BranchWizardStep = BranchWizardStep.AwaitingSteamSettle;

        var steamapps = Path.GetDirectoryName(Path.GetDirectoryName(fx.SteamLink))!;
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"StateFlags"		"518"
            	"buildid"		"100"
            	"TargetBuildID"		"0"
            	"BytesToDownload"		"0"
            	"BytesDownloaded"		"0"
            	"BytesToStage"		"0"
            	"BytesStaged"		"0"
            	"UserConfig"
            	{
            		"BetaKey"		"publicbeta"
            	}
            }
            """);

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeTrue();
        notes.Should().Contain(LocalizationService.T("NotifySettleBlockedUpdateUnhealthy"));
    }

    [Fact]
    public async Task ConfirmManualBeta_keeps_settle_when_acf_snapshot_missing()
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
            BetaBranchName = "public_test"
        });

        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        WriteSettledAcf(fx, betaKey: "public_test");

        var acf = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", "..", "appmanifest_669330.acf"));
        File.Delete(acf);

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeTrue(
            "snapshot failure must not ClearSteamSettle (no deferred Ready)");
        notes.Should().Contain(LocalizationService.T("NotifySettleBlockedSnapshotFailed"));
        notes.Should().NotContain(n => n.Contains("急救", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfirmManualBeta_not_aligned_waits_without_emergency_cta()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.AwaitingSteamSettle,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test"
        });

        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.IsAwaitingSteamSettle = true;
        vm.BranchWizardStep = BranchWizardStep.AwaitingSteamSettle;
        vm.ActiveGameBranch = GameBranch.Official;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        // Junction is official, but live ACF still has the beta key / beta identity.
        WriteSettledAcf(fx, betaKey: "public_test", buildId: "25139974");

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeTrue();
        notes.Should().Contain(LocalizationService.T("NotifySettleBlockedSnapshotNotAligned"));
        notes.Should().NotContain(n => n.Contains("急救", StringComparison.Ordinal));
        notes.Should().NotContain(n => n.Contains("切到另一", StringComparison.Ordinal));
    }

    [Fact]
    public void Refresh_degrades_ready_when_active_store_hollow()
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
            BetaBranchName = "public_test"
        });

        File.Delete(Path.Combine(fx.OfficialStore, "GameAssembly.dll"));

        var notes = new List<string>();
        var vm = fx.CreateVm(notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        vm.BranchWizardStep.Should().Be(BranchWizardStep.AwaitingSteamSettle);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        Directory.Exists(fx.OfficialStore).Should().BeTrue();
        notes.Should().Contain(LocalizationService.T("NotifyHollowStoreDegraded"));
    }

    [Fact]
    public void Refresh_does_not_degrade_ready_for_acf_hardfault_when_root_intact()
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
            BetaBranchName = "public_test"
        });

        var acf = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", "..", "appmanifest_669330.acf"));
        File.WriteAllText(acf,
            """
            "AppState"
            {
            	"StateFlags"		"1190"
            	"buildid"		"1"
            	"TargetBuildID"		"1"
            	"BytesToDownload"		"100"
            	"BytesDownloaded"		"0"
            }
            """);

        var vm = fx.CreateVm();
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        vm.BranchWizardStep.Should().Be(BranchWizardStep.Ready);
        vm.IsAwaitingSteamSettle.Should().BeFalse();
    }

    [Fact]
    public async Task Hollow_ready_degraded_can_switch_to_complete_other_store()
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
            BetaBranchName = "public_test"
        });

        File.Delete(Path.Combine(fx.OfficialStore, "GameAssembly.dll"));

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.CanSwitchGameBranch.Should().BeTrue();
        vm.CanTeardownBranchSwitch.Should().BeTrue();

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        vm.ActiveGameBranch.Should().Be(GameBranch.Beta);
        SteamGameLocator.LooksLikeGameRoot(fx.BetaStore).Should().BeTrue();
    }

    static void WriteSettledAcf(Fixture fx, string? betaKey, string buildId = "200")
    {
        var acf = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", "..", "appmanifest_669330.acf"));
        var betaLine = string.IsNullOrWhiteSpace(betaKey)
            ? ""
            : "\t\t\"BetaKey\"\t\t\"" + betaKey + "\"\n";
        File.WriteAllText(acf,
            "\"AppState\"\n{\n" +
            "\t\"appid\"\t\t\"669330\"\n" +
            "\t\"StateFlags\"\t\t\"4\"\n" +
            "\t\"buildid\"\t\t\"" + buildId + "\"\n" +
            "\t\"TargetBuildID\"\t\t\"" + buildId + "\"\n" +
            "\t\"BytesToDownload\"\t\t\"0\"\n" +
            "\t\"BytesDownloaded\"\t\t\"0\"\n" +
            "\t\"BytesToStage\"\t\t\"0\"\n" +
            "\t\"BytesStaged\"\t\t\"0\"\n" +
            "\t\"UserConfig\"\n\t{\n\t\t\"language\"\t\t\"english\"\n" + betaLine +
            "\t}\n" +
            "\t\"MountedConfig\"\n\t{\n\t\t\"language\"\t\t\"english\"\n" + betaLine +
            "\t}\n}\n");
    }

    [Fact]
    public async Task SwitchToBranch_shows_busy_around_swap_and_ends_even_if_steam_wait_fails()
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
            BetaBranchName = "publicbeta"
        });
        fx.Probe.SteamRunning = true;

        var begins = 0;
        var ends = 0;
        var vm = fx.CreateVm(
            confirm: _ => true,
            delay: _ => Task.CompletedTask,
            steamExitTimeout: TimeSpan.Zero,
            beginBusy: (_, _) => begins++,
            setBusyMessage: _ => { },
            endBusy: () => ends++);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        begins.Should().BeGreaterThanOrEqualTo(1);
        ends.Should().Be(begins);
        vm.ActiveGameBranch.Should().Be(GameBranch.Official);
        File.ReadAllText(Path.Combine(fx.SteamLink, "marker.txt")).Should().Be("official");
    }

    [Fact]
    public async Task SwitchToBeta_steam_still_running_aborts_before_swap()
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
            BetaBranchName = "publicbeta"
        });
        fx.Probe.SteamRunning = true;

        var starter = new RecordingStarter();
        var vm = fx.CreateVm(
            confirm: _ => true,
            starter: starter,
            delay: _ => Task.CompletedTask,
            steamExitTimeout: TimeSpan.Zero);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        starter.Starts.Should().Contain("steam://exit");
        starter.Starts.Should().NotContain("steam://open/games");
        fx.Probe.ForceCloseCalls.Should().BeGreaterThan(0);
        vm.ActiveGameBranch.Should().Be(GameBranch.Official);
        vm.IsAwaitingSteamSettle.Should().BeFalse();
        File.ReadAllText(Path.Combine(fx.SteamLink, "marker.txt")).Should().Be("official");
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeFalse();
    }

    [Fact]
    public async Task SwitchToBeta_when_already_aligned_skips_steam_restart()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.Junctions.DeleteJunction(fx.SteamLink);
        fx.Junctions.CreateJunction(fx.SteamLink, fx.BetaStore);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(fx.SteamLink))!, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"public_test"
            	}
            }
            """);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Beta,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test"
        });

        var starter = new RecordingStarter();
        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, starter: starter, notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        starter.Starts.Should().BeEmpty();
        vm.IsAwaitingSteamSettle.Should().BeFalse();
        (vm.LogText + string.Join('\n', notes)).Should().Contain("已是该服");
    }

    [Fact]
    public async Task RepairOrphanDualLayout_materializes_when_disabled_but_junction_remains()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.None,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = "",
            BetaStorePath = "",
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta"
        });

        var notes = new List<string>();
        var vm = fx.CreateVm(
            confirmChoice: (msg, _) => !msg.Contains("删除另一", StringComparison.Ordinal),
            notify: notes.Add);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        vm.BranchSwitchEnabled.Should().BeFalse();
        vm.ShowRepairOrphanDualLayout.Should().BeTrue();
        vm.CanRepairOrphanDualLayout.Should().BeTrue();

        await vm.RepairOrphanDualLayoutCommand.ExecuteAsync(null);

        fx.Junctions.IsJunction(fx.SteamLink).Should().BeFalse();
        File.Exists(Path.Combine(fx.SteamLink, "Mechabellum.exe")).Should().BeTrue();
        Directory.Exists(fx.BetaStore).Should().BeTrue();
        (vm.LogText + string.Join('\n', notes)).Should().Contain("单目录");
    }

    [Fact]
    public void ShowRepairOrphanDualLayout_false_when_dual_folder_enabled()
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
            BetaBranchName = "publicbeta"
        });
        var vm = fx.CreateVm();
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        vm.BranchSwitchEnabled.Should().BeTrue();
        vm.ShowRepairOrphanDualLayout.Should().BeFalse();
    }

    [Fact]
    public async Task StartBranchWizard_rejects_when_gamePath_is_store_and_Mechabellum_missing()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.Junctions.DeleteJunction(fx.SteamLink);
        if (Directory.Exists(fx.SteamLink))
            Directory.Delete(fx.SteamLink, recursive: true);

        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, notify: notes.Add);
        vm.GamePath = fx.OfficialStore;
        vm.RefreshStatusCommand.Execute(null);

        await vm.StartBranchWizardCommand.ExecuteAsync(null);

        var text = vm.LogText + string.Join('\n', notes);
        text.Should().Contain("双服仓");
        text.Should().Contain("Mechabellum");
        text.Should().NotContain("Store path already exists.");
        fx.LoadBranchConfig().Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task StartBranchWizard_normalizes_store_gamePath_to_Mechabellum_when_link_exists()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        // Dual folder already has link→official; also leave beta. Point GamePath at official store folder path.
        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, notify: notes.Add, delay: _ => Task.CompletedTask);
        vm.GamePath = fx.OfficialStore;
        vm.RefreshStatusCommand.Execute(null);

        // Pre-create would-be dest conflict shouldn't apply when we normalize to Mechabellum link.
        // Delete junction so link path is missing as a real dir — wait, CreateReadyDualFolder has junction.
        // For normalize test: have BOTH Mechabellum (junction) and we're selecting official path string.
        await vm.StartBranchWizardCommand.ExecuteAsync(null);

        // Should not show raw English store-exists as first failure when path is official while link exists.
        // Either progresses (needs steam exit) or Chinese leftover message for beta/official conflict.
        var text = vm.LogText + string.Join('\n', notes);
        text.Should().Contain("归一");
        text.Should().NotContain("Store path already exists.");
        Path.GetFullPath(vm.GamePath).Should().Be(Path.GetFullPath(fx.SteamLink));
    }

    [Fact]
    public void MapArchiveFailureMessage_maps_english_store_exists()
    {
        var mapped = MainViewModel.MapArchiveFailureMessage(
            "Store path already exists.",
            @"D:\steam\steamapps\common\Mechabellum_beta");
        mapped.Should().Contain("Mechabellum_beta");
        mapped.Should().NotContain("Store path already exists");
        mapped.Should().Contain("另一服");
    }

    [Fact]
    public async Task SwitchToBeta_when_aligned_but_game_missing_does_not_claim_already_on_branch()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.Junctions.DeleteJunction(fx.SteamLink);
        fx.Junctions.CreateJunction(fx.SteamLink, fx.BetaStore);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(fx.SteamLink))!, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"public_test"
            	}
            }
            """);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Beta,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test"
        });

        var notes = new List<string>();
        var vm = fx.CreateVm(confirm: _ => true, notify: notes.Add);
        vm.GamePath = Path.Combine(Path.GetTempPath(), "mmm-missing-game-" + Guid.NewGuid().ToString("N"));
        vm.RefreshStatusCommand.Execute(null);

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        var text = vm.LogText + string.Join('\n', notes);
        text.Should().NotContain("无需再次切换");
        text.Should().Contain("尚未就绪");
    }

    [Fact]
    public async Task SwitchToBeta_silent_success_stays_in_settle_until_ConfirmManualBeta()
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
            BetaBranchName = "publicbeta"
        });

        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirm: _ => true, starter: starter);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;
        vm.IsReady.Should().BeTrue();

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        vm.ActiveGameBranch.Should().Be(GameBranch.Beta);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeFalse();
        starter.Starts.Should().NotContain("steam://open/games");
        vm.LogText.Should().Match(s =>
            s.Contains("不会自动启动 Steam", StringComparison.Ordinal)
            || s.Contains("no need to auto-start Steam", StringComparison.OrdinalIgnoreCase)
            || s.Contains("will not auto-start Steam", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Open Steam yourself", StringComparison.OrdinalIgnoreCase)
            || s.Contains("请自行打开 Steam", StringComparison.Ordinal));
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeFalse();

        WriteSettledAcf(fx, betaKey: "publicbeta");
        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeFalse();
        vm.DegradeToManualBeta.Should().BeFalse();
        vm.CanDeployOrLaunch.Should().BeTrue();
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeTrue();
    }

    [Fact]
    public void Load_selects_bound_profile_for_active_branch()
    {
        using var fx = Fixture.CreateReady();
        var extra = fx.Profiles.Create("Beta Build");
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            ActiveBranch = GameBranch.Beta,
            OfficialProfileId = "default",
            BetaProfileId = extra.Id,
            BetaBranchName = "publicbeta"
        });

        var vm = fx.CreateVm();

        vm.BranchSwitchEnabled.Should().BeTrue();
        vm.ActiveGameBranch.Should().Be(GameBranch.Beta);
        vm.SelectedProfile!.Id.Should().Be(extra.Id);
    }

    [Fact]
    public void Restored_AwaitingSteamSettle_does_not_force_DegradeToManualBeta()
    {
        using var fx = Fixture.CreateReady();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.AwaitingSteamSettle,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm();

        vm.IsAwaitingSteamSettle.Should().BeTrue();
        vm.ActiveGameBranch.Should().Be(GameBranch.Official);
        vm.DegradeToManualBeta.Should().BeFalse();
        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.SettleConfirmButtonText.Should().NotContain("测试服");
        vm.BranchStatusText.Should().Contain("正式");
    }

    [Fact]
    public void BrowseGamePath_allowed_during_settle_when_game_not_ready()
    {
        using var fx = Fixture.CreateReady();
        string? browsed = null;
        var vm = fx.CreateVm(browseFolder: () =>
        {
            browsed = fx.GameRoot;
            return fx.GameRoot;
        });
        vm.IsAwaitingSteamSettle = true;
        vm.IsReady.Should().BeTrue();
        vm.GamePath = @"D:\missing\Mechabellum";
        vm.RefreshStatusCommand.Execute(null);
        vm.IsReady.Should().BeFalse();
        vm.IsSessionLocked.Should().BeTrue();

        vm.BrowseGamePathCommand.Execute(null);

        browsed.Should().Be(fx.GameRoot);
        vm.GamePath.Should().Be(fx.GameRoot);
    }


    [Fact]
    public void Busy_disables_CanDeployOrLaunch()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.IsReady.Should().BeTrue();
        vm.CanDeployOrLaunch.Should().BeTrue();

        vm.IsBranchSwitchBusy = true;

        vm.CanDeployOrLaunch.Should().BeFalse();
        vm.ApplyAndLaunchCommand.CanExecute(null).Should().BeFalse();
        vm.ApplyProfileCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Wizard_ends_busy_before_waiting_download_poll()
    {
        using var fx = Fixture.CreateWizardStart();
        var events = new List<string>();
        var vm = fx.CreateVm(
            confirm: msg =>
            {
                events.Add("confirm:" + (msg.Length > 24 ? msg[..24] : msg));
                return !msg.Contains("删除另一", StringComparison.Ordinal);
            },
            promptText: _ => "publicbeta",
            beginBusy: (_, m) => events.Add("begin:" + m),
            setBusyMessage: _ => { },
            endBusy: () => events.Add("end"),
            delay: _ => Task.Delay(30));
        vm.GamePath = fx.SteamLink;
        vm.BetaBranchName = "publicbeta";

        await vm.StartBranchWizardCommand.ExecuteAsync(null);

        vm.BranchWizardStep.Should().Be(BranchWizardStep.WaitingDownloadB);
        vm.IsBranchSwitchBusy.Should().BeFalse();
        events.Should().NotContain(e => e.Contains("点「是」", StringComparison.Ordinal));
        events.Count(e => e == "end").Should().Be(events.Count(e => e.StartsWith("begin:", StringComparison.Ordinal)));
        vm.CancelBusyWork();
    }

    [Fact]
    public async Task Wizard_archive_A_leaves_steam_link_empty_then_auto_continues_when_ready()
    {
        using var fx = Fixture.CreateWizardStart();
        File.WriteAllText(fx.Paths.DeployManifestPath, """{"gamePath":"legacy"}""");
        var vm = fx.CreateVm(
            confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal),
            promptText: _ => "publicbeta",
            delay: _ => Task.Delay(40));
        vm.GamePath = fx.SteamLink;
        vm.BetaBranchName = "publicbeta";

        await vm.StartBranchWizardCommand.ExecuteAsync(null);

        vm.BranchWizardStep.Should().Be(BranchWizardStep.WaitingDownloadB);
        Directory.Exists(fx.SteamLink).Should().BeFalse();
        fx.Junctions.IsJunction(fx.SteamLink).Should().BeFalse();

        Fixture.SeedGameRoot(fx.SteamLink, "downloaded");
        var steamapps = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", ".."));
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"StateFlags"		"4"
            	"buildid"		"200"
            	"TargetBuildID"		"200"
            	"BytesToDownload"		"0"
            	"BytesDownloaded"		"0"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"publicbeta"
            	}
            }
            """);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline
               && vm.BranchWizardStep == BranchWizardStep.WaitingDownloadB)
        {
            await Task.Delay(100);
        }

        vm.CancelBusyWork();
        fx.Junctions.IsJunction(fx.SteamLink).Should().BeTrue();
        fx.Junctions.ResolveTarget(fx.SteamLink).Should().Be(Path.GetFullPath(fx.OfficialStore));
        File.ReadAllText(Path.Combine(fx.OfficialStore, "marker.txt")).Should().Be("current");
        File.ReadAllText(Path.Combine(fx.BetaStore, "marker.txt")).Should().Be("downloaded");
        vm.BranchSwitchEnabled.Should().BeTrue();
        vm.ActiveGameBranch.Should().Be(GameBranch.Official);
        (vm.IsAwaitingSteamSettle || vm.BranchWizardStep == BranchWizardStep.Ready).Should().BeTrue();
        File.ReadAllText(fx.Paths.GetDeployManifestPath(GameBranch.Official, enabled: true))
            .Should().Be("""{"gamePath":"legacy"}""");
    }

    [Fact]
    public async Task Wizard_archive_A_snapshots_current_official_acf()
    {
        using var fx = Fixture.CreateWizardStart();
        WriteSettledAcf(fx, betaKey: null, buildId: "100");
        var vm = fx.CreateVm(
            confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal),
            promptText: _ => "publicbeta",
            delay: _ => Task.Delay(40));
        vm.GamePath = fx.SteamLink;
        vm.BetaBranchName = "publicbeta";

        await vm.StartBranchWizardCommand.ExecuteAsync(null);

        vm.BranchWizardStep.Should().Be(BranchWizardStep.WaitingDownloadB);
        var officialSnap = fx.Paths.GetSteamAcfSnapshotPath(GameBranch.Official);
        File.Exists(officialSnap).Should().BeTrue();
        File.ReadAllText(officialSnap).Should().Contain("\"buildid\"\t\t\"100\"");
        vm.CancelBusyWork();
    }

    [Fact]
    public async Task Wizard_archive_B_restores_official_acf_snapshot_instead_of_beta_key_only()
    {
        using var fx = Fixture.CreateWizardStart();
        WriteSettledAcf(fx, betaKey: null, buildId: "100");
        var vm = fx.CreateVm(
            confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal),
            promptText: _ => "publicbeta",
            delay: _ => Task.Delay(40));
        vm.GamePath = fx.SteamLink;
        vm.BetaBranchName = "publicbeta";

        await vm.StartBranchWizardCommand.ExecuteAsync(null);
        vm.BranchWizardStep.Should().Be(BranchWizardStep.WaitingDownloadB);

        Fixture.SeedGameRoot(fx.SteamLink, "downloaded");
        WriteSettledAcf(fx, betaKey: "publicbeta", buildId: "200");

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline
               && vm.BranchWizardStep == BranchWizardStep.WaitingDownloadB)
        {
            await Task.Delay(100);
        }

        vm.CancelBusyWork();
        vm.BranchSwitchEnabled.Should().BeTrue();
        (vm.IsAwaitingSteamSettle || vm.BranchWizardStep == BranchWizardStep.Ready).Should().BeTrue();

        var liveAcf = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", "..", "appmanifest_669330.acf"));
        var liveText = File.ReadAllText(liveAcf);
        liveText.Should().Contain("\"buildid\"\t\t\"100\"",
            "wizard finish must restore the official ACF snapshot, not leave the beta download buildid");
        liveText.Should().NotContain("\"buildid\"\t\t\"200\"");

        var cfg = fx.LoadBranchConfig();
        cfg.SessionOwnedOfficialStore.Should().BeFalse();
        cfg.SessionOwnedBetaStore.Should().BeFalse();
    }

    [Fact]
    public async Task Wizard_ArchiveB_failure_aborts_and_rolls_back_to_single()
    {
        using var fx = Fixture.CreateWizardStart();
        var notes = new List<string>();
        var vm = fx.CreateVm(
            confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal),
            notify: notes.Add,
            promptText: _ => "publicbeta",
            delay: _ => Task.Delay(40));
        vm.GamePath = fx.SteamLink;
        vm.BetaBranchName = "publicbeta";

        await vm.StartBranchWizardCommand.ExecuteAsync(null);
        vm.BranchWizardStep.Should().Be(BranchWizardStep.WaitingDownloadB);

        // Complete Beta store already present + downloaded real link ⇒ ArchiveB structural fail → full rollback.
        Fixture.SeedGameRoot(fx.BetaStore, "preexisting-beta");
        Fixture.SeedGameRoot(fx.SteamLink, "downloaded");
        var steamapps = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", ".."));
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"StateFlags"		"4"
            	"buildid"		"200"
            	"TargetBuildID"		"200"
            	"BytesToDownload"		"0"
            	"BytesDownloaded"		"0"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"publicbeta"
            	}
            }
            """);

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline
               && vm.BranchWizardStep == BranchWizardStep.WaitingDownloadB)
        {
            await Task.Delay(100);
        }

        vm.CancelBusyWork();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        vm.BranchSwitchEnabled.Should().BeFalse();
        Directory.Exists(fx.OfficialStore).Should().BeFalse();
        File.Exists(Path.Combine(fx.SteamLink, "Mechabellum.exe")).Should().BeTrue();
        notes.Should().Contain(n =>
            n.Contains("回滚", StringComparison.Ordinal)
            || n.Contains("失败", StringComparison.Ordinal)
            || n.Contains("已存在", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Teardown_from_Beta_switches_Official_first_then_tears_down()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.Junctions.DeleteJunction(fx.SteamLink);
        fx.Junctions.CreateJunction(fx.SteamLink, fx.BetaStore);
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Beta,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta"
        });

        var busy = new List<string>();
        var vm = fx.CreateVm(
            confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal),
            beginBusy: (_, msg) => busy.Add(msg));
        vm.GamePath = fx.SteamLink;

        await vm.EmergencyRecoverSingleCommand.ExecuteAsync(null);

        vm.BranchSwitchEnabled.Should().BeFalse();
        fx.Junctions.IsJunction(fx.SteamLink).Should().BeFalse();
        Directory.Exists(fx.SteamLink).Should().BeTrue();
        File.ReadAllText(Path.Combine(fx.SteamLink, "marker.txt")).Should().Be("beta");
    }

    [Fact]
    public async Task Teardown_restores_current_store_and_legacy_manifest()
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
            BetaBranchName = "publicbeta"
        });
        var branchManifest = fx.Paths.GetDeployManifestPath(GameBranch.Official, enabled: true);
        File.WriteAllText(branchManifest, """{"gamePath":"official-branch"}""");

        var vm = fx.CreateVm(confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal));
        vm.GamePath = fx.SteamLink;

        await vm.TeardownBranchSwitchCommand.ExecuteAsync(null);

        vm.BranchSwitchEnabled.Should().BeFalse();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        fx.Junctions.IsJunction(fx.SteamLink).Should().BeFalse();
        Directory.Exists(fx.SteamLink).Should().BeTrue();
        File.ReadAllText(Path.Combine(fx.SteamLink, "marker.txt")).Should().Be("official");
        Directory.Exists(fx.BetaStore).Should().BeTrue();
        File.ReadAllText(fx.Paths.DeployManifestPath).Should().Be("""{"gamePath":"official-branch"}""");
        fx.LoadBranchConfig().Enabled.Should().BeFalse();
        fx.LoadBranchConfig().WizardStep.Should().Be(BranchWizardStep.None);
    }

    [Fact]
    public async Task Teardown_success_clears_recovery_gate()
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
            BetaBranchName = "publicbeta"
        });
        var guard = new CriticalOpGuard(fx.Paths);
        Directory.CreateDirectory(fx.Paths.DataRoot);
        File.WriteAllText(
            fx.Paths.CriticalOpMarkerPath,
            """{"kind":"BranchDiskWrite","startedUtc":"2026-09-05T12:00:00+00:00","detail":"stale","pid":1}""");

        var vm = fx.CreateVm(
            confirm: msg => !msg.Contains("删除另一", StringComparison.Ordinal),
            criticalOp: guard);
        vm.GamePath = fx.SteamLink;
        vm.IsRecoveryGateActive = true;

        await vm.TeardownBranchSwitchCommand.ExecuteAsync(null);

        vm.IsRecoveryGateActive.Should().BeFalse();
        File.Exists(fx.Paths.CriticalOpMarkerPath).Should().BeFalse();
    }

    [Fact]
    public async Task Stuck_Linked_on_load_is_rolled_back_not_resumed()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.Linked,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta",
            SessionOwnedOfficialStore = true,
            SessionOwnedBetaStore = true
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;

        vm.BranchSwitchEnabled.Should().BeFalse();
        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        vm.CanStartBranchWizard.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Teardown_delete_other_confirm_defaults_to_no()
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
            BetaBranchName = "publicbeta"
        });

        MessageBoxResult? deleteDefault = null;
        var vm = fx.CreateVm(confirmChoice: (msg, defaultResult) =>
        {
            if (msg.Contains("删除另一", StringComparison.Ordinal))
            {
                deleteDefault = defaultResult;
                return false;
            }

            return true;
        });
        vm.GamePath = fx.SteamLink;

        await vm.TeardownBranchSwitchCommand.ExecuteAsync(null);

        deleteDefault.Should().Be(MessageBoxResult.No);
        Directory.Exists(fx.BetaStore).Should().BeTrue();
        vm.BranchSwitchEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Teardown_warns_about_leftover_store_when_not_deleted()
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
            BetaBranchName = "publicbeta"
        });

        var notes = new List<string>();
        var vm = fx.CreateVm(confirmChoice: (msg, defaultResult) =>
        {
            if (msg.Contains("删除另一", StringComparison.Ordinal))
                return false;
            return true;
        }, notify: notes.Add);
        vm.GamePath = fx.SteamLink;

        await vm.TeardownBranchSwitchCommand.ExecuteAsync(null);

        Directory.Exists(fx.BetaStore).Should().BeTrue();
        (vm.LogText + string.Join('\n', notes)).Should().Contain("再次启用双服");
    }

    [Fact]
    public async Task Start_does_not_resume_Linked_when_acf_missing()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.Linked,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta",
            SessionOwnedOfficialStore = true
        });
        var acf = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", "..", "appmanifest_669330.acf"));
        File.Delete(acf);

        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirm: _ => true, starter: starter);
        vm.GamePath = fx.SteamLink;

        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        starter.Starts.Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Fact]
    public void Enabled_forces_GamePath_to_SteamLinkPath_before_status_persist()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        var otherGame = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", "..", "..", "other-game"));
        Fixture.SeedGameRoot(otherGame, "other");
        File.WriteAllText(Path.Combine(otherGame, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(otherGame, "GameAssembly.dll"), "");
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = otherGame,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
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
            BetaBranchName = "publicbeta"
        });

        var vm = fx.CreateVm();

        Path.GetFullPath(vm.GamePath).Should().Be(Path.GetFullPath(fx.SteamLink));
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig()).GamePath
            .Should().Be(Path.GetFullPath(fx.SteamLink));
    }

    [Fact]
    public void Wizard_incomplete_on_load_clears_step_and_keeps_steam_link_gamepath()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        fx.Junctions.DeleteJunction(fx.SteamLink);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = @"Z:\not-a-real-mechabellum-" + Guid.NewGuid().ToString("N"),
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = false,
            WizardStep = BranchWizardStep.WaitingDownloadB,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "publicbeta",
            SessionOwnedOfficialStore = true
        });

        var vm = fx.CreateVm();

        fx.LoadBranchConfig().WizardStep.Should().Be(BranchWizardStep.None);
        vm.BranchWizardStep.Should().Be(BranchWizardStep.None);
        vm.CancelBusyWork();
    }

    [Fact]
    public async Task ConfirmManualBeta_succeeds_while_Steam_running_when_ready()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        var steamapps = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", ".."));
        void WriteSettledAcf()
        {
            File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
                """
                "AppState"
                {
                	"appid"		"669330"
                	"StateFlags"		"4"
                	"buildid"		"100"
                	"TargetBuildID"		"100"
                	"BytesToDownload"		"0"
                	"BytesDownloaded"		"0"
                	"UserConfig"
                	{
                		"language"		"english"
                	}
                }
                """);
        }

        WriteSettledAcf();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.AwaitingSteamSettle,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore,
            ActiveBranch = GameBranch.Official,
            OfficialProfileId = "default",
            BetaProfileId = "default",
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm(confirm: _ => true, delay: _ => Task.CompletedTask);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.IsAwaitingSteamSettle = true;
        vm.BranchWizardStep = BranchWizardStep.AwaitingSteamSettle;
        fx.Probe.SteamRunning = true;
        WriteSettledAcf();

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmManualBeta_refuses_deploy_while_game_running_unless_risk_confirmed()
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

        var vm = fx.CreateVm(
            confirm: msg => !(msg.Contains("游戏仍在运行", StringComparison.Ordinal)
                              || msg.Contains("仍要部署", StringComparison.Ordinal)),
            delay: _ => Task.CompletedTask);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();
        fx.Probe.GameRunning = true;

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeTrue();
        File.Exists(fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmManualBeta_succeeds_while_Steam_running_after_switch()
    {
        using var fx = Fixture.CreateReadyDualFolder();
        var steamapps = Path.GetFullPath(Path.Combine(fx.SteamLink, "..", ".."));
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
            BetaBranchName = "public_test"
        });

        var vm = fx.CreateVm(confirm: _ => true, delay: _ => Task.CompletedTask);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);
        vm.Mods[0].IsEnabled = true;

        await vm.SwitchToBetaCommand.ExecuteAsync(null);
        vm.IsAwaitingSteamSettle.Should().BeTrue();

        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"StateFlags"		"4"
            	"buildid"		"100"
            	"TargetBuildID"		"100"
            	"BytesToDownload"		"0"
            	"BytesDownloaded"		"0"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"public_test"
            	}
            }
            """);
        fx.Probe.SteamRunning = true;

        await vm.ConfirmManualBetaCommand.ExecuteAsync(null);

        vm.IsAwaitingSteamSettle.Should().BeFalse();
    }

    [Fact]
    public async Task SwitchToBeta_silent_success_confirm_label_is_not_manual()
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
            BetaBranchName = "publicbeta"
        });

        var vm = fx.CreateVm(confirm: _ => true);
        vm.GamePath = fx.SteamLink;
        vm.RefreshStatusCommand.Execute(null);

        await vm.SwitchToBetaCommand.ExecuteAsync(null);

        vm.DegradeToManualBeta.Should().BeFalse();
        vm.SettleConfirmButtonText.Should().NotContain("已手选");
        vm.SettleConfirmButtonText.Should().Be(LocalizationService.T("BranchSwitchConfirmSettleBeta"));
    }

    [Fact]
    public void AwaitingSteamSettle_keeps_switch_and_emergency_but_blocks_enable()
    {
        using var fx = Fixture.CreateReady();
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

        vm.CanSwitchGameBranch.Should().BeTrue();
        vm.CanStartBranchWizard.Should().BeFalse();
        vm.CanEmergencyRecoverSingle.Should().BeTrue();

        vm.IsAwaitingSteamSettle = true;

        vm.CanSwitchGameBranch.Should().BeTrue("escape to complete other store must stay available");
        vm.CanStartBranchWizard.Should().BeFalse();
        vm.CanEmergencyRecoverSingle.Should().BeTrue();
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
        vm.EmergencyRecoverSingleCommand.Should().NotBeNull();
        vm.ConfirmManualBetaCommand.Should().NotBeNull();
    }

    sealed class RecordingStarter : IProcessStarter
    {
        public List<string> Starts { get; } = new();
        public void StartShell(string uriOrPath) => Starts.Add(uriOrPath);
    }
}
