using System.IO;
using System.Threading;
using System.Windows;
using MechabellumModManager.Dialogs;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Microsoft.Win32;

namespace MechabellumModManager;

public partial class App : Application
{
    BusyProgressDialog? _busyDialog;
    Window? _busyOwner;
    bool _busyOwnerWasEnabled = true;
    bool _blockMainClose;
    Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (InstallConfigSeeder.TryParseArgs(e.Args, out var seedGamePath, out var seedUiLanguage, out var seedRoute, out var seedEdition))
        {
            try
            {
                InstallConfigSeeder.Seed(seedGamePath!, seedUiLanguage, seedRoute, edition: seedEdition);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                try
                {
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "MechabellumModManager");
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(
                        Path.Combine(dir, "seed-config.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n");
                }
                catch { /* ignore */ }

                Shutdown(1);
            }

            return;
        }

        if (EnsureRedistCli.TryParseArgs(
                e.Args,
                out var ensureRedistDir,
                out var ensureMirror,
                out var ensureIds,
                out var ensureNoMirror,
                out var ensureProgressFile,
                out var ensureRouteMirror))
        {
            var code = EnsureRedistCli.Run(
                ensureRedistDir!,
                ensureMirror,
                ensureIds,
                ensureNoMirror,
                ensureProgressFile,
                ensureRouteMirror);
            Shutdown(code);
            return;
        }

        if (InstallMelonLoaderCli.TryParseArgs(e.Args, out var melonGamePath, out var melonRedistDir))
        {
            var code = InstallMelonLoaderCli.Run(melonGamePath!, melonRedistDir);
            Shutdown(code);
            return;
        }

        if (SanitizeAcfSnapshotsCli.TryParseArgs(e.Args, out var snapshotsDir, out var betaBranchName))
        {
            var code = SanitizeAcfSnapshotsCli.Run(snapshotsDir, betaBranchName);
            Shutdown(code);
            return;
        }

