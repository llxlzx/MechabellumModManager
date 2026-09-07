using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using MechabellumModManager.Tests.Support;
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
        var vm = fx.CreateVm(confirmHighRisk: null, preserveNullConfirmHighRisk: true);

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
}
