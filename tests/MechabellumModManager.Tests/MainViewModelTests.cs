using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using MechabellumModManager.Tests.Support;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

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
    public async Task ApplyAndLaunch_skips_steam_when_registry_still_marks_game_running()
    {
        using var fx = Fixture.CreateReady();
        string? notified = null;
        var starter = new RecordingStarter();
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            starter: starter,
            notify: m => notified = m,
            steamAppMarkedRunning: () => true);
        vm.LaunchMode = LaunchMode.SteamThenExe;

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        starter.Starts.Should().BeEmpty();
        notified.Should().Be(LocalizationService.T("NotifyLaunchSteamStaleRunning"));
        vm.LogText.Should().Contain(LocalizationService.T("NotifyLaunchSteamStaleRunning"));
        vm.LogText.Should().NotContain(LocalizationService.T("NotifyLaunchProcessNotSeen"));
    }

    [Fact]
    public async Task ApplyAndLaunch_reports_process_not_seen_when_probe_stays_false()
    {
        using var fx = Fixture.CreateReady();
        string? notified = null;
        var starter = new RecordingStarter();
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            starter: starter,
            notify: m => notified = m);

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        starter.Starts.Should().NotBeEmpty();
        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchRequestedVerifying"));
        notified.Should().Be(LocalizationService.T("NotifyLaunchProcessNotSeen"));
        vm.GameStatus!.Kind.Should().Be(GameStatusKind.Ready);
    }

    [Fact]
    public async Task Exe_launch_confirm_stays_fifteen_polls()
    {
        using var fx = Fixture.CreateReady();
        var delays = 0;
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            delay: _ =>
            {
                delays++;
                return Task.CompletedTask;
            });

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        delays.Should().Be(MainViewModel.ExeLaunchConfirmPolls);
        vm.LogText.Should().Contain(LocalizationService.T("NotifyLaunchProcessNotSeen"));
    }

    [Fact]
    public async Task Steam_launch_confirm_waits_ninety_polls()
    {
        using var fx = Fixture.CreateReady();
        var delays = 0;
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            delay: _ =>
            {
                delays++;
                return Task.CompletedTask;
            });
        vm.LaunchMode = LaunchMode.SteamThenExe;

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        delays.Should().Be(MainViewModel.SteamLaunchConfirmPolls);
        MainViewModel.LaunchConfirmPolls(LaunchMode.SteamOnly).Should().Be(MainViewModel.SteamLaunchConfirmPolls);
        MainViewModel.DefaultLaunchFollowUpPolls.Should().Be(120);
    }

    [Fact]
    public async Task Steam_follow_up_logs_when_process_appears_after_timeout()
    {
        using var fx = Fixture.CreateReady();
        string? notified = null;
        var delays = 0;
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            notify: m => notified = m,
            delay: _ =>
            {
                delays++;
                if (delays >= 4)
                    fx.Probe.GameRunning = true;
                return Task.CompletedTask;
            });
        vm.LaunchMode = LaunchMode.SteamThenExe;
        vm.LaunchConfirmPollsOverride = 2;
        vm.LaunchFollowUpPolls = 4;

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);
        await vm.LaunchFollowUp;

        notified.Should().NotBe(LocalizationService.T("NotifyLaunchProcessNotSeen"));
        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchStillWaiting"));
        vm.LogText.Should().NotContain(LocalizationService.T("NotifyLaunchProcessNotSeen"));
        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchProcessSeenLate"));
        vm.LogText.Should().NotContain(LocalizationService.T("LogLaunchProcessSeen"));
        vm.IsAwaitingGameProcess.Should().BeFalse();
    }

    [Fact]
    public async Task Steam_follow_up_notifies_only_after_the_extra_wait_expires()
    {
        using var fx = Fixture.CreateReady();
        string? notified = null;
        MainViewModel vm = null!;
        vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            notify: m => notified = m,
            delay: _ =>
            {
                vm.IsAwaitingGameProcess.Should().BeTrue();
                vm.StatusKindLabel.Should().Be("正在等待游戏进程");
                vm.ShowReadyAccent.Should().BeFalse();
                return Task.CompletedTask;
            });
        vm.LaunchMode = LaunchMode.SteamThenExe;
        vm.LaunchConfirmPollsOverride = 1;
        vm.LaunchFollowUpPolls = 1;

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);
        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchStillWaiting"));

        await vm.LaunchFollowUp;

        notified.Should().Be(LocalizationService.T("NotifyLaunchProcessNotSeen"));
        vm.IsAwaitingGameProcess.Should().BeFalse();
        vm.StatusKindLabel.Should().NotBe("正在等待游戏进程");
    }

    [Fact]
    public async Task Late_process_waits_for_recheck_before_calling_injection_failed()
    {
        using var fx = Fixture.CreateReady();
        var logPath = Path.Combine(fx.GameRoot, "MelonLoader", "Latest.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllText(logPath, "[14:17:52.025] MelonLoader v0.7.3 Open-Beta\n");
        File.SetLastWriteTime(logPath, DateTime.Now.AddDays(-8));

        var delays = 0;
        MainViewModel vm = null!;
        vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            delay: _ =>
            {
                delays++;
                if (delays == 2)
                {
                    fx.Probe.GameRunning = true;
                    // The 90s confirm is already past the 20s settle window in production.
                    // A fast test must backdate the stamp or Detect still treats injection as unknown.
                    var cfg = fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig());
                    cfg.LastLaunchRequestedAt = DateTimeOffset.Now.AddMinutes(-5);
                    cfg.LastApplyStartedAt = cfg.LastLaunchRequestedAt.Value.AddSeconds(-1);
                    fx.Store.Save(fx.Paths.ConfigPath, cfg);
                }
                if (delays == 3)
                {
                    vm.GameStatus!.LoaderInjected.Should().NotBe(false);
                    File.SetLastWriteTime(logPath, DateTime.Now.AddMinutes(1));
                }

                return Task.CompletedTask;
            });
        vm.LaunchMode = LaunchMode.SteamThenExe;
        vm.LaunchConfirmPollsOverride = 1;
        vm.LaunchFollowUpPolls = 2;

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);
        await vm.LaunchFollowUp;

        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchProcessSeenLate"));
        vm.GameStatus!.LoaderInjected.Should().NotBe(false);
        vm.LogText.Should().NotContain("未在本次启动注入");
    }

    [Fact]
    public async Task ApplyAndLaunch_stamps_apply_started_before_launch()
    {
        using var fx = Fixture.CreateReady();
        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirmHighRisk: _ => true, starter: starter);

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        var cfg = fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig());
        cfg.LastApplyStartedAt.Should().NotBeNull();
        cfg.LastLaunchRequestedAt.Should().NotBeNull();
        cfg.LastApplyStartedAt!.Value.Should().BeOnOrBefore(cfg.LastLaunchRequestedAt!.Value);
    }

    [Fact]
    public async Task ApplyAndLaunch_logs_process_seen_then_rechecks_injection()
    {
        using var fx = Fixture.CreateReady();
        var delays = 0;
        var starter = new RecordingStarter();
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            starter: starter,
            delay: _ =>
            {
                delays++;
                if (delays >= 2)
                    fx.Probe.GameRunning = true;
                return Task.CompletedTask;
            });

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        starter.Starts.Should().NotBeEmpty();
        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchProcessSeen"));
        vm.LogText.Should().NotContain(LocalizationService.T("NotifyLaunchProcessNotSeen"));
        vm.LogText.Should().NotContain(LocalizationService.T("NotifyLaunchGamePathMismatch").Split('{')[0]);
    }

    [Fact]
    public async Task ApplyAndLaunch_notifies_when_running_exe_is_other_library()
    {
        using var fx = Fixture.CreateReady();
        fx.Probe.RunningGameExePath = @"D:\steam\steamapps\common\Mechabellum\Mechabellum.exe";
        string? notified = null;
        var delays = 0;
        var starter = new RecordingStarter();
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            starter: starter,
            notify: m => notified = m,
            delay: _ =>
            {
                delays++;
                if (delays >= 2)
                    fx.Probe.GameRunning = true;
                return Task.CompletedTask;
            });

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        vm.LogText.Should().Contain("D:\\steam\\steamapps\\common\\Mechabellum\\Mechabellum.exe");
        notified.Should().Contain("仅直启");
        notified.Should().Contain("D:\\steam\\steamapps\\common\\Mechabellum\\Mechabellum.exe");
    }

    [Fact]
    public void ApplyProfile_empty_profile_notifies_no_enabled_mods()
    {
        using var fx = Fixture.CreateReady();
        var notes = new List<string>();
        var vm = fx.CreateVm(confirmHighRisk: _ => true, notify: notes.Add);

        vm.ApplyProfile();

        vm.LogText.Should().Contain("没有启用任何 Mod");
        notes.Should().Contain(n => n.Contains("没有启用任何 Mod", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyAndLaunch_after_assembly_gen_uses_exe_without_changing_launch_mode()
    {
        using var fx = Fixture.CreateReady();
        var asm = Path.Combine(fx.GameRoot, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll");
        File.Delete(asm);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.SteamThenExe
        });

        var starter = new RecordingStarter();
        var gen = new MelonLoaderAssemblyGenerator(
            startProcess: exe =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(asm)!);
                File.WriteAllText(asm, "asm");
                return null;
            },
            delay: (_, _) => Task.CompletedTask);
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            starter: starter,
            melonAssemblyGenerator: gen);
        vm.LaunchMode = LaunchMode.SteamThenExe;

        await vm.ApplyAndLaunchCommand.ExecuteAsync(null);

        starter.Starts.Should().ContainSingle();
        starter.Starts[0].Should().Be(Path.Combine(fx.GameRoot, "Mechabellum.exe"));
        vm.LaunchMode.Should().Be(LaunchMode.SteamThenExe);
        fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig())
            .LaunchMode.Should().Be(LaunchMode.SteamThenExe);
        vm.LogText.Should().Contain(LocalizationService.T("LogLaunchExeAfterAssemblyGen"));
    }

    [Fact]
    public async Task Recheck_when_still_not_injected_hints_exe_only_without_second_start()
    {
        using var fx = Fixture.CreateReady();
        var logPath = Path.Combine(fx.GameRoot, "MelonLoader", "Latest.log");
        Directory.CreateDirectory(Path.Combine(fx.GameRoot, "MelonLoader"));
        File.WriteAllText(logPath, "[14:17:52.025] MelonLoader v0.7.3 Open-Beta\n");
        File.SetLastWriteTime(logPath, DateTime.Now.AddDays(-8));
        fx.Probe.GameRunning = true;
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.SteamThenExe,
            LastLaunchRequestedAt = DateTimeOffset.Now.AddMinutes(-5)
        });

        var starter = new RecordingStarter();
        var vm = fx.CreateVm(confirmHighRisk: _ => true, starter: starter);
        vm.GameStatus!.LoaderInjected.Should().BeFalse();

        await vm.RecheckLoaderInjectionAfterLaunchAsync();

        vm.LogText.Should().Contain("仅直启");
        starter.Starts.Should().BeEmpty();
    }

    [Fact]
    public void PureGameCleanup_asks_once_then_cancel_does_not_run()
    {
        using var fx = Fixture.CreateReady();
        var calls = 0;
        var vm = fx.CreateVm(
            promptConfirmOption: (msg, opt) =>
            {
                calls++;
                msg.Should().Contain("•");
                opt.Should().BeNull();
                return null;
            });

        vm.PureGameCleanupCommand.Execute(null);

        calls.Should().Be(1);
        vm.LogText.Should().NotContain(LocalizationService.T("NotifyPureGameCleanupDone"));
    }

    [Fact]
    public void After_launch_stale_latest_log_drops_ready_accent_but_keeps_deploy()
    {
        using var fx = Fixture.CreateReady();
        var logPath = Path.Combine(fx.GameRoot, "MelonLoader", "Latest.log");
        Directory.CreateDirectory(Path.Combine(fx.GameRoot, "MelonLoader"));
        File.WriteAllText(logPath, "[14:17:52.025] MelonLoader v0.7.1 Open-Beta\n");
        File.SetLastWriteTime(logPath, DateTime.Now.AddDays(-8));
        fx.Probe.GameRunning = true;
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly,
            LastLaunchRequestedAt = DateTimeOffset.Now.AddMinutes(-5)
        });

        var vm = fx.CreateVm(confirmHighRisk: _ => true);

        vm.IsReady.Should().BeTrue();
        vm.ShowReadyAccent.Should().BeFalse();
        vm.StatusKindLabel.Should().Be("未注入");
        vm.GameStatus!.Message.Should().Be("Loader 未在本次启动注入");
        vm.CanDeployOrLaunch.Should().BeTrue();
        vm.GameStatus.MelonLoaderVersion.Should().Be("0.7.1");
    }

    [Fact]
    public void Detect_not_injected_records_loader_not_injected_once()
    {
        using var fx = Fixture.CreateReady();
        var logPath = Path.Combine(fx.GameRoot, "MelonLoader", "Latest.log");
        Directory.CreateDirectory(Path.Combine(fx.GameRoot, "MelonLoader"));
        File.WriteAllText(logPath, "[14:17:52.025] MelonLoader v0.7.1 Open-Beta\n");
        File.SetLastWriteTime(logPath, DateTime.Now.AddDays(-8));
        fx.Probe.GameRunning = true;
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = fx.GameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly,
            LastLaunchRequestedAt = DateTimeOffset.Now.AddMinutes(-5)
        });

        var vm = fx.CreateVm(confirmHighRisk: _ => true);
        vm.GameStatus!.LoaderInjected.Should().BeFalse();

        var eventsPath = Path.Combine(fx.Paths.LogsDir, ManagerEventLog.FileName);
        File.Exists(eventsPath).Should().BeTrue();
        var first = File.ReadAllText(eventsPath);
        first.Should().Contain("loader_not_injected");

        vm.RefreshStatusCommand.Execute(null);
        File.ReadAllText(eventsPath).Should().Be(first);
    }

    [Fact]
    public void HighRisk_mark_does_not_block_enable_while_detection_is_paused()
    {
        using var fx = Fixture.CreateReady(highRisk: true);
        var vm = fx.CreateVm(confirmHighRisk: _ => false);

        vm.Mods[0].IsEnabled = true;

        vm.Mods[0].IsEnabled.Should().BeTrue();
        vm.Mods[0].HighRisk.Should().BeFalse();
        vm.LogText.Should().NotContain("已取消启用高风险");
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
        var item = new CatalogModItemViewModel(mod, CatalogEntryState.NotInstalled);

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
            CatalogEntryState.UpToDate);

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
            CatalogEntryState.UpToDate);
        var fresh = new CatalogModItemViewModel(
            new CatalogMod { Id = "new-mod", Name = "New", File = "mods/new/New.dll" },
            CatalogEntryState.NotInstalled);

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
            CatalogEntryState.NotInstalled);

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

    [Fact]
    public void LogicFrameGradeLabel_unknown_string_is_low_and_refresh_raises_it()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm(confirmHighRisk: _ => true);
        var item = vm.Mods.Single(m => m.Package.Id == "cam-aaaaaaaa");

        item.Package.LogicFrameGrade = nameof(LogicFrameGrade.Medium);
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameMedium"));
        item.Package.LogicFrameGrade = nameof(LogicFrameGrade.High);
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameHigh"));
        item.Package.LogicFrameGrade = nameof(LogicFrameGrade.Unchecked);
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameUnchecked"));
        item.Package.LogicFrameGrade = nameof(LogicFrameGrade.Low);
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameLow"));
        item.Package.LogicFrameGrade = "not-a-grade";
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameLow"));

        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        item.NotifyDetailChanged();
        item.NotifyRiskChanged();
        raised.Count(name => name == nameof(ModItemViewModel.LogicFrameGradeLabel)).Should().Be(2);
    }

    [Fact]
    public void OnModEnabledChanged_low_package_does_not_confirm_and_profile_contains_id()
    {
        using var fx = Fixture.CreateReady();
        var dll = Path.Combine(fx.Paths.LibraryRoot, "mods", "cam-aaaaaaaa", "Cam.dll");
        File.Delete(dll);

        var confirms = 0;
        var vm = fx.CreateVm(confirmHighRisk: _ =>
        {
            confirms++;
            return false;
        });
        confirms.Should().Be(0);

        var item = vm.Mods.Single(m => m.Package.Id == "cam-aaaaaaaa");
        item.Package.LogicFrameGrade = nameof(LogicFrameGrade.High);
        item.Package.LogicFrameReason = "sim-replace";

        vm.OnModEnabledChanged(item, true);

        confirms.Should().Be(0);
        fx.Profiles.Get("default").EnabledPackageIds.Should().Contain("cam-aaaaaaaa");
        item.Package.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.Low));
        item.Package.LogicFrameReason.Should().Be("none");
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameLow"));
    }

    [Fact]
    public void Checkbox_enable_medium_void_ReduceLife_prefix_cancel_stays_off()
    {
        using var fx = Fixture.CreateReady();
        var dll = Path.Combine(fx.Paths.LibraryRoot, "mods", "cam-aaaaaaaa", "Cam.dll");
        WriteVoidReduceLifePrefix(dll);

        var confirms = 0;
        string? seen = null;
        var vm = fx.CreateVm(confirmHighRisk: message =>
        {
            confirms++;
            seen = message;
            return false;
        });
        confirms.Should().Be(0);

        var item = vm.Mods.Single(m => m.Package.Id == "cam-aaaaaaaa");
        item.Package.LogicFrameGrade = nameof(LogicFrameGrade.Low);
        item.Package.LogicFrameReason = "none";

        var raisedLabel = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModItemViewModel.LogicFrameGradeLabel))
                raisedLabel = true;
        };

        item.IsEnabled = true;

        confirms.Should().Be(1);
        seen.Should().Be(LocalizationService.T("ConfirmLogicFrameMedium").Replace("{0}", item.DisplayName));
        fx.Profiles.Get("default").EnabledPackageIds.Should().NotContain("cam-aaaaaaaa");
        item.IsEnabled.Should().BeFalse();
        item.Package.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.Medium));
        item.Package.LogicFrameReason.Should().Be("sim-observe");
        raisedLabel.Should().BeTrue();
        item.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameMedium"));

        var disableLine = string.Format(
            LocalizationService.T("LogEnableDisableAction"),
            LocalizationService.T("LogActionDisable"),
            item.DisplayName);
        vm.LogText.Should().Contain(LocalizationService.T("LogCancelledEnableHighRisk"));
        vm.LogText.Should().NotContain(disableLine);
    }

    [Fact]
    public void Rechecking_a_missing_row_stays_unchecked_and_does_not_write_package_json()
    {
        using var fx = Fixture.CreateReady();
        fx.Profiles.SetEnabled("default", "gone-missing01", true);
        var cwdJson = Path.Combine(Directory.GetCurrentDirectory(), "package.json");
        var existed = File.Exists(cwdJson);
        var confirms = 0;
        try
        {
            var vm = fx.CreateVm(confirmHighRisk: _ =>
            {
                confirms++;
                return false;
            });
            var missing = vm.Mods.Should().ContainSingle(m => m.IsMissing && m.Package.Id == "gone-missing01").Subject;
            missing.LogicFrameGradeLabel.Should().Be(LocalizationService.T("LogicFrameUnchecked"));
            missing.SetEnabledSilent(false);
            missing.IsEnabled = true;

            confirms.Should().Be(0);
            missing.IsEnabled.Should().BeTrue();
            missing.Package.LogicFrameGrade.Should().Be(nameof(LogicFrameGrade.Unchecked));
            missing.Package.LogicFrameReason.Should().Be("unreadable");
            fx.Profiles.Get("default").EnabledPackageIds.Should().Contain("gone-missing01");
            File.Exists(cwdJson).Should().Be(existed);
        }
        finally
        {
            if (!existed && File.Exists(cwdJson))
                File.Delete(cwdJson);
        }
    }

    static void WriteVoidReduceLifePrefix(string path)
    {
        using var module = ModuleDefinition.CreateModule("LogicFrameFixture", ModuleKind.Dll);
        var target = new TypeDefinition(
            "Sim",
            "FightActor",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        var gameMethod = new MethodDefinition("ReduceLife", MethodAttributes.Public, module.TypeSystem.Void);
        gameMethod.Body.GetILProcessor().Emit(OpCodes.Ret);
        target.Methods.Add(gameMethod);
        module.Types.Add(target);

        var hook = new MethodDefinition(
            "Prefix",
            MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        hook.Body.GetILProcessor().Emit(OpCodes.Ret);

        var attr = new TypeDefinition(
            "",
            "HarmonyPatchAttribute",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        ctor.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Type))));
        ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        ctor.Body.GetILProcessor().Emit(OpCodes.Ret);
        attr.Methods.Add(ctor);
        module.Types.Add(attr);

        var attribute = new CustomAttribute(ctor);
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference(typeof(Type)), target));
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "ReduceLife"));
        hook.CustomAttributes.Add(attribute);

        var outer = new TypeDefinition("", "Mod", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        outer.Methods.Add(hook);
        module.Types.Add(outer);
        module.Write(path);
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
}