        if (PureGameCleanupCli.TryParseArgs(
                e.Args,
                out var cleanupGamePath,
                out var cleanupDryRun,
                out var cleanupConfirmOther,
                out var cleanupSkipAppData,
                out var cleanupSkipMelon))
        {
            var code = PureGameCleanupCli.Run(
                cleanupGamePath,
                cleanupDryRun,
                cleanupConfirmOther,
                cleanupSkipAppData,
                cleanupSkipMelon);
            Shutdown(code);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MechabellumModManager");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {args.Exception}\n");
            }
            catch { /* ignore */ }

            // Win32Exception 1816 (quota) during window updates should not hard-kill the process.
            if (args.Exception is System.ComponentModel.Win32Exception { NativeErrorCode: 1816 })
            {
                args.Handled = true;
                return;
            }
        };

        // UI single-instance only (CLI helpers above skip this).
        LocalizationService.Apply(LocalizationService.ResolveSystemLanguage(StartupLanguageFallback()));
        if (!SingleInstanceGate.TryAcquire(out _singleInstanceMutex))
        {
            MessageBox.Show(
                LocalizationService.T("NotifyManagerAlreadyRunning"),
                "Mechabellum Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        SteamOverlayLatch.EnsureDisabledWhileSteamIsClosed();

        UiScaleHost.EnsureHooked();
        var window = new MainWindow();
        window.Closing += (_, e) =>
        {
            if (window.DataContext is MainViewModel vm)
            {
                if (vm.TryHandleWindowClosing(_blockMainClose || _busyDialog is not null, out var cancel) && cancel)
                    e.Cancel = true;
                else if (_blockMainClose)
                    e.Cancel = true;
            }
            else if (_blockMainClose)
                e.Cancel = true;
        };
        var vm = ComposeMainViewModel(window, out var criticalOp);
        window.DataContext = vm;
        MainWindow = window;
        window.Show();
        OfferCriticalOpRecovery(window, vm, criticalOp);
        _ = vm.BeginStartupChecksAsync();
    }

    /// <summary>Fallback language of the saved edition, for messages shown before the view model exists.</summary>
    static string StartupLanguageFallback()
    {
        try
        {
            var config = new JsonStore().LoadOrDefault(new PathsService().ConfigPath, () => new AppConfig());
            if (EditionPolicy.TryParse(config.Edition, out var edition))
                return EditionPolicy.LanguageFallback(edition);
        }
        catch
        {
            // Unreadable config: the view model reports it later.
        }

        return LocalizationService.DefaultFallback;
    }

    static void OfferCriticalOpRecovery(Window owner, MainViewModel vm, CriticalOpGuard criticalOp)
    {
        if (!vm.ShouldOfferCriticalRecovery(criticalOp))
            return;

        vm.IsRecoveryGateActive = true;
        var dialog = new CriticalOpRecoveryDialog(() =>
        {
            if (vm.ExportDiagnosticsCommand.CanExecute(null))
                vm.ExportDiagnosticsCommand.Execute(null);
        })
        {
            Owner = owner
        };
        if (dialog.ShowDialog() != true)
            return;

        if (dialog.Choice == CriticalOpRecoveryChoice.Repair)
            _ = vm.ApplyRecoveryRepair();
        else if (dialog.Choice == CriticalOpRecoveryChoice.Abandon)
            vm.ApplyRecoveryAbandon();
        else
            vm.ApplyRecoveryContinue();
    }

    void BeginBusy(Window owner, string title, string message)
    {
        if (_busyDialog is not null)
        {
            _busyDialog.Title = title;
            _busyDialog.SetMessage(message);
            return;
        }

        _busyOwner = owner;
        _busyOwnerWasEnabled = owner.IsEnabled;
        owner.IsEnabled = false;
        _blockMainClose = true;

        _busyDialog = new BusyProgressDialog { Owner = owner };
        _busyDialog.Title = title;
        _busyDialog.SetMessage(message);
        _busyDialog.Show();
    }

    void SetBusyMessage(string message) => _busyDialog?.SetMessage(message);

    void EndBusy()
    {
        _blockMainClose = false;
        if (_busyDialog is not null)
        {
            _busyDialog.ForceClose();
            _busyDialog = null;
        }
        if (_busyOwner is not null)
        {
            _busyOwner.IsEnabled = _busyOwnerWasEnabled;
            _busyOwner = null;
        }
    }

    MainViewModel ComposeMainViewModel(MainWindow window, out CriticalOpGuard criticalOp)
    {
        var store = new JsonStore();
        var paths = ResolvePaths(store);
        var detector = new GameDetector();
        var profiles = new ProfileService(paths, store);
        var inspector = new AssemblyInspector();
        var library = new ModLibraryService(paths, inspector, store, profiles);
        var planner = new DeployPlanner();
        var probe = new ProcessProbe();
        var deploy = new DeployService(paths, store, planner, detector, probe);
        var starter = new ShellProcessStarter();
        var launcher = new GameLauncher(starter, probe);
        var riskGate = new RiskGate();
        var junctions = new JunctionService();
        var betaEditor = new SteamBetaKeyEditor(probe);
        var branchSwitch = new BranchSwitchService(paths, store, probe, junctions, betaEditor);
        criticalOp = new CriticalOpGuard(paths);
        var setupDownloader = new ManagerSetupDownloader();

        return new MainViewModel(
            paths,
            store,
            detector,
            library,
            profiles,
            deploy,
            launcher,
            riskGate,
            assemblyInspector: inspector,
            confirmHighRisk: msg => Confirm(window, msg, LocalizationService.T("Confirm"), MessageBoxImage.Warning),
            confirm: msg => Confirm(window, msg, LocalizationService.T("Confirm"), MessageBoxImage.Question),
            confirmChoice: (msg, defaultResult) => Confirm(window, msg, LocalizationService.T("Confirm"), MessageBoxImage.Question, defaultResult),
            promptConfirmOption: (msg, option) => PromptConfirmOption(window, msg, option),
            notify: msg => Notify(window, msg),
            browseFolder: () => BrowseFolder(window),
            openDll: () => OpenFile(window, LocalizationService.T("FileFilterDll")),
            openZip: () => OpenFile(window, LocalizationService.T("FileFilterZip")),
            promptText: title => Prompt(window, title),
            pickPackageType: () => PickPackageType(window),
            pickCurrentBranch: () => PickCurrentBranch(window),
            openFolder: () => BrowseImportFolder(window),
            promptReport: modName => PromptReport(window, modName),
            promptSubmitGuide: () => PromptSubmitGuide(window),
            promptDirectUpload: () => PromptDirectUpload(window, paths, store),
            promptEditTaxonomy: pkg => PromptEditTaxonomy(window, pkg),
            copyText: text => Clipboard.SetText(text),
            unselectLibrary: window.UnselectLibraryMods,
            unselectCatalog: window.UnselectCatalogMods,
            branchSwitch: branchSwitch,
            processProbe: probe,
            processStarter: starter,
            promptExportDiagnostics: () => PromptExportDiagnostics(window),
            promptMailProvider: () => MailProviderDialog.Prompt(window, EditionOf(window)),
            promptDiagnosticsMail: () =>
            {
                var edition = EditionOf(window);
                var hint = edition == AppEdition.Cn
                    ? string.Format(LocalizationService.T("DiagnosticsMailHintCn"), GitHubCommunityLinks.QqGroupNumber)
                    : LocalizationService.T("DiagnosticsMailHint");
                return MailProviderDialog.PromptDiagnostics(window, hint, edition);
            },
            saveZipFile: suggestedName => SaveZipFile(window, suggestedName),
            revealInExplorer: RevealInExplorer,
            beginBusy: (title, msg) => BeginBusy(window, title, msg),
            setBusyMessage: SetBusyMessage,
            endBusy: EndBusy,
            criticalOp: criticalOp,
            setupDownloader: setupDownloader,
            promptManagerUpdate: (prompt, ct) =>
            {
                _ = ct;
                var dialog = new ManagerUpdateDialog(prompt, setupDownloader) { Owner = window };
                dialog.ShowDialog();
                return Task.FromResult(dialog.Result);
            },
            requestProcessExit: () =>
            {
                EndBusy();
                try { Current.Shutdown(); } catch { /* ignore */ }
            });
    }

    static PathsService ResolvePaths(JsonStore store)
    {
        var appData = new PathsService();
        var configExisted = File.Exists(appData.ConfigPath);
        var config = store.LoadOrDefault(appData.ConfigPath, () => new AppConfig());

        InstallDefaults? seed = null;
        if (File.Exists(PathsService.InstallDefaultsPath))
            seed = store.LoadOrDefault(PathsService.InstallDefaultsPath, () => new InstallDefaults());

        if (InstallDefaultsMerger.TryMerge(config, seed, configExisted))
        {
            appData.EnsureCreated();
            store.Save(appData.ConfigPath, config);
        }

        if (!string.IsNullOrWhiteSpace(config.DataRoot))
            return new PathsService(config.DataRoot);
        return appData;
    }

    static bool Confirm(Window owner, string message, string title, MessageBoxImage icon, MessageBoxResult defaultResult = MessageBoxResult.Yes)
    {
        _ = icon;
        var dialog = new ConfirmDialog(
            message,
            title,
            yesNo: true,
            defaultYes: defaultResult != MessageBoxResult.No)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }

    static (bool ok, bool option)? PromptConfirmOption(Window owner, string message, string? optionLabel)
    {
        var dialog = new ConfirmDialog(
            message,
            LocalizationService.T("Confirm"),
            yesNo: true,
            defaultYes: false,
            optionLabel: optionLabel)
        {
            Owner = owner
        };
        if (dialog.ShowDialog() != true)
            return null;
        return (true, dialog.IsOptionChecked);
    }

    static void Notify(Window owner, string message)
    {
        var dialog = new ConfirmDialog(message, LocalizationService.T("Notice"), yesNo: false)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    static string? BrowseFolder(Window owner)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T("PickGameFolderTitle")
        };
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

    static string? BrowseImportFolder(Window owner)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T("PickImportFolderTitle")
        };
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

    static string? OpenFile(Window owner, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    static string? Prompt(Window owner, string title)
    {
        var dialog = new PromptDialog(title)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    static ModPackageType? PickPackageType(Window owner)
    {
        var dialog = new TypePickDialog
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    static GameBranch? PickCurrentBranch(Window owner)
    {
        var dialog = new BranchPickDialog
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    static (ReportCategory Category, string Notes)? PromptReport(Window owner, string modName)
    {
        var dialog = new ReportModDialog(modName) { Owner = owner };
        if (dialog.ShowDialog() != true)
            return null;
        return (dialog.Category, dialog.Notes);
    }

    /// <summary>Edition at the moment a dialog opens; Settings can switch it while the app runs.</summary>
    static AppEdition EditionOf(Window window) =>
        (window.DataContext as MainViewModel)?.Edition ?? AppEdition.Cn;

    static bool PromptSubmitGuide(Window owner)
    {
        var vm = owner.DataContext as MainViewModel;
        var dialog = new SubmitGuideDialog(
            vm?.Edition ?? AppEdition.Cn,
            vm?.ContributeGuideUrl ?? GitHubCommunityLinks.ContributeGuideUrl)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }

    static void PromptDirectUpload(Window owner, PathsService paths, JsonStore store)
    {
        var config = store.LoadOrDefault(paths.ConfigPath, () => new AppConfig());
        var ui = (owner.DataContext as MainViewModel)?.Ui ?? new UiStrings();
        var dialog = new DirectUploadDialog(
            config,
            c =>
            {
                paths.EnsureCreated();
                store.Save(paths.ConfigPath, c);
            },
            ui)
        {
            Owner = owner
        };
        dialog.ShowDialog();
    }

    static (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)? PromptEditTaxonomy(Window owner, ModPackage package)
    {
        var ui = (owner.DataContext as MainViewModel)?.Ui ?? new UiStrings();
        var dialog = new EditModTaxonomyDialog(package, ui) { Owner = owner };
        if (dialog.ShowDialog() != true)
            return null;
        return dialog.Result;
    }

    static DiagnosticsRedactionMode? PromptExportDiagnostics(Window owner)
    {
        var dialog = new ExportDiagnosticsDialog { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.RedactionMode : null;
    }

    static string? SaveZipFile(Window owner, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = LocalizationService.T("ExportDiagnosticsTitle"),
            Filter = "Zip (*.zip)|*.zip",
            FileName = suggestedFileName,
            AddExtension = true,
            DefaultExt = ".zip",
            OverwritePrompt = true
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    static void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + path + "\"",
                    UseShellExecute = true
                });
                return;
            }

            var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch
        {
            // Best-effort; export itself already succeeded.
        }
    }
}
