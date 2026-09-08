using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    static readonly JsonSerializerOptions PackageJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    readonly PathsService _paths;
    readonly JsonStore _store;
    readonly GameDetector _detector;
    readonly ModLibraryService _library;
    readonly ProfileService _profiles;
    readonly DeployService _deploy;
    readonly GameLauncher _launcher;
    readonly RiskGate _riskGate;
    readonly MelonLoaderConfigOptimizer _melonOptimizer;
    readonly MelonLoaderDualStoreSync _melonDualSync;
    readonly MelonLoaderAssemblyGenerator _melonAssemblyGenerator;
    readonly RiskHeuristic _riskHeuristic;
    readonly SteamGameLocator _steamLocator;
    readonly UpdateChecker _updateChecker;
    readonly ModCatalogService _catalog;
    readonly AssemblyInspector _assemblyInspector;
    readonly ManagerLogWriter _managerLog;
    readonly ManagerEventLog _events;
    readonly Func<string, bool> _confirmHighRisk;
    readonly Func<string, bool> _confirm;
    readonly Func<string, MessageBoxResult, bool>? _confirmChoice;
    readonly Action<string> _notify;
    readonly Func<string?>? _browseFolder;
    readonly Func<string?>? _openDll;
    readonly Func<string?>? _openZip;
    readonly Func<string, string?>? _promptText;
    readonly Func<ModPackageType?>? _pickPackageType;
    readonly Func<GameBranch?>? _pickCurrentBranch;
    readonly Func<string?>? _openFolder;
    readonly Func<string, (ReportCategory Category, string Notes)?>? _promptReport;
    readonly Func<bool>? _promptSubmitGuide;
    readonly Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags)?>? _promptEditTaxonomy;
    readonly Action<string>? _copyText;
    readonly Action? _unselectLibrary;
    readonly Action? _unselectCatalog;
    readonly Func<DiagnosticsRedactionMode?>? _promptExportDiagnostics;
    readonly Func<MailProvider?>? _promptMailProvider;
    readonly Func<string, string?, (bool ok, bool option)?>? _promptConfirmOption;
    readonly Func<string, string?>? _saveZipFile;
    readonly Action<string>? _revealInExplorer;
    readonly Action<string, string>? _beginBusy;
    readonly Action<string>? _setBusyMessage;
    readonly Action? _endBusy;
    readonly BranchSwitchService _branchSwitch;
    readonly CriticalOpGuard _criticalOp;
    readonly IProcessProbe _processProbe;
    readonly IProcessStarter _processStarter;
    readonly ISteamLifecycle _steamLifecycle;
    readonly TaskProgressSession _taskProgress = new();
    readonly Func<TimeSpan, Task> _delay;
    readonly TimeSpan _steamExitTimeout;
    readonly TimeSpan _steamExitCooldown;
    readonly TimeSpan _steamRestartCooldown;
    bool _loggedMelonOptimize;
    bool _suppressBranchSwitchSave;
    bool _checkingUpdates;
    bool _checkingCatalog;
    bool _addingCatalogMod;
    /// <summary>
    /// Dispatcher that owns <see cref="LibraryModsView"/> / <see cref="CatalogModsView"/>.
    /// Captured at construction so post-download continuations (thread-pool after
    /// <c>ConfigureAwait(false)</c> in the catalog service) can marshal collection mutations
    /// even when <see cref="Application.Current"/> is null (unit tests).
    /// </summary>
    readonly Dispatcher _uiDispatcher;
    readonly object _modsSync = new();
    readonly object _catalogModsSync = new();
    readonly List<CatalogModItemViewModel> _catalogSelection = new();
    bool _autoImportedFromGame;
    readonly HashSet<string> _assemblyGeneratePrompted = new(StringComparer.OrdinalIgnoreCase);
    bool _suppressLanguageSave;
    bool _reporting;
    bool _exportingDiagnostics;
    bool _suppressFilterRefresh;
    CancellationTokenSource? _busyWorkCts;
    CancellationTokenSource? _wizardDownloadPollCts;
    int _wizardDownloadPollGeneration;
    bool _wizardArchiveBRunning;
    Action? _requestProcessExit;

    public IRelayCommand ApplyProfileCommand { get; }

    public MainViewModel(
        PathsService paths,
        JsonStore store,
        GameDetector detector,
        ModLibraryService library,
        ProfileService profiles,
        DeployService deploy,
        GameLauncher launcher,
        RiskGate riskGate,
        MelonLoaderConfigOptimizer? melonOptimizer = null,
        MelonLoaderDualStoreSync? melonDualSync = null,
        MelonLoaderAssemblyGenerator? melonAssemblyGenerator = null,
        RiskHeuristic? riskHeuristic = null,
        SteamGameLocator? steamLocator = null,
        UpdateChecker? updateChecker = null,
        ModCatalogService? catalog = null,
        AssemblyInspector? assemblyInspector = null,
        Func<string, bool>? confirmHighRisk = null,
        Func<string, bool>? confirm = null,
        Action<string>? notify = null,
        Func<string?>? browseFolder = null,
        Func<string?>? openDll = null,
        Func<string?>? openZip = null,
        Func<string, string?>? promptText = null,
        Func<ModPackageType?>? pickPackageType = null,
        Func<GameBranch?>? pickCurrentBranch = null,
        Func<string?>? openFolder = null,
        Func<string, (ReportCategory Category, string Notes)?>? promptReport = null,
        Func<bool>? promptSubmitGuide = null,
        Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags)?>? promptEditTaxonomy = null,
        Action<string>? copyText = null,
        Action? unselectLibrary = null,
        Action? unselectCatalog = null,
        BranchSwitchService? branchSwitch = null,
        IProcessProbe? processProbe = null,
        IProcessStarter? processStarter = null,
        Func<TimeSpan, Task>? delay = null,
        TimeSpan? steamExitTimeout = null,
        TimeSpan? steamExitCooldown = null,
        TimeSpan? steamRestartCooldown = null,
        Func<string, MessageBoxResult, bool>? confirmChoice = null,
        ManagerLogWriter? managerLog = null,
        Func<DiagnosticsRedactionMode?>? promptExportDiagnostics = null,
        Func<string, string?>? saveZipFile = null,
        Action<string>? revealInExplorer = null,
        Action<string, string>? beginBusy = null,
        Action<string>? setBusyMessage = null,
        Action? endBusy = null,
        CriticalOpGuard? criticalOp = null,
        Action? requestProcessExit = null,
        ISteamLifecycle? steamLifecycle = null,
        Func<MailProvider?>? promptMailProvider = null,
        Func<string, string?, (bool ok, bool option)?>? promptConfirmOption = null)
    {
        _paths = paths;
        _store = store;
        _detector = detector;
        _library = library;
        _profiles = profiles;
        _deploy = deploy;
        _launcher = launcher;
        _riskGate = riskGate;
        _melonOptimizer = melonOptimizer ?? new MelonLoaderConfigOptimizer();
        _melonDualSync = melonDualSync ?? new MelonLoaderDualStoreSync();
        _melonAssemblyGenerator = melonAssemblyGenerator ?? new MelonLoaderAssemblyGenerator();
        _riskHeuristic = riskHeuristic ?? new RiskHeuristic();
        _steamLocator = steamLocator ?? new SteamGameLocator();
        _requestProcessExit = requestProcessExit;
        _updateChecker = updateChecker ?? new UpdateChecker();
        _catalog = catalog ?? new ModCatalogService();
        _assemblyInspector = assemblyInspector ?? new AssemblyInspector();
        _managerLog = managerLog ?? new ManagerLogWriter(paths.LogsDir);
        _events = new ManagerEventLog(paths.LogsDir);
        _melonDualSync.Events ??= _events;
        // Default deny: UI must wire confirmation dialogs.
        _confirmHighRisk = confirmHighRisk ?? (_ => false);
        _confirm = confirm ?? (_ => false);
        _confirmChoice = confirmChoice;
        _notify = notify ?? (_ => { });
        _browseFolder = browseFolder;
        _openDll = openDll;
        _openZip = openZip;
        _promptText = promptText;
        _pickPackageType = pickPackageType;
        _pickCurrentBranch = pickCurrentBranch;
        _openFolder = openFolder;
        _promptReport = promptReport;
        _promptSubmitGuide = promptSubmitGuide;
        _promptEditTaxonomy = promptEditTaxonomy;
        _copyText = copyText;
        _unselectLibrary = unselectLibrary;
        _unselectCatalog = unselectCatalog;
        _promptExportDiagnostics = promptExportDiagnostics;
        _promptMailProvider = promptMailProvider;
        _promptConfirmOption = promptConfirmOption;
        _saveZipFile = saveZipFile;
        _revealInExplorer = revealInExplorer;
        _beginBusy = beginBusy;
        _setBusyMessage = setBusyMessage;
        _endBusy = endBusy;
        _processProbe = processProbe ?? new ProcessProbe();
        _processStarter = processStarter ?? new ShellProcessStarter();
        _delay = delay ?? (span => Task.Delay(span));
        _steamExitTimeout = steamExitTimeout ?? TimeSpan.FromSeconds(45);
        _steamExitCooldown = steamExitCooldown ?? TimeSpan.FromSeconds(3);
        _steamRestartCooldown = steamRestartCooldown ?? TimeSpan.FromSeconds(2);
        _steamLifecycle = steamLifecycle ?? new SteamLifecycle(
            _processProbe,
            _processStarter,
            _delay,
            _steamExitTimeout,
            _steamExitCooldown,
            log: AppendLog,
            notify: msg => _notify(msg));
        _branchSwitch = branchSwitch ?? new BranchSwitchService(
            paths,
            store,
            _processProbe,
            new JunctionService(),
            new SteamBetaKeyEditor(_processProbe));
        _criticalOp = criticalOp ?? new CriticalOpGuard(paths);
        ApplyProfileCommand = new AsyncRelayCommand(async () => _ = await ApplyProfileAsync(), () => CanApplyProfile);

        Ui = new UiStrings();
        Profiles = new ObservableCollection<ProfileItemViewModel>();
        Mods = new ObservableCollection<ModItemViewModel>();
        CatalogMods = new ObservableCollection<CatalogModItemViewModel>();
        // Always the creating thread's dispatcher — collections/views are affiliated with it.
        // Preferring Application.Current.Dispatcher broke parallel/STA tests that share a leaked
        // Application: RunOnUiThread Invoked onto the wrong or shut-down dispatcher.
        _uiDispatcher = Dispatcher.CurrentDispatcher;
        // Must be enabled before associating CollectionViews so background reloads after
        // catalog download (thread-pool) do not throw NotSupportedException / deadlock Invoke.
        BindingOperations.EnableCollectionSynchronization(Mods, _modsSync);
        BindingOperations.EnableCollectionSynchronization(CatalogMods, _catalogModsSync);
        CatalogModsView = CollectionViewSource.GetDefaultView(CatalogMods);
        CatalogModsView.Filter = FilterCatalogItem;
        LibraryModsView = CollectionViewSource.GetDefaultView(Mods);
        LibraryModsView.Filter = FilterLibraryItem;
        CatalogMods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsCatalogEmpty));
        Mods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsLibraryEmpty));
        CatalogAvailableTagOptions = new ObservableCollection<TagFilterOption>();
        LibraryAvailableTagOptions = new ObservableCollection<TagFilterOption>();
        RiskBanner = RiskGate.BannerText;
        LaunchModeOptions = new[]
        {
            new LaunchModeOption(LaunchMode.SteamThenExe, "Steam 优先，失败则直启"),
            new LaunchModeOption(LaunchMode.SteamOnly, "仅 Steam"),
            new LaunchModeOption(LaunchMode.ExeOnly, "仅直启 exe")
        };
        LanguageOptions = new[]
        {
            new LanguageOption("system", "System"),
            new LanguageOption("zh-CN", "简体中文"),
            new LanguageOption("en", "English"),
            new LanguageOption("ru", "Русский"),
            new LanguageOption("ja", "日本語"),
            new LanguageOption("de", "Deutsch")
        };

        _paths.EnsureCreated();
        _managerLog.PurgeOldFiles();
        _profiles.EnsureDefaults();

        var config = LoadConfig();
        // null = never configured → factory COS default. "" = player opted out (do not refill).
        if (config.MirrorBaseUrl is null)
            ApplyMirrorBaseUrl(DomesticMirrorDefaults.BaseUrl, save: true);
        else
            ApplyMirrorBaseUrl(config.MirrorBaseUrl, save: false);
        ApplyUiLanguage(config.UiLanguage, save: false, refreshUi: true);

        try
        {
            LoadBranchSwitchState();
        }
        catch (Exception ex)
        {
            AppendLog($"双服配置读取失败，已忽略：{ex.Message}");
        }

        _gamePath = ResolveInitialGamePath(config.GamePath);
        _launchMode = config.LaunchMode;
        _usePortableDataRoot = IsPortableRoot(config.DataRoot);
        _checkModUpdatesOnStartup = config.CheckModUpdatesOnStartup;

        if (!string.Equals(config.GamePath ?? "", _gamePath, StringComparison.OrdinalIgnoreCase))
        {
            config.GamePath = _gamePath;
            SaveConfig(config);
        }

        var selectId = config.ActiveProfileId;
        if (BranchSwitchEnabled)
            selectId = ActiveGameBranch == GameBranch.Official ? OfficialProfileId : BetaProfileId;

        _suppressBranchSwitchSave = true;
        try
        {
            ReloadProfiles(selectId: selectId);
            SelectBoundProfile(ActiveGameBranch);
        }
        finally
        {
            _suppressBranchSwitchSave = false;
        }
        RefreshStatus();
        ReloadMods();
        RebuildFilterOptionLabels();
        RefreshCatalogView();
        RefreshLibraryView();
        RecomputeDirty();
        UpdateLoaderVersionWarning();
        UpdateFirstAssemblyWarning();
        TryAutoImportFromGame();

        if (string.IsNullOrWhiteSpace(_gamePath))
            AppendLog("未自动找到游戏目录。请在「设置」中浏览选择 Mechabellum 安装路径。");
        else if (!SteamGameLocator.LooksLikeGameRoot(_gamePath))
            AppendLog("当前游戏路径无效。请在「设置」中重新选择包含 Mechabellum.exe 的目录。");
    }

    string ResolveInitialGamePath(string? configured)
    {
        var branchCfg = _branchSwitch.LoadConfig();
        // Mid-wizard leaves Enabled=false but still owns SteamLinkPath — do not let locator overwrite it.
        var wizardInProgress = branchCfg.WizardStep is not BranchWizardStep.None
            and not BranchWizardStep.Ready;
        var skipLocator = branchCfg.Enabled
            || wizardInProgress
            || File.Exists(_paths.BranchSwitchJournalPath);

        if ((branchCfg.Enabled || skipLocator) && !string.IsNullOrWhiteSpace(branchCfg.SteamLinkPath))
            return Path.GetFullPath(branchCfg.SteamLinkPath);

        if (SteamGameLocator.LooksLikeGameRoot(configured))
            return Path.GetFullPath(configured!);

        if (!skipLocator)
        {
            var found = _steamLocator.TryFind();
            if (!string.IsNullOrWhiteSpace(found))
            {
                AppendLog($"已自动定位游戏目录：{found}");
                return found;
            }
        }

        return configured?.Trim() ?? "";
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }
    public ObservableCollection<ModItemViewModel> Mods { get; }
    public ObservableCollection<CatalogModItemViewModel> CatalogMods { get; }
    public ICollectionView CatalogModsView { get; }
    public ICollectionView LibraryModsView { get; }
    public bool IsCatalogEmpty => CatalogMods.Count == 0;
    public bool IsLibraryEmpty => Mods.Count == 0;
    public ObservableCollection<TagFilterOption> CatalogAvailableTagOptions { get; }
    public ObservableCollection<TagFilterOption> LibraryAvailableTagOptions { get; }
    public IReadOnlyList<CategoryFilterOption> CatalogCategoryFilterOptions { get; private set; } = Array.Empty<CategoryFilterOption>();
    public IReadOnlyList<CategoryFilterOption> LibraryCategoryFilterOptions { get; private set; } = Array.Empty<CategoryFilterOption>();
    public IReadOnlyList<SortModeOption> SortModeOptions { get; private set; } = Array.Empty<SortModeOption>();
    public IReadOnlyList<LaunchModeOption> LaunchModeOptions { get; }
    public IReadOnlyList<LanguageOption> LanguageOptions { get; }
    public UiStrings Ui { get; }

    public bool IsReady => GameStatus?.Kind == GameStatusKind.Ready;

    /// <summary>Teal/green status accent. False after a launch whose Latest.log never updated.</summary>
    public bool ShowReadyAccent =>
        IsReady && GameStatus?.LoaderInjected != false;

    public bool NeedsMelonLoaderInstall =>
        !IsEnableDualWaitingDownload
        && (GameStatus?.Kind is GameStatusKind.GameOkLoaderMissing
            or GameStatusKind.LoaderPartial
            or GameStatusKind.LoaderPresentAssembliesMissing
        || (GameStatus?.Kind == GameStatusKind.Ready
            && MelonLoaderVersionGate.ShouldForceUpgradeStore(GamePath, GameStatus.MelonLoaderVersion)));

    /// <summary>Mid enable-dual: Steam link is hollow or a fresh other-branch download — not a lost install.</summary>
    bool IsEnableDualWaitingDownload =>
        !BranchSwitchEnabled && BranchWizardStep == BranchWizardStep.WaitingDownloadB;

    string? EnableDualWaitingBanner()
    {
        if (!IsEnableDualWaitingDownload)
            return null;
        if (IsWizardDownloadDiskReadyNow() && _processProbe.IsGameOrSteamRunning())
            return LocalizationService.T("StatusEnableDualWaitingExitSteam");
        return LocalizationService.T("StatusEnableDualWaitingDownload");
    }

    public bool CanDeployOrLaunch =>
        IsReady && !IsSessionLocked;

    /// <summary>
    /// Critical unfinished work: dual-folder wizard/settle/busy, or an active session-locked task.
    /// Does not disable the whole window — only CanExecute / button gates.
    /// </summary>
    public bool IsSessionLocked =>
        IsRecoveryGateActive
        || IsAwaitingSteamSettle
        || IsBranchWizardInProgress
        || IsBranchSwitchBusy
        || (_taskProgress.HasTask && _taskProgress.SessionLock);

    public bool IsCriticalOpRunning => _criticalOp.IsRunning;

    bool _isRecoveryGateActive;

    public bool IsRecoveryGateActive
    {
        get => _isRecoveryGateActive;
        internal set
        {
            if (_isRecoveryGateActive == value)
                return;
            _isRecoveryGateActive = value;
            OnPropertyChanged();
            NotifyBranchGates();
        }
    }

    public bool IsWizardWaitingSteam =>
        BranchWizardStep is BranchWizardStep.WaitingDownloadB;

    public CriticalOpGateLevel EvaluateCloseOrUpdateGate(bool busyDialogOpen) =>
        CriticalOpGate.Classify(
            _criticalOp.IsRunning,
            busyDialogOpen,
            IsBranchSwitchBusy,
            IsRecoveryGateActive,
            IsAwaitingSteamSettle,
            IsBranchWizardInProgress);

    internal async Task RunWithCriticalOpAsync(CriticalOpKind kind, string detail, Func<Task> action)
    {
        _criticalOp.Begin(kind, detail);
        try
        {
            await action().ConfigureAwait(true);
        }
        finally
        {
            _criticalOp.Complete();
        }
    }

    public bool ShouldOfferCriticalRecovery(CriticalOpGuard? guard = null)
    {
        var g = guard ?? _criticalOp;
        if (g.TryLoadInterrupted(out _))
            return true;

        try
        {
            return TryInspectOrphanDualLayout() && IsBranchWizardInProgress;
        }
        catch
        {
            return false;
        }
    }

    public void ApplyRecoveryContinue()
    {
        try
        {
            LoadBranchSwitchState();
        }
        catch
        {
            // Isolation: recovery must still route or clear.
        }

        ActiveContentPage = MainContentPage.Settings;

        if (IsAwaitingSteamSettle || IsBranchWizardInProgress)
        {
            ClearRecoveryGate();
            NotifyBranchGates();
            return;
        }

        // Sticky Hard only when Repair is actually offered; healthy Enabled dual-folder must not brick.
        if (!ShowRepairOrphanDualLayout)
            ClearRecoveryGate();

        NotifyBranchGates();
    }

    public async Task ApplyRecoveryRepair()
    {
        try
        {
            LoadBranchSwitchState();
        }
        catch
        {
            // Isolation: recovery must still route or clear.
        }

        ActiveContentPage = MainContentPage.Settings;
        try
        {
            await TryRunOrphanRepairIfAvailableAsync().ConfigureAwait(true);
        }
        catch
        {
            // Optional orphan repair must not block recovery routing.
        }

        if (IsAwaitingSteamSettle || IsBranchWizardInProgress || IsStaleReadyRecovery())
            ClearRecoveryGate();

        NotifyBranchGates();
    }

    public void ClearRecoveryGate()
    {
        _criticalOp.ClearInterrupted();
        IsRecoveryGateActive = false;
        NotifyBranchGates();
    }

    bool IsStaleReadyRecovery() =>
        !IsAwaitingSteamSettle
        && !IsBranchWizardInProgress
        && (BranchWizardStep is BranchWizardStep.None or BranchWizardStep.Ready)
        && !ShowRepairOrphanDualLayout;

    bool TryInspectOrphanDualLayout()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(GamePath))
                return false;
            return _branchSwitch.InspectOrphanDualLayout(GamePath).IsOrphan;
        }
        catch
        {
            return false;
        }
    }

    async Task TryRunOrphanRepairIfAvailableAsync()
    {
        try
        {
            if (EmergencyRecoverSingleCommand.CanExecute(null))
                await EmergencyRecoverSingleCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
        catch
        {
            // Repair must not block recovery routing.
        }
    }

    public bool TryHandleWindowClosing(bool busyDialogOpen, out bool cancel)
    {
        var level = EvaluateCloseOrUpdateGate(busyDialogOpen);
        if (level == CriticalOpGateLevel.HardBlock)
        {
            _notify(LocalizationService.T("CriticalOpBlockClose"));
            cancel = true;
            return true;
        }

        if (level == CriticalOpGateLevel.SoftConfirm)
        {
            var prompt = string.Format(
                LocalizationService.T("CriticalOpSoftCloseConfirm"),
                SoftGateStateLabel());
            if (!_confirm(prompt))
            {
                cancel = true;
                return true;
            }

            CancelBusyWork();
            AbandonUserCancelledWork();
            try { _requestProcessExit?.Invoke(); } catch { /* ignore */ }
            cancel = false;
            return false;
        }

        cancel = false;
        return false;
    }

    public void CancelBusyWork()
    {
        try { _busyWorkCts?.Cancel(); } catch { /* ignore */ }
        StopWizardDownloadPoll();
    }

    /// <summary>
    /// User confirmed close / recovery abandon: clear sticky tasks.
    /// Mid-enable (!Enabled): full session rollback to single folder.
    /// Dual Enabled settle: cancel settle wait only (keep dual Ready).
    /// </summary>
    public void AbandonUserCancelledWork()
    {
        CancelBusyWork();
        _taskProgress.Clear();
        NotifyTaskProgress();

        if (!BranchSwitchEnabled && IsBranchWizardInProgress)
        {
            RollbackEnableDualToSingle(notify: false);
            return;
        }

        if (BranchSwitchEnabled
            && (IsAwaitingSteamSettle || BranchWizardStep == BranchWizardStep.AwaitingSteamSettle))
        {
            // Closing the window must not promote a fake Ready. Keep settle until ACF is confirmed.
            _suppressBranchSwitchSave = true;
            try
            {
                DegradeToManualBeta = false;
                IsAwaitingSteamSettle = true;
                BranchWizardStep = BranchWizardStep.AwaitingSteamSettle;
            }
            finally
            {
                _suppressBranchSwitchSave = false;
            }

            TryUpdateBranchConfig(cfg => cfg.WizardStep = BranchWizardStep.AwaitingSteamSettle);
            RecordEvent(ManagerEventLog.SettleAbandoned, new Dictionary<string, string?>
            {
                ["wizardStep"] = BranchWizardStep.ToString(),
                ["awaitingSteamSettle"] = "true"
            });
            SyncStickyBranchTaskStrip();
            NotifyTaskProgress();
            NotifyBranchGates();
            RefreshBranchStatusText();
            ClearRecoveryGate();
            return;
        }

        if (IsAwaitingSteamSettle)
        {
            IsAwaitingSteamSettle = false;
            DegradeToManualBeta = false;
            SyncStickyBranchTaskStrip();
            NotifyTaskProgress();
            NotifyBranchGates();
            RefreshBranchStatusText();
        }

        ClearRecoveryGate();
    }

    /// <summary>
    /// Disk rollback for a cancelled/failed enable-dual session (session-owned stores only).
    /// </summary>
    void RollbackEnableDualToSingle(bool notify)
    {
        StopWizardDownloadPoll();
        try
        {
            var result = _branchSwitch.TryRollbackEnableSession(GamePath);
            if (!result.Success)
            {
                if (!string.IsNullOrWhiteSpace(result.Message))
                    AppendLog(string.Format(LocalizationService.T("NotifyEnableDualRollbackFailed"), result.Message));

                var blocked = _branchSwitch.LoadConfig();
                if (blocked.SessionOwnedOfficialStore || blocked.SessionOwnedBetaStore)
                {
                    // Steam/game still running: keep session ownership; do not claim full rollback.
                    _suppressBranchSwitchSave = true;
                    try
                    {
                        BranchSwitchEnabled = blocked.Enabled;
                        BranchWizardStep = blocked.WizardStep;
                        IsAwaitingSteamSettle = false;
                    }
                    finally
                    {
                        _suppressBranchSwitchSave = false;
                    }

                    IsRecoveryGateActive = true;
                    var tip = LocalizationService.T("NotifyEnableDualRollbackSteamBusy");
                    AppendLog(tip);
                    if (notify)
                        _notify(tip);
                    NotifyBranchGates();
                    RefreshBranchStatusText();
                    RefreshStatus();
                    return;
                }
            }

            var cfg = _branchSwitch.LoadConfig();
            if (!string.IsNullOrWhiteSpace(cfg.SteamLinkPath)
                && !string.Equals(GamePath, cfg.SteamLinkPath, StringComparison.OrdinalIgnoreCase))
                GamePath = cfg.SteamLinkPath;

            try
            {
                var appCfg = LoadConfig();
                appCfg.GamePath = GamePath;
                SaveConfig(appCfg);
            }
            catch { /* best-effort */ }

            _suppressBranchSwitchSave = true;
            try
            {
                BranchSwitchEnabled = false;
                BranchWizardStep = BranchWizardStep.None;
                IsAwaitingSteamSettle = false;
                DegradeToManualBeta = false;
            }
            finally
            {
                _suppressBranchSwitchSave = false;
            }

            _taskProgress.Clear();
            NotifyTaskProgress();
            ClearRecoveryGate();
            AppendLog(LocalizationService.T("LogEnableDualRolledBack"));
            if (notify)
                _notify(LocalizationService.T("LogEnableDualRolledBack"));
            RefreshStatus();
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
        catch (Exception ex)
        {
            AppendLog("启用双服回滚失败：" + ex.Message);
            _taskProgress.Clear();
            NotifyTaskProgress();
            ClearRecoveryGate();
            _suppressBranchSwitchSave = true;
            try
            {
                BranchSwitchEnabled = false;
                BranchWizardStep = BranchWizardStep.None;
                IsAwaitingSteamSettle = false;
                DegradeToManualBeta = false;
            }
            finally
            {
                _suppressBranchSwitchSave = false;
            }

            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    public void ApplyRecoveryAbandon()
    {
        ActiveContentPage = MainContentPage.Settings;
        AbandonUserCancelledWork();
        ClearRecoveryGate();
        _notify(LocalizationService.T("NotifyRecoveryAbandoned"));
    }

    string SoftGateStateLabel() =>
        IsWizardWaitingSteam
            ? LocalizationService.T("TaskTitleBranchWizard")
            : HasCurrentTask && !string.IsNullOrWhiteSpace(TaskTitle)
                ? TaskTitle
                : ActiveBranchDisplayName;

    public bool HasCurrentTask => _taskProgress.HasTask;
    public string TaskTitle => _taskProgress.Title;
    public string TaskMessage => _taskProgress.Message;
    public double TaskProgressValue => _taskProgress.Percent ?? 0;
    public bool IsTaskIndeterminate => _taskProgress.IsIndeterminate;
    public bool ShowTaskPercent => _taskProgress.HasTask && _taskProgress.Percent is not null;
    public string TaskPercentLabel =>
        _taskProgress.Percent is { } p ? $"{p:0}%" : "";

    public bool CanApplyProfile =>
        CanDeployOrLaunch && IsDirty;

    public bool UseApplyAccent => CanApplyProfile;

    [ObservableProperty] private int _librarySelectionCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutdatedMods))]
    private int _outdatedCount;

    public bool HasOutdatedMods => OutdatedCount > 0;

    public int CatalogSelectionCount { get; private set; }

    public bool ShowConfirmManualBeta => IsAwaitingSteamSettle || DegradeToManualBeta;

    public bool CanConfirmManualBeta =>
        ShowConfirmManualBeta && GameStatus?.Kind == GameStatusKind.Ready;

    public bool ShowConfirmWizardExitSteam =>
        IsEnableDualWaitingDownload && IsWizardDownloadDiskReadyNow();

    public bool CanConfirmWizardExitSteam =>
        ShowConfirmWizardExitSteam && !IsBranchSwitchBusy && !_wizardArchiveBRunning;

    public string WizardExitSteamConfirmButtonText =>
        LocalizationService.T("WizardExitSteamContinue");

    /// <summary>
    /// Switch: dual Ready, or settle hollow-escape while Enabled.
    /// </summary>
    public bool CanSwitchGameBranch =>
        BranchSwitchEnabled
        && !IsBranchSwitchBusy
        && !IsSteamWritingGame
        && (BranchWizardStep == BranchWizardStep.Ready || IsAwaitingSteamSettle)
        && !(IsAwaitingSteamSettle && GameStatus?.Kind == GameStatusKind.Ready);

    public bool IsSteamWritingGame
    {
        get
        {
            try
            {
                var gamePath = string.IsNullOrWhiteSpace(GamePath)
                    ? _branchSwitch.LoadConfig().SteamLinkPath
                    : GamePath;
                if (string.IsNullOrWhiteSpace(gamePath))
                    return false;

                var acf = SteamBetaKeyEditor.FindAppManifestPath(gamePath);
                if (string.IsNullOrWhiteSpace(acf) || !File.Exists(acf))
                    return false;

                return SteamBetaKeyEditor.LooksSteamWritingGame(File.ReadAllText(acf));
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanStartBranchWizard =>
        !BranchSwitchEnabled
        && !IsBranchWizardInProgress
        && !IsAwaitingSteamSettle
        && !IsBranchSwitchBusy;

    /// <summary>Wizard left mid-flight (Enabled may still be false until finish).</summary>
    public bool IsBranchWizardInProgress =>
        BranchWizardStep is not BranchWizardStep.None and not BranchWizardStep.Ready;

    /// <summary>Show emergency when dual on, mid-enable, session-owned residue, or orphan leftovers.</summary>
    public bool ShowEmergencyRecoverSingle
    {
        get
        {
            if (IsBranchSwitchBusy)
                return false;
            if (BranchSwitchEnabled || IsBranchWizardInProgress)
                return true;
            try
            {
                var cfg = _branchSwitch.LoadConfig();
                if (cfg.SessionOwnedOfficialStore || cfg.SessionOwnedBetaStore)
                    return true;
                return _branchSwitch.InspectOrphanDualLayout(GamePath).IsOrphan;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool CanEmergencyRecoverSingle =>
        ShowEmergencyRecoverSingle && !IsBranchSwitchBusy;

    // Legacy names kept for recovery routing / older tests wiring through emergency.
    public bool CanTeardownBranchSwitch => CanEmergencyRecoverSingle && BranchSwitchEnabled;

    public bool ShowRepairOrphanDualLayout =>
        !BranchSwitchEnabled && !IsBranchWizardInProgress && ShowEmergencyRecoverSingle;

    public bool CanRepairOrphanDualLayout =>
        ShowRepairOrphanDualLayout && !IsAwaitingSteamSettle && !IsBranchSwitchBusy;

    public string SettleConfirmButtonText
    {
        get
        {
            if (ShowConfirmManualBeta && GameStatus?.Kind != GameStatusKind.Ready)
                return LocalizationService.T("BranchSwitchConfirmSettleWaiting");

            if (DegradeToManualBeta)
            {
                return ActiveGameBranch == GameBranch.Official
                    ? LocalizationService.T("BranchSwitchConfirmManualOfficial")
                    : LocalizationService.T("BranchSwitchConfirmManual");
            }

            return ActiveGameBranch == GameBranch.Official
                ? LocalizationService.T("BranchSwitchConfirmSettleOfficial")
                : LocalizationService.T("BranchSwitchConfirmSettleBeta");
        }
    }

    string ActiveBranchDisplayName =>
        ActiveGameBranch == GameBranch.Official
            ? LocalizationService.T("BranchStatusOfficial")
            : LocalizationService.T("BranchStatusBeta");

    /// <summary>
    /// Incomplete dual-folder wizard blocks deploy/launch.
    /// Only when the feature is enabled — leftover wizardStep with Enabled=false must not brick deploy.
    /// </summary>
    public bool IsBranchWizardBlocking =>
        BranchSwitchEnabled && IsBranchWizardInProgress;

    /// <summary>Why Apply / Apply-and-launch stay disabled (empty when allowed).</summary>
    public string DeployBlockedReason
    {
        get
        {
            var waiting = EnableDualWaitingBanner();
            if (waiting is not null)
                return waiting;

            if (IsReady
                && !IsSessionLocked)
                return "";

            if (!IsReady)
            {
                return GameStatus?.Kind switch
                {
                    GameStatusKind.LoaderPresentAssembliesMissing =>
                        LocalizationService.T("DeployBlockedAssembliesMissing"),
                    GameStatusKind.GameOkLoaderMissing =>
                        LocalizationService.T("DeployBlockedLoaderMissing"),
                    GameStatusKind.LoaderPartial =>
                        LocalizationService.T("DeployBlockedLoaderPartial"),
                    _ => string.IsNullOrWhiteSpace(GameStatus?.Message)
                        ? LocalizationService.T("DeployBlockedNotReady")
                        : GameStatus.Message
                };
            }

            if (IsRecoveryGateActive)
                return LocalizationService.T("CriticalOpRecoveryBody");
            if (IsAwaitingSteamSettle)
                return LocalizationService.T("DeployBlockedAwaitingSteam");
            if (IsBranchWizardInProgress)
                return LocalizationService.T("DeployBlockedWizardIncomplete");
            if (IsBranchSwitchBusy || (_taskProgress.HasTask && _taskProgress.SessionLock))
                return LocalizationService.T("DeployBlockedBusy");
            return LocalizationService.T("DeployBlockedGeneric");
        }
    }

    public bool ShowDeployBlockedReason => !string.IsNullOrWhiteSpace(DeployBlockedReason);

    public string StatusKindLabel =>
        IsEnableDualWaitingDownload
            ? LocalizationService.T("BranchStatusEnabling")
            : GameStatus?.LoaderInjected == false
                ? "未注入"
                : GameStatus?.Kind switch
            {
                GameStatusKind.Ready => "就绪",
                GameStatusKind.LoaderPresentAssembliesMissing => "待生成程序集",
                GameStatusKind.GameOkLoaderMissing => "缺少 Loader",
                GameStatusKind.LoaderPartial => "Loader 不完整",
                GameStatusKind.GameMissing => "未找到游戏",
                _ => "未知"
            };

    public string DirtyHint => IsDirty ? "方案已改，游戏目录未同步 — 请点击「应用方案」" : "已与游戏目录同步";

    public string DiagnosisTitle => CurrentDiagnosis?.Title ?? "";

    public string DiagnosisTooltip
    {
        get
        {
            var d = CurrentDiagnosis;
            if (d is null)
                return "";
            var lines = new List<string>();
            if (d.Evidence.Count > 0)
                lines.Add(string.Join(Environment.NewLine, d.Evidence));
            if (!string.IsNullOrWhiteSpace(d.Action))
                lines.Add(d.Action);
            return string.Join(Environment.NewLine + Environment.NewLine, lines);
        }
    }

    [ObservableProperty] private Diagnosis? _currentDiagnosis;
    [ObservableProperty] private GameStatus? _gameStatus;
    [ObservableProperty] private ProfileItemViewModel? _selectedProfile;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private string _latestLogLine = "";
    [ObservableProperty] private bool _isLogExpanded;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _riskBanner = "";
    [ObservableProperty] private string _loaderVersionWarning = "";
    [ObservableProperty] private string _firstAssemblyWarning = "";
    [ObservableProperty] private string _missingEnabledPackagesWarning = "";
    [ObservableProperty] private string _gamePath = "";
    [ObservableProperty] private LaunchMode _launchMode;
    [ObservableProperty] private bool _usePortableDataRoot;
    [ObservableProperty] private bool _checkModUpdatesOnStartup;
    [ObservableProperty] private MainContentPage _activeContentPage = MainContentPage.Library;

    public bool IsLibraryPage => ActiveContentPage == MainContentPage.Library;
    public bool IsCatalogPage => ActiveContentPage == MainContentPage.Catalog;
    public bool IsSettingsPage => ActiveContentPage == MainContentPage.Settings;

    partial void OnActiveContentPageChanged(MainContentPage value)
    {
        OnPropertyChanged(nameof(IsLibraryPage));
        OnPropertyChanged(nameof(IsCatalogPage));
        OnPropertyChanged(nameof(IsSettingsPage));
    }
    [ObservableProperty] private string _catalogStatus = "";

    [ObservableProperty] private double _catalogDownloadPercent;
    [ObservableProperty] private bool _isCatalogDownloading;

    /// <summary>True when the catalog declares no size and the server sent no Content-Length,
    /// so the bar must animate rather than sit misleadingly at zero.</summary>
    [ObservableProperty] private bool _isCatalogDownloadIndeterminate;

    [ObservableProperty] private CatalogModItemViewModel? _selectedCatalogMod;
    [ObservableProperty] private ModItemViewModel? _selectedLibraryMod;
    [ObservableProperty] private string _appVersion = UpdateChecker.ReadLocalVersion();
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private string _mirrorBaseUrl = "";
    bool _suppressMirrorSave;
    [ObservableProperty] private string _selectedUiLanguageCode = "system";
    [ObservableProperty] private string _catalogSearchText = "";
    [ObservableProperty] private CategoryFilterOption? _selectedCatalogCategoryFilter;
    [ObservableProperty] private TagFilterOption? _selectedCatalogTagFilter;
    [ObservableProperty] private SortModeOption? _selectedCatalogSortMode;
    [ObservableProperty] private string _librarySearchText = "";
    [ObservableProperty] private CategoryFilterOption? _selectedLibraryCategoryFilter;
    [ObservableProperty] private TagFilterOption? _selectedLibraryTagFilter;
    [ObservableProperty] private SortModeOption? _selectedLibrarySortMode;
    [ObservableProperty] private bool _branchSwitchEnabled;
    [ObservableProperty] private GameBranch _activeGameBranch = GameBranch.Official;
    [ObservableProperty] private string _betaBranchName = BranchSwitchConfig.DefaultSteamBetaBranchName;
    [ObservableProperty] private string _officialProfileId = "default";
    [ObservableProperty] private string _betaProfileId = "default";
    [ObservableProperty] private string _branchStatusText = "未配置";
    [ObservableProperty] private bool _isBranchSwitchBusy;
    [ObservableProperty] private bool _isAwaitingSteamSettle;
    [ObservableProperty] private bool _degradeToManualBeta;
    [ObservableProperty] private BranchWizardStep _branchWizardStep = BranchWizardStep.None;

    partial void OnCatalogSearchTextChanged(string value)
    {
        if (!_suppressFilterRefresh) RefreshCatalogView();
    }
    partial void OnSelectedCatalogCategoryFilterChanged(CategoryFilterOption? value)
    {
        if (!_suppressFilterRefresh) RefreshCatalogView();
    }
    partial void OnSelectedCatalogTagFilterChanged(TagFilterOption? value)
    {
        if (!_suppressFilterRefresh) RefreshCatalogView();
    }
    partial void OnSelectedCatalogSortModeChanged(SortModeOption? value)
    {
        if (!_suppressFilterRefresh) RefreshCatalogView();
    }
    partial void OnLibrarySearchTextChanged(string value)
    {
        if (!_suppressFilterRefresh) RefreshLibraryView();
    }
    partial void OnSelectedLibraryCategoryFilterChanged(CategoryFilterOption? value)
    {
        if (!_suppressFilterRefresh) RefreshLibraryView();
    }
    partial void OnSelectedLibraryTagFilterChanged(TagFilterOption? value)
    {
        if (!_suppressFilterRefresh) RefreshLibraryView();
    }
    partial void OnSelectedLibrarySortModeChanged(SortModeOption? value)
    {
        if (!_suppressFilterRefresh) RefreshLibraryView();
    }

    partial void OnSelectedUiLanguageCodeChanged(string? oldValue, string newValue)
    {
        if (_suppressLanguageSave) return;
        ApplyUiLanguage(newValue, save: true, refreshUi: true, previousConfiguredOverride: oldValue);
    }

    void ApplyUiLanguage(string? code, bool save, bool refreshUi, string? previousConfiguredOverride = null)
    {
        var configured = string.IsNullOrWhiteSpace(code) ? "system" : code.Trim();
        var previousConfigured = previousConfiguredOverride ?? SelectedUiLanguageCode;
        var previousResolved = LocalizationService.ResolveConfiguredLanguage(previousConfigured);
        var resolved = LocalizationService.ResolveConfiguredLanguage(configured);
        LocalizationService.Apply(resolved);

        _suppressLanguageSave = true;
        try
        {
            SelectedUiLanguageCode = configured;
            void UpdateSystemLabel()
            {
                var system = LanguageOptions.FirstOrDefault(o => o.Code == "system");
                if (system is not null)
                    system.Label = LocalizationService.T("LanguageSystem");
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
                _ = dispatcher.BeginInvoke(UpdateSystemLabel);
            else if (dispatcher is not null)
                _ = dispatcher.BeginInvoke(UpdateSystemLabel, System.Windows.Threading.DispatcherPriority.Background);
            else
                UpdateSystemLabel();
        }
        finally
        {
            _suppressLanguageSave = false;
        }

        if (save)
        {
            var config = LoadConfig();
            config.UiLanguage = configured;
            SaveConfig(config);
        }

        if (string.Equals(configured, "system", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(previousConfigured, "system", StringComparison.OrdinalIgnoreCase)
            && string.Equals(resolved, previousResolved, StringComparison.OrdinalIgnoreCase))
        {
            var display = resolved switch
            {
                "zh-CN" => "简体中文",
                "en" => "English",
                "ru" => "Русский",
                "ja" => "日本語",
                "de" => "Deutsch",
                _ => resolved
            };
            AppendLog(string.Format(LocalizationService.T("NotifyFollowedSystemLanguage"), display));
        }

        if (refreshUi)
        {
            Ui.Refresh();
            RebuildFilterOptionLabels();
            RefreshBranchStatusText();
            foreach (var mod in Mods)
                mod.NotifyDetailChanged();
            foreach (var mod in CatalogMods)
                mod.NotifyDisplayChanged();
            RefreshCatalogView();
            RefreshLibraryView();
        }
    }

    partial void OnCurrentDiagnosisChanged(Diagnosis? value)
    {
        OnPropertyChanged(nameof(DiagnosisTitle));
        OnPropertyChanged(nameof(DiagnosisTooltip));
    }

    partial void OnGameStatusChanged(GameStatus? value)
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(ShowReadyAccent));
        OnPropertyChanged(nameof(StatusKindLabel));
        OnPropertyChanged(nameof(NeedsMelonLoaderInstall));
        InstallMelonLoaderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanConfirmManualBeta));
        OnPropertyChanged(nameof(SettleConfirmButtonText));
        ConfirmManualBetaCommand.NotifyCanExecuteChanged();
        NotifyBranchGates();
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(DirtyHint));
        OnPropertyChanged(nameof(CanApplyProfile));
        OnPropertyChanged(nameof(UseApplyAccent));
        ApplyProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnGamePathChanged(string value)
    {
        var config = LoadConfig();
        config.GamePath = value ?? "";
        SaveConfig(config);
        RefreshStatus();
        RecomputeDirty();
        UpdateLoaderVersionWarning();
        UpdateFirstAssemblyWarning();
    }

    partial void OnLaunchModeChanged(LaunchMode value)
    {
        var config = LoadConfig();
        config.LaunchMode = value;
        SaveConfig(config);
    }

    partial void OnMirrorBaseUrlChanged(string value)
    {
        if (_suppressMirrorSave)
            return;
        ApplyMirrorBaseUrl(value, save: true);
    }

    void ApplyMirrorBaseUrl(string? value, bool save)
    {
        // Persist "" for cleared/opt-out so the next launch does not treat it as null (factory default).
        var normalized = string.IsNullOrWhiteSpace(value) ? "" : value.Trim().TrimEnd('/');
        _suppressMirrorSave = true;
        try
        {
            if (!string.Equals(MirrorBaseUrl, normalized, StringComparison.Ordinal))
                MirrorBaseUrl = normalized;
        }
        finally
        {
            _suppressMirrorSave = false;
        }

        string? active = string.IsNullOrEmpty(normalized) ? null : normalized;

        // The mirror serves the installer and mod binaries, so plain http would let anyone on
        // the path swap them. Refuse rather than silently downgrade the whole update channel.
        // Persist "" (not null) so a refused URL is not mistaken for "never configured".
        var persisted = normalized;
        if (active is not null && !ExternalUrlPolicy.IsHttpsUrl(active))
        {
            AppendLog(string.Format(LocalizationService.T("LogMirrorMustBeHttps"), active));
            active = null;
            persisted = "";
            _suppressMirrorSave = true;
            try { MirrorBaseUrl = ""; }
            finally { _suppressMirrorSave = false; }
        }

        _catalog.MirrorBaseUrl = active;
        _catalog.DataRoot = _paths.DataRoot;
        _updateChecker.MirrorBaseUrl = active;

        if (!save)
            return;

        var config = LoadConfig();
        config.MirrorBaseUrl = persisted;
        SaveConfig(config);
    }

    partial void OnCheckModUpdatesOnStartupChanged(bool value)
    {
        var config = LoadConfig();
        config.CheckModUpdatesOnStartup = value;
        SaveConfig(config);
    }

    /// <summary>
    /// Reports which installed mods the catalog has moved past. Deliberately does not install:
    /// the player asked to be told, not to have a loaded DLL swapped under them.
    /// </summary>
    public async Task RunStartupModUpdateCheckAsync()
    {
        if (!CheckModUpdatesOnStartup)
            return;

        try
        {
            var root = await _catalog.FetchCatalogAsync().ConfigureAwait(true);

            // Populating the list is what makes the check visible: it enriches the installed
            // rows, so the status column and the per-row update button are live from launch
            // rather than waiting for the player to open the catalog panel.
            ApplyCatalogRoot(root);

            var packages = _library.List();
            var stale = root.Mods
                .Where(mod => ModCatalogService.GetEntryState(packages, mod) == CatalogEntryState.UpdateAvailable)
                .Select(mod => CatalogLocaleResolver.ResolveName(mod))
                .ToList();

            if (stale.Count == 0)
            {
                AppendLog(LocalizationService.T("LogModUpdateCheckAllCurrent"));
                return;
            }

            var message = string.Format(
                LocalizationService.T("NotifyModUpdatesAvailable"),
                stale.Count,
                string.Join("、", stale));
            AppendLog(message);
            _notify(message);
        }
        catch (Exception ex)
        {
            AppendLog(string.Format(LocalizationService.T("LogModUpdateCheckFailed"), ex.Message));
        }
    }

    partial void OnUsePortableDataRootChanged(bool value)
    {
        var config = LoadConfig();
        config.DataRoot = value
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : null;
        SaveConfig(config);
        AppendLog(value
            ? $"已选择便携数据根（需重启生效）：{config.DataRoot}"
            : "已选择 AppData 数据根（需重启生效）。");
    }

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value)
    {
        if (value is null) return;
        var config = LoadConfig();
        if (!string.Equals(config.ActiveProfileId, value.Id, StringComparison.OrdinalIgnoreCase))
        {
            config.ActiveProfileId = value.Id;
            SaveConfig(config);
        }

        if (BranchSwitchEnabled && !_suppressBranchSwitchSave)
        {
            if (ActiveGameBranch == GameBranch.Official)
                OfficialProfileId = value.Id;
            else
                BetaProfileId = value.Id;
        }

        ReloadMods();
        RecomputeDirty();
        UpdateLoaderVersionWarning();
        UpdateFirstAssemblyWarning();
    }

    [RelayCommand]
    void RefreshStatus()
    {
        RefreshStatusCore(offerAssemblyGeneratePrompt: true);
    }

    void RefreshStatusCore(bool offerAssemblyGeneratePrompt)
    {
        var statusCfg = LoadConfig();
        GameStatus = _detector.Detect(
            GamePath,
            statusCfg.LastLaunchRequestedAt,
            applyStartedAt: statusCfg.LastApplyStartedAt,
            gameAlreadyRunning: _processProbe.IsGameRunning());
        if (GameStatus.LoaderInjected == false)
            RecordLoaderNotInjectedOnce();
        UpdateLoaderVersionWarning();
        UpdateFirstAssemblyWarning();
        var suppressWaitNoise = IsEnableDualWaitingDownload
            && GameStatus.Kind is GameStatusKind.GameMissing or GameStatusKind.GameOkLoaderMissing;
        if (!suppressWaitNoise)
            AppendLog(GameStatus.Message);
        TryOptimizeMelonLoader(logAlways: false);
        TryAutoImportFromGame();
        if (offerAssemblyGeneratePrompt && !IsEnableDualWaitingDownload)
            ScheduleAssemblyGeneratePrompt();
        OnPropertyChanged(nameof(NeedsMelonLoaderInstall));
        InstallMelonLoaderCommand.NotifyCanExecuteChanged();
        TryDegradeHollowReadyStore();
        EnsureWizardDownloadPollRunning();
        RefreshBranchStatusText();
        RecalculateDiagnosis();
    }

    void RecalculateDiagnosis()
    {
        BranchSwitchConfig? branch = null;
        try { branch = _branchSwitch.LoadConfig(); }
        catch { /* ignore */ }

        CriticalOpMarker? marker = null;
        if (!_criticalOp.TryLoadInterrupted(out marker) && _criticalOp.IsRunning)
            marker = new CriticalOpMarker { Kind = _criticalOp.RunningKind, Detail = "running" };

        var diagCfg = LoadConfig();
        var snap = DiagnosisSnapshotBuilder.Capture(
            _paths,
            GamePath,
            GameStatus,
            diagCfg.LastLaunchRequestedAt,
            branch,
            isAwaitingSteamSettle: IsAwaitingSteamSettle,
            events: _events.ReadRecent(),
            deployBlockedOrFailed: ShowDeployBlockedReason && !string.IsNullOrWhiteSpace(DeployBlockedReason),
            deployFailure: DeployBlockedReason,
            interruptedCriticalOp: marker,
            applyStartedAt: diagCfg.LastApplyStartedAt);
        CurrentDiagnosis = DiagnosisEngine.Evaluate(snap);
    }

    void EnsureWizardDownloadPollRunning()
    {
        if (BranchWizardStep != BranchWizardStep.WaitingDownloadB)
            return;
        if (_wizardDownloadPollCts is { IsCancellationRequested: false })
            return;
        StartWizardDownloadPoll(ActiveGameBranch);
    }

    /// <summary>
    /// False BranchWizardStep.Ready while active store/link is not a game root → settle/repair.
    /// Does not degrade for ACF-only faults while the root is still intact.
    /// </summary>
    void TryDegradeHollowReadyStore()
    {
        if (!BranchSwitchEnabled || BranchWizardStep != BranchWizardStep.Ready)
            return;

        try
        {
            var cfg = _branchSwitch.LoadConfig();
            var store = ActiveGameBranch == GameBranch.Official
                ? cfg.OfficialStorePath
                : cfg.BetaStorePath;
            var storeConfigured = !string.IsNullOrWhiteSpace(store);
            var linkConfigured = !string.IsNullOrWhiteSpace(cfg.SteamLinkPath);
            // Incomplete config is not the field hollow-Ready case — avoid false degrade in tests/setup.
            if (!storeConfigured && !linkConfigured)
                return;

            var storeOk = !storeConfigured || SteamGameLocator.LooksLikeGameRoot(store);
            var linkOk = !linkConfigured || SteamGameLocator.LooksLikeGameRoot(cfg.SteamLinkPath);
            if (storeOk && linkOk)
                return;

            EnterSteamSettle();
            var msg = LocalizationService.T("NotifyHollowStoreDegraded");
            AppendLog(msg);
            _notify(msg);
        }
        catch (Exception ex)
        {
            AppendLog($"检查空壳仓失败：{ex.Message}");
        }
    }

    public bool CanOfferAssemblyGeneratePrompt =>
        !IsRecoveryGateActive
        && !IsEnableDualWaitingDownload
        && GameStatus?.Kind == GameStatusKind.LoaderPresentAssembliesMissing;

    void ScheduleAssemblyGeneratePrompt()
    {
        if (IsRecoveryGateActive)
            return;
        if (GameStatus?.Kind is not GameStatusKind.LoaderPresentAssembliesMissing)
            return;
        if (string.IsNullOrWhiteSpace(GamePath) || !Directory.Exists(GamePath))
            return;

        string full;
        try { full = Path.GetFullPath(GamePath); }
        catch { return; }

        if (!_assemblyGeneratePrompted.Add(full))
            return;

        async void RunPrompt()
        {
            try
            {
                if (IsRecoveryGateActive)
                    return;
                if (_detector.Detect(GamePath).Kind is not GameStatusKind.LoaderPresentAssembliesMissing)
                    return;

                if (!Confirm(LocalizationService.T("ConfirmGenerateAssembliesNow"), MessageBoxResult.Yes))
                {
                    AppendLog(LocalizationService.T("LogGenerateAssembliesDeclined"));
                    return;
                }

                await EnsureMelonAssembliesForStoreAsync(GamePath).ConfigureAwait(true);
                RefreshStatusCore(offerAssemblyGeneratePrompt: false);
            }
            catch (Exception ex)
            {
                AppendLog($"询问生成程序集时出错：{ex.Message}");
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            RunPrompt();
        else
            _ = dispatcher.BeginInvoke(RunPrompt, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    void TryAutoImportFromGame()
    {
        if (_autoImportedFromGame)
            return;

        if (GameStatus?.Kind is not (
            GameStatusKind.Ready or
            GameStatusKind.GameOkLoaderMissing or
            GameStatusKind.LoaderPartial))
            return;

        if (string.IsNullOrWhiteSpace(GamePath) ||
            !File.Exists(Path.Combine(GamePath, "Mechabellum.exe")))
            return;

        _autoImportedFromGame = true;
        try
        {
            var result = _library.ImportFromGame(GamePath);
            if (result.Imported <= 0)
                return;

            foreach (var msg in result.Messages)
                AppendLog(msg);
            AppendLog($"已从游戏自动导入 {result.Imported} 个包（跳过 {result.Skipped}）。");
            ReloadMods();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            RecomputeDirty();
        }
        catch (Exception ex)
        {
            AppendLog($"从游戏自动导入失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void ImportFromGame()
    {
        try
        {
            var result = _library.ImportFromGame(GamePath);
            foreach (var msg in result.Messages)
                AppendLog(msg);
            AppendLog($"从游戏导入完成：导入 {result.Imported}，跳过 {result.Skipped}。");
            ReloadMods();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            RecomputeDirty();
        }
        catch (Exception ex)
        {
            AppendLog($"从游戏导入失败：{ex.Message}");
        }
    }

    void TryOptimizeMelonLoader(bool logAlways)
    {
        // Ready / assemblies-missing: seed then optimize (Case B repair via refresh).
        // LoaderPartial: keep optimize for partial installs.
        if (GameStatus?.Kind is not (
            GameStatusKind.Ready or
            GameStatusKind.LoaderPresentAssembliesMissing or
            GameStatusKind.LoaderPartial))
            return;

        try
        {
            if (GameStatus.Kind is GameStatusKind.Ready or GameStatusKind.LoaderPresentAssembliesMissing)
            {
                var zip = MelonLoaderDualStoreSync.ResolveLocalZip();
                var seed = _melonDualSync.SeedDependencies(
                    GamePath,
                    MelonLoaderDualStoreSync.DeriveRedistDirFromMelonZip(zip));
                if (seed.Copied)
                    AppendLog(seed.Message);
                else if (!seed.Success)
                    AppendLog("警告：UnityDependencies 播种失败 — " + seed.Message);

                var result = seed.Success && seed.Version != null
                    ? _melonOptimizer.ApplyRecommendedSettings(GamePath, seed.Version)
                    : _melonOptimizer.ApplyRecommendedSettings(GamePath);
                if (result.Changed || logAlways || !_loggedMelonOptimize)
                {
                    AppendLog(result.Message);
                    _loggedMelonOptimize = true;
                }
            }
            else
            {
                var result = _melonOptimizer.ApplyRecommendedSettings(GamePath);
                if (result.Changed || logAlways || !_loggedMelonOptimize)
                {
                    AppendLog(result.Message);
                    _loggedMelonOptimize = true;
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"MelonLoader 优化配置失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void BrowseGamePath()
    {
        // Allow fixing an invalid path while settle/wizard locks write ops.
        if (IsSessionLocked && IsReady)
        {
            _notify(LocalizationService.T("NotifySessionLocked"));
            return;
        }
        var picked = _browseFolder?.Invoke();
        if (string.IsNullOrWhiteSpace(picked)) return;
        GamePath = picked;
    }

    [RelayCommand]
    void ImportDll()
    {
        var path = _openDll?.Invoke();
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var pkg = _library.ImportDll(path);
            AppendLog($"\u5df2\u5bfc\u5165 DLL\uff1a{pkg.DisplayName} ({pkg.Id})");
            TryEnableImportedPackages(pkg);
            ReloadMods();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            RecomputeDirty();
        }
        catch (ImportNeedsTypeException ex)
        {
            var picked = _pickPackageType?.Invoke();
            if (picked is null)
            {
                _library.DiscardStaging(ex.StagingPath);
                AppendLog("\u5df2\u53d6\u6d88\u5bfc\u5165\uff08\u672a\u9009\u62e9\u7c7b\u578b\uff09");
                return;
            }

            try
            {
                var pkg = _library.CommitStaging(ex.StagingPath, picked.Value);
                AppendLog($"\u5df2\u5bfc\u5165 DLL\uff1a{pkg.DisplayName} ({pkg.Id})");
                TryEnableImportedPackages(pkg);
                ReloadMods();
                UpdateLoaderVersionWarning();
                UpdateFirstAssemblyWarning();
                RecomputeDirty();
            }
            catch (Exception commitEx)
            {
                _library.DiscardStaging(ex.StagingPath);
                AppendLog($"\u5bfc\u5165 DLL \u5931\u8d25\uff1a{commitEx.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"\u5bfc\u5165 DLL \u5931\u8d25\uff1a{ex.Message}");
        }
    }

    [RelayCommand]
    void ImportZip()
    {
        var path = _openZip?.Invoke();
        if (string.IsNullOrWhiteSpace(path)) return;
        ImportZipFromPath(path, forceType: null);
    }

    [RelayCommand]
    void ImportFolder()
    {
        var path = _openFolder?.Invoke();
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var pkgs = _library.ImportFolder(path);
            AppendLog($"\u5df2\u5bfc\u5165\u6587\u4ef6\u5939\uff1a{pkgs.Count} \u4e2a\u5305");
            TryEnableImportedPackages(pkgs);
            ReloadMods();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            RecomputeDirty();
        }
        catch (ImportNeedsTypeException ex)
        {
            var picked = _pickPackageType?.Invoke();
            if (picked is null)
            {
                _library.DiscardStaging(ex.StagingPath);
                AppendLog("\u5df2\u53d6\u6d88\u5bfc\u5165\uff08\u672a\u9009\u62e9\u7c7b\u578b\uff09");
                return;
            }

            try
            {
                _library.DiscardStaging(ex.StagingPath);
                var pkgs = _library.ImportFolder(path, picked.Value);
                AppendLog($"\u5df2\u5bfc\u5165\u6587\u4ef6\u5939\uff1a{pkgs.Count} \u4e2a\u5305");
                TryEnableImportedPackages(pkgs);
                ReloadMods();
                UpdateLoaderVersionWarning();
                UpdateFirstAssemblyWarning();
                RecomputeDirty();
            }
            catch (Exception retryEx)
            {
                AppendLog($"\u5bfc\u5165\u6587\u4ef6\u5939 \u5931\u8d25\uff1a{retryEx.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"\u5bfc\u5165\u6587\u4ef6\u5939 \u5931\u8d25\uff1a{ex.Message}");
        }
    }

    void ImportZipFromPath(string path, ModPackageType? forceType)
    {
        try
        {
            var pkgs = _library.ImportZip(path, forceType);
            AppendLog($"\u5df2\u5bfc\u5165 Zip\uff1a{pkgs.Count} \u4e2a\u5305");
            TryEnableImportedPackages(pkgs);
            ReloadMods();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            RecomputeDirty();
        }
        catch (ImportNeedsTypeException ex)
        {
            var picked = _pickPackageType?.Invoke();
            if (picked is null)
            {
                _library.DiscardStaging(ex.StagingPath);
                AppendLog("\u5df2\u53d6\u6d88\u5bfc\u5165\uff08\u672a\u9009\u62e9\u7c7b\u578b\uff09");
                return;
            }

            try
            {
                _library.DiscardStaging(ex.StagingPath);
                var pkgs = _library.ImportZip(path, picked.Value);
                AppendLog($"\u5df2\u5bfc\u5165 Zip\uff1a{pkgs.Count} \u4e2a\u5305");
                TryEnableImportedPackages(pkgs);
                ReloadMods();
                UpdateLoaderVersionWarning();
                UpdateFirstAssemblyWarning();
                RecomputeDirty();
            }
            catch (Exception retryEx)
            {
                AppendLog($"\u5bfc\u5165 Zip \u5931\u8d25\uff1a{retryEx.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"\u5bfc\u5165 Zip \u5931\u8d25\uff1a{ex.Message}");
        }
    }

    /// <returns>true if deploy succeeded.</returns>
    public bool ApplyProfile() => ApplyProfile(ignoreBranchGate: false);

    public async Task<bool> ApplyProfileAsync()
    {
        RefreshStatusCore(offerAssemblyGeneratePrompt: false);
        if (GameStatus?.Kind == GameStatusKind.LoaderPresentAssembliesMissing)
            await EnsureMelonAssembliesForStoreAsync(GamePath).ConfigureAwait(true);
        return ApplyProfile(ignoreBranchGate: false);
    }



    bool ApplyProfile(bool ignoreBranchGate)
    {
        if (!ignoreBranchGate && IsSessionLocked)
        {
            AppendLog(LocalizationService.T("DeployBlockedWizardIncomplete"));
            return false;
        }

        if (SelectedProfile is null)
        {
            AppendLog("未选择方案。");
            return false;
        }

        RefreshStatusCore(offerAssemblyGeneratePrompt: false);
        if (GameStatus?.Kind == GameStatusKind.LoaderPresentAssembliesMissing)
        {
            AppendLog(GameStatus?.Message ?? "MelonLoader 程序集未就绪，请先生成后再部署。");
            return false;
        }

        if (GameStatus?.Kind != GameStatusKind.Ready)
        {
            AppendLog(GameStatus?.Message ?? "游戏状态未就绪，无法部署。");
            return false;
        }

        try
        {
            _taskProgress.Begin(
                ManagerTaskKind.Deploy,
                LocalizationService.T("TaskTitleDeploy"),
                LocalizationService.T("TaskMessageDeploy"),
                sessionLock: true);
            NotifyTaskProgress();

            var profile = _profiles.Get(SelectedProfile.Id);
            var packages = _library.List().ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
            var manifestPath = CurrentDeployManifestPath();
            var manifestPrevPath = CurrentDeployManifestPrevPath();
            var result = _deploy.Apply(
                profile,
                packages,
                GamePath,
                allowOverwriteUnmanaged: false,
                manifestPath: manifestPath,
                manifestPrevPath: manifestPrevPath);

            if (!result.Success &&
                result.Plan.ConflictsUnmanaged.Count > 0 &&
                result.Plan.IntraProfileNameCollisions.Count == 0)
            {
                var sample = string.Join("\n", result.Plan.ConflictsUnmanaged.Take(8).Select(Path.GetFileName));
                var more = result.Plan.ConflictsUnmanaged.Count > 8
                    ? string.Format(
                        LocalizationService.T("ConfirmOverwriteUnmanagedMore"),
                        result.Plan.ConflictsUnmanaged.Count)
                    : "";
                var prompt =
                    LocalizationService.T("ConfirmOverwriteUnmanaged") + "\n\n" + sample + more;
                if (Confirm(prompt))
                    result = _deploy.Apply(
                        profile,
                        packages,
                        GamePath,
                        allowOverwriteUnmanaged: true,
                        manifestPath: manifestPath,
                        manifestPrevPath: manifestPrevPath);
                else
                {
                    AppendLog(LocalizationService.T("LogCancelledOverwriteUnmanaged"));
                    return false;
                }
            }

            AppendLog(result.Message);
            if (!result.Success)
            {
                _notify(string.Format(LocalizationService.T("NotifyDeployFailed"), result.Message));
                return false;
            }

            RecomputeDirty();
            _notify(LocalizationService.T("NotifyApplySucceeded"));
            WarnIfProfileHasNoEnabledMods();
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"部署失败：{ex.Message}");
            _notify(string.Format(LocalizationService.T("NotifyDeployFailed"), ex.Message));
            return false;
        }
        finally
        {
            if (_taskProgress.Kind == ManagerTaskKind.Deploy)
            {
                _taskProgress.Clear();
                SyncStickyBranchTaskStrip();
                NotifyTaskProgress();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeployOrLaunch))]
    async Task ApplyAndLaunch()
    {
        var opStartedAt = DateTimeOffset.Now;
        var startedCfg = LoadConfig();
        startedCfg.LastApplyStartedAt = opStartedAt;
        SaveConfig(startedCfg);

        var missingAssembliesBefore = !GameDetector.HasIl2CppAssemblies(GamePath);
        if (!await ApplyProfileAsync().ConfigureAwait(true)) return;
        if (GameStatus?.Kind != GameStatusKind.Ready) return;

        TryOptimizeMelonLoader(logAlways: true);

        var generatedThisOp = missingAssembliesBefore && GameDetector.HasIl2CppAssemblies(GamePath);
        var config = LoadConfig();
        config.GamePath = GamePath;
        config.LaunchMode = LaunchMode;
        config.LastApplyStartedAt = opStartedAt;
        var launchConfig = config;
        if (generatedThisOp && LaunchMode == LaunchMode.SteamThenExe)
        {
            launchConfig = new AppConfig
            {
                GamePath = config.GamePath,
                LaunchMode = LaunchMode.ExeOnly,
                ActiveProfileId = config.ActiveProfileId,
                DataRoot = config.DataRoot,
                UiLanguage = config.UiLanguage,
                LastLaunchRequestedAt = config.LastLaunchRequestedAt,
                LastApplyStartedAt = config.LastApplyStartedAt,
                MirrorBaseUrl = config.MirrorBaseUrl
            };
            AppendLog(LocalizationService.T("LogLaunchExeAfterAssemblyGen"));
        }

        var launch = _launcher.Launch(launchConfig);
        if (!launch.Success)
            AppendLog(launch.Message);
        else
        {
            config.LastLaunchRequestedAt = DateTimeOffset.Now;
            SaveConfig(config);
            RecordEvent(ManagerEventLog.LaunchRequested, new Dictionary<string, string?>
            {
                ["mode"] = LaunchMode.ToString(),
                ["at"] = config.LastLaunchRequestedAt.Value.ToString("o")
            });
            AppendLog(LocalizationService.T("LogLaunchRequestedVerifying"));
            AppendLog(LocalizationService.T("LogMelonConsoleHint"));
            AppendLog(LocalizationService.T("LogMelonLaunchVerifyHint"));
            if (_melonOptimizer.NeedsFirstAssemblyGeneration(GamePath))
            {
                AppendLog(LocalizationService.T("LogFirstAssemblyLaunchHint"));
                UpdateFirstAssemblyWarning();
            }

            await VerifyLaunchProcessThenInjectionAsync().ConfigureAwait(true);
        }
    }

    async Task VerifyLaunchProcessThenInjectionAsync()
    {
        var seen = false;
        for (var i = 0; i < 15; i++)
        {
            if (_processProbe.IsGameRunning())
            {
                seen = true;
                break;
            }

            await _delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        }

        if (!seen)
            seen = _processProbe.IsGameRunning();

        if (!seen)
        {
            var msg = LocalizationService.T("NotifyLaunchProcessNotSeen");
            AppendLog(msg);
            _notify(msg);
            return;
        }

        AppendLog(LocalizationService.T("LogLaunchProcessSeen"));
        WarnIfRunningGamePathMismatch();
        await RecheckLoaderInjectionAfterLaunchAsync().ConfigureAwait(true);
    }

    void WarnIfRunningGamePathMismatch()
    {
        var running = _processProbe.TryGetRunningGameExePath();
        if (ProcessProbe.IsManagedGameExe(GamePath, running))
            return;

        var msg = string.Format(LocalizationService.T("NotifyLaunchGamePathMismatch"), running);
        AppendLog(msg);
        _notify(msg);
    }

    internal async Task RecheckLoaderInjectionAfterLaunchAsync()
    {
        try
        {
            await _delay(GameDetector.LoaderInjectSettle).ConfigureAwait(true);
            RefreshStatusCore(offerAssemblyGeneratePrompt: false);
            if (GameStatus?.LoaderInjected != false)
                return;

            for (var i = 0; i < 12; i++)
            {
                await _delay(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                RefreshStatusCore(offerAssemblyGeneratePrompt: false);
                if (GameStatus?.LoaderInjected != false)
                    return;
            }

            NotifyLoaderNotInjectedUseExeOnly();
        }
        catch (Exception ex)
        {
            AppendLog($"检查 Melon 注入状态失败：{ex.Message}");
        }
    }

    void WarnIfProfileHasNoEnabledMods()
    {
        if (ProfileHasEnabledMods())
            return;
        var msg = LocalizationService.T("NotifyEmptyProfileNoMods");
        AppendLog(msg);
        _notify(msg);
    }

    bool ProfileHasEnabledMods()
    {
        if (Mods.Any(m => m.IsEnabled))
            return true;
        if (SelectedProfile is null)
            return false;
        return _profiles.Get(SelectedProfile.Id).EnabledPackageIds.Count > 0;
    }

    void NotifyLoaderNotInjectedUseExeOnly()
    {
        var msg = LocalizationService.T("NotifyLoaderNotInjectedUseExeOnly");
        AppendLog(msg);
        _notify(msg);
    }

    [RelayCommand(CanExecute = nameof(CanConfirmWizardExitSteam))]
    async Task ConfirmWizardExitSteam()
    {
        if (!IsEnableDualWaitingDownload)
            return;

        if (!IsWizardDownloadReadyNow())
        {
            var msg = LocalizationService.T("LogSteamOrGameStillRunning");
            AppendLog(msg);
            _notify(msg);
            return;
        }

        await CompleteWizardAfterDownloadBAsync(ActiveGameBranch).ConfigureAwait(true);
    }

    [RelayCommand]
    async Task SwitchToOfficial() => await SwitchToBranchAsync(GameBranch.Official);

    [RelayCommand]
    async Task SwitchToBeta() => await SwitchToBranchAsync(GameBranch.Beta);

    [RelayCommand]
    async Task StartBranchWizard()
    {
        if (IsBranchSwitchBusy || IsAwaitingSteamSettle) return;
        if (BranchSwitchEnabled)
        {
            _notify(LocalizationService.T("NotifyEnableDualAlreadyOn"));
            return;
        }

        if (IsBranchWizardInProgress)
        {
            // No mid-wizard resume — roll back then user can click Enable again.
            RollbackEnableDualToSingle(notify: true);
            return;
        }

        IsBranchSwitchBusy = true;
        try
        {
            await RunWizardFromStartAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog($"双服向导失败：{ex.Message}");
            _notify(string.Format(LocalizationService.T("NotifyBranchWizardFailed"), ex.Message));
            RollbackEnableDualToSingle(notify: false);
        }
        finally
        {
            IsBranchSwitchBusy = false;
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    [RelayCommand]
    async Task EmergencyRecoverSingle()
    {
        if (!CanEmergencyRecoverSingle) return;

        if (TryNotifySteamWritingAndAbort())
            return;

        var prompt = EmergencyRecoverGuidance.Build(_branchSwitch.LoadConfig(), GamePath);
        if (!Confirm(LocalizationService.T(prompt.ConfirmKey)))
            return;

        var deleteOther = false;
        var askedAboutOther = false;
        if (!prompt.SkipDeleteOtherStore)
        {
            deleteOther = Confirm(
                LocalizationService.T("ConfirmBranchDeleteOtherStore"),
                MessageBoxResult.No);
            askedAboutOther = true;
        }

        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
        {
            FailWizard(LocalizationService.T("LogSteamOrGameStillRunning"));
            return;
        }

        IsBranchSwitchBusy = true;
        try
        {
            await RunWithCriticalOpAsync(CriticalOpKind.OrphanRepair, "EmergencyRecover", async () =>
            {
                await Task.Run(() =>
                {
                    var result = _branchSwitch.TryEmergencyRecoverToSingle(deleteOther, GamePath);
                    if (!result.Success)
                        throw new InvalidOperationException(
                            string.IsNullOrWhiteSpace(result.Message)
                                ? LocalizationService.T("NotifyOrphanRepairFailedGeneric")
                                : result.Message);
                }).ConfigureAwait(true);

                var cfg = _branchSwitch.LoadConfig();
                if (!string.IsNullOrWhiteSpace(cfg.SteamLinkPath)
                    && !string.Equals(GamePath, cfg.SteamLinkPath, StringComparison.OrdinalIgnoreCase))
                    GamePath = cfg.SteamLinkPath;

                try
                {
                    var appCfg = LoadConfig();
                    appCfg.GamePath = GamePath;
                    SaveConfig(appCfg);
                }
                catch { /* best-effort */ }

                _suppressBranchSwitchSave = true;
                try
                {
                    BranchSwitchEnabled = false;
                    BranchWizardStep = BranchWizardStep.None;
                    IsAwaitingSteamSettle = false;
                    DegradeToManualBeta = false;
                }
                finally
                {
                    _suppressBranchSwitchSave = false;
                }

                RefreshStatus();
                AppendLog(LocalizationService.T("LogOrphanRepairDone"));
                _notify(LocalizationService.T("NotifyOrphanRepairDone"));
                ClearRecoveryGate();

                if (!deleteOther)
                    WarnLeftoverDualStores(GamePath, userChoseKeep: askedAboutOther);
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            var mapped = MapBranchOperationFailure(
                ex.Message,
                LocalizationService.T("NotifyOrphanRepairFailedGeneric"));
            AppendLog(string.Format(LocalizationService.T("NotifyOrphanRepairFailed"), mapped));
            _notify(string.Format(LocalizationService.T("NotifyOrphanRepairFailed"), mapped));
        }
        finally
        {
            IsBranchSwitchBusy = false;
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    // Compatibility: old command names forward to emergency recover.
    [RelayCommand]
    async Task TeardownBranchSwitch() => await EmergencyRecoverSingle().ConfigureAwait(true);

    [RelayCommand]
    async Task RepairOrphanDualLayout() => await EmergencyRecoverSingle().ConfigureAwait(true);

    bool Confirm(string message, MessageBoxResult defaultResult = MessageBoxResult.Yes) =>
        _confirmChoice?.Invoke(message, defaultResult) ?? _confirm(message);

    bool TryNotifySteamWritingAndAbort()
    {
        try
        {
            var gamePath = string.IsNullOrWhiteSpace(GamePath)
                ? _branchSwitch.LoadConfig().SteamLinkPath
                : GamePath;
            if (string.IsNullOrWhiteSpace(gamePath))
                return false;

            var acf = SteamBetaKeyEditor.FindAppManifestPath(gamePath);
            if (string.IsNullOrWhiteSpace(acf) || !File.Exists(acf))
                return false;

            if (!SteamBetaKeyEditor.LooksSteamWritingGame(File.ReadAllText(acf)))
                return false;

            var msg = LocalizationService.T("NotifySteamWritingDoNotTouchStores");
            AppendLog(msg);
            _notify(msg);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirmManualBeta))]
    async Task ConfirmManualBeta()
    {
        if (!IsAwaitingSteamSettle && !DegradeToManualBeta) return;

        // Brief wait in case Steam is mid-finalize after download UI shows 100%.
        for (var i = 0; i < 8 && _processProbe.IsSteamRunning(); i++)
            await _delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(true);

        if (_processProbe.IsGameRunning())
        {
            _notify(LocalizationService.T("NotifyGameStillRunningSettle"));
            if (!Confirm(LocalizationService.T("ConfirmDeployWhileGameRunning")))
                return;
        }
        else if (_processProbe.IsSteamRunning())
        {
            // Steam open is OK for settle confirm: we only read ACF + deploy mods.
            // Disk swaps / writing live ACF still require Steam exit elsewhere.
            AppendLog(LocalizationService.T("LogSteamOpenDuringSettleOk"));
        }

        DeployBoundProfileAndClearSettle();
    }

    [RelayCommand]
    void ShowSettingsPage() => ActiveContentPage = MainContentPage.Settings;

    [RelayCommand]
    void ShowLibraryPage() => ActiveContentPage = MainContentPage.Library;

    [RelayCommand]
    void ToggleLogPanel() => IsLogExpanded = !IsLogExpanded;

    public bool CanInstallMelonLoader =>
        NeedsMelonLoaderInstall
        && !(IsSessionLocked && _taskProgress.Kind != ManagerTaskKind.MelonInstall);

    [RelayCommand(CanExecute = nameof(CanInstallMelonLoader))]
    async Task InstallMelonLoaderAsync()
    {
        if (!CanInstallMelonLoader) return;
        if (string.IsNullOrWhiteSpace(GamePath))
        {
            _notify(LocalizationService.T("NotifyMelonInstallNeedGamePath"));
            return;
        }

        if (!Confirm(LocalizationService.T(
                MelonLoaderVersionGate.ShouldForceUpgradeStore(GamePath, GameStatus?.MelonLoaderVersion)
                    ? "ConfirmUpgradeMelonLoader"
                    : "ConfirmInstallMelonLoader")))
            return;

        if (_processProbe.IsGameRunning())
        {
            _notify(LocalizationService.T("NotifyMelonInstallCloseGame"));
            return;
        }

        AppendLog(LocalizationService.T("LogMelonInstallStart"));
        _taskProgress.Begin(
            ManagerTaskKind.MelonInstall,
            LocalizationService.T("TaskTitleMelonInstall"),
            LocalizationService.T("TaskMessageMelonInstall"),
            sessionLock: true);
        NotifyTaskProgress();
        try
        {
            await RunWithCriticalOpAsync(CriticalOpKind.MelonInstall, "MelonInstall", async () =>
            {
                MelonLoaderInstallResult result;
                var redistDir = Path.Combine(AppContext.BaseDirectory, "installer-redist");
                Directory.CreateDirectory(redistDir);
                // MirrorBaseUrl "" = opt-out (origin only). Null should not happen after factory fill.
                var mirror = MirrorBaseUrl;
                AppendLog("正在准备 Melon 运行时离线包（镜像优先）…");
                var ensure = await new RedistEnsureService()
                    .EnsureAsync(
                        redistDir,
                        mirror,
                        ids:
                        [
                            "melonloader-x64",
                            "unity-deps-2022.3.62",
                            "cpp2il-exe",
                            "cpp2il-plugin"
                        ])
                    .ConfigureAwait(true);
                AppendLog(ensure.Message);
                foreach (var (id, source) in ensure.SourceById)
                    AppendLog($"  redist {id}: {source}");

                var zip = MelonLoaderDualStoreSync.ResolveLocalZip(
                    Path.Combine(redistDir, "melonloader", "MelonLoader.x64.zip"));
                if (zip is not null)
                {
                    AppendLog(string.Format(LocalizationService.T("LogMelonInstallLocalZip"), zip));
                    _taskProgress.Report(LocalizationService.T("TaskMessageMelonInstallLocal"));
                    NotifyTaskProgress();
                    result = await Task.Run(() => _melonDualSync.InstallFromZip(GamePath, zip)).ConfigureAwait(true);
                }
                else
                {
                    AppendLog(LocalizationService.T("LogMelonInstallDownload"));
                    var installer = new MelonLoaderInstaller(isGameRunning: () => _processProbe.IsGameRunning());
                    var progress = new Progress<MelonLoaderProgress>(p =>
                    {
                        _taskProgress.Report(p.Message, p.Percent);
                        NotifyTaskProgress();
                    });
                    result = await installer.InstallAsync(GamePath, progress).ConfigureAwait(true);
                }

                AppendLog(result.Message);
                _notify(result.Success
                    ? LocalizationService.T("NotifyMelonInstallOk")
                    : string.Format(LocalizationService.T("NotifyMelonInstallFailed"), result.Message));
                RefreshStatusCore(offerAssemblyGeneratePrompt: true);
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog(string.Format(LocalizationService.T("NotifyMelonInstallFailed"), ex.Message));
            _notify(string.Format(LocalizationService.T("NotifyMelonInstallFailed"), ex.Message));
        }
        finally
        {
            _taskProgress.Clear();
            SyncStickyBranchTaskStrip();
            NotifyTaskProgress();
        }
    }

    [RelayCommand]
    async Task ReportCatalogModAsync()
    {
        if (SelectedCatalogMod is null) return;
        await ReportAsync(
            SelectedCatalogMod.Id,
            SelectedCatalogMod.Name,
            "catalog").ConfigureAwait(true);
    }

    [RelayCommand]
    async Task ReportLibraryModAsync()
    {
        if (SelectedLibraryMod is null || SelectedLibraryMod.IsMissing) return;
        await ReportAsync(
            SelectedLibraryMod.Package.Id,
            SelectedLibraryMod.DisplayName,
            "library").ConfigureAwait(true);
    }

    bool CanEditLibraryTaxonomy() =>
        SelectedLibraryMod is not null && !SelectedLibraryMod.IsMissing;

    [RelayCommand(CanExecute = nameof(CanEditLibraryTaxonomy))]
    void EditLibraryModTaxonomy()
    {
        var mod = SelectedLibraryMod;
        if (mod is null || mod.IsMissing) return;
        var result = _promptEditTaxonomy?.Invoke(mod.Package);
        if (result is null) return;
        mod.Package.CategoryOverride = result.Value.Override;
        mod.Package.ExtraTags = result.Value.ExtraTags.ToList();
        PersistPackageMeta(mod.Package);
        mod.NotifyDetailChanged();
        RefreshLibraryView();
        AppendLog($"已更新分类/标签：{mod.DisplayName}");
    }

    Task ReportAsync(string modId, string modName, string source)
    {
        if (_reporting) return Task.CompletedTask;

        var picked = _promptReport?.Invoke(modName);
        if (picked is null) return Task.CompletedTask;

        if (!Confirm(Ui.ReportConfirm))
            return Task.CompletedTask;

        _reporting = true;
        try
        {
            var request = new ReportRequest
            {
                ModId = modId,
                ModName = modName,
                Source = source,
                Category = picked.Value.Category,
                Notes = picked.Value.Notes,
                AppVersion = AppVersion
            };
            if (!ReportRequest.TryValidate(request, out var error))
            {
                AppendLog($"{Ui.ReportFailed}：{error}");
                _notify(Ui.ReportFailed);
                return Task.CompletedTask;
            }

            var compose = GitHubCommunityLinks.BuildReportCompose(
                request.ModId,
                request.ModName,
                request.Source,
                request.Category,
                request.Notes,
                request.AppVersion);

            NotifyComposeResult(OpenCompose(compose.Subject, compose.Body));
        }
        catch (Exception ex)
        {
            AppendLog($"{Ui.ReportFailed}：{ex.Message}");
            _notify(Ui.ReportFailed);
        }
        finally
        {
            _reporting = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    void SubmitMod()
    {
        if (_promptSubmitGuide is not null)
        {
            if (!_promptSubmitGuide())
                return;
        }
        else if (!Confirm(Ui.SubmitModConfirm))
        {
            return;
        }

        var compose = GitHubCommunityLinks.BuildSubmitCompose();
        NotifyComposeResult(OpenCompose(compose.Subject, compose.Body));
    }

    [RelayCommand]
    void SendFeedback()
    {
        var compose = GitHubCommunityLinks.BuildFeedbackCompose();
        NotifyComposeResult(OpenCompose(compose.Subject, compose.Body));
    }

    (bool opened, MailProvider? provider) OpenCompose(string subject, string body)
    {
        var picked = _promptMailProvider?.Invoke();
        var (opened, _) = GitHubCommunityLinks.TryOpenCompose(subject, body, TryCopyText, picked);
        return (opened, picked);
    }

    string DescribeCompose((bool opened, MailProvider? provider) result)
    {
        var (opened, provider) = result;
        if (provider is null)
            return Ui.MailCancelled;
        if (!opened)
            return $"{Ui.MailOpenFailed}：{GitHubCommunityLinks.Inbox}";
        return provider == MailProvider.Qq ? Ui.MailOpenedQq : Ui.MailOpenedGmail;
    }

    void NotifyComposeResult((bool opened, MailProvider? provider) result)
    {
        var msg = DescribeCompose(result);
        if (result.provider is not null)
            AppendLog($"{msg}\n{GitHubCommunityLinks.Inbox}");
        else
            AppendLog(msg);
        _notify(msg);
    }

    [RelayCommand]
    void PureGameCleanup()
    {
        if (IsBranchSwitchBusy || IsSessionLocked)
        {
            _notify(LocalizationService.T("NotifySessionLocked"));
            return;
        }

        if (TryNotifySteamWritingAndAbort())
            return;

        var cfg = LoadConfig();
        var branch = _branchSwitch.LoadConfig();
        var request = new PureGameCleanupRequest
        {
            ConfigGamePath = string.IsNullOrWhiteSpace(GamePath) ? cfg.GamePath : GamePath,
            Branch = branch,
            ManagerAppDataRoot = _paths.DataRoot,
            ConfirmDeleteOtherStore = false
        };

        var plan = PureGameCleanupPlanner.Build(request);
        if (plan.IsAborted)
        {
            AppendLog(plan.AbortReason!);
            _notify(string.Format(LocalizationService.T("NotifyPureGameCleanupFailed"), plan.AbortReason));
            return;
        }

        var summary = string.Join("\n", plan.Actions.Select(a => $"• {a.Kind}: {a.Path}"));
        var body = LocalizationService.T("ConfirmPureGameCleanup") + "\n\n" + summary;
        var optionLabel = BranchSwitchEnabled
            ? LocalizationService.T("ConfirmPureGameCleanupDeleteOtherOption")
            : null;

        bool deleteOther;
        if (_promptConfirmOption is not null)
        {
            var picked = _promptConfirmOption(body, optionLabel);
            if (picked is null || !picked.Value.ok)
                return;
            deleteOther = BranchSwitchEnabled && picked.Value.option;
        }
        else
        {
            if (!Confirm(body, MessageBoxResult.No))
                return;
            deleteOther = false;
        }

        if (deleteOther)
        {
            request = new PureGameCleanupRequest
            {
                ConfigGamePath = request.ConfigGamePath,
                Branch = request.Branch,
                ManagerAppDataRoot = request.ManagerAppDataRoot,
                ConfirmDeleteOtherStore = true
            };
            plan = PureGameCleanupPlanner.Build(request);
            if (plan.IsAborted)
            {
                AppendLog(plan.AbortReason!);
                _notify(string.Format(LocalizationService.T("NotifyPureGameCleanupFailed"), plan.AbortReason));
                return;
            }
        }

        try
        {
            var executor = new PureGameCleanupExecutor(_processProbe, _branchSwitch, AppendLog);
            var result = executor.Execute(request, dryRun: false);
            if (!result.Success)
            {
                _notify(string.Format(LocalizationService.T("NotifyPureGameCleanupFailed"), result.Message));
                return;
            }

            _notify(LocalizationService.T("NotifyPureGameCleanupDone"));
            Application.Current?.Shutdown(0);
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message);
            _notify(string.Format(LocalizationService.T("NotifyPureGameCleanupFailed"), ex.Message));
        }
    }

    [RelayCommand]
    void ExportDiagnostics()
    {
        if (_exportingDiagnostics) return;

        var mode = _promptExportDiagnostics?.Invoke();
        if (mode is null) return;

        var defaultName = $"MechabellumModManager-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var zipPath = _saveZipFile?.Invoke(defaultName);
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        _exportingDiagnostics = true;
        // Refresh + hollow degrade first so summary/probe share one truth (H3).
        RefreshStatusCore(offerAssemblyGeneratePrompt: false);
        var nestedUnderSticky = _taskProgress.Begin(
            ManagerTaskKind.DiagnosticsExport,
            LocalizationService.T("TaskTitleDiagnostics"),
            LocalizationService.T("TaskMessageDiagnostics"),
            sessionLock: false);
        NotifyTaskProgress();
        try
        {
            var service = new DiagnosticsExportService();
            var result = service.ExportToFile(zipPath, new DiagnosticsExportRequest
            {
                Paths = _paths,
                GamePath = GamePath ?? "",
                SessionLogText = LogText ?? "",
                AppVersion = AppVersion ?? "",
                GameStatusKind = GameStatus?.Kind.ToString(),
                GameStatusMessage = GameStatus?.Message,
                BranchSwitchEnabled = BranchSwitchEnabled,
                BranchWizardStep = BranchWizardStep.ToString(),
                ActiveGameBranch = ActiveGameBranch.ToString(),
                LaunchMode = LaunchMode.ToString(),
                GameRunning = _processProbe.IsGameRunning(),
                SteamRunning = _processProbe.IsSteamRunning(),
                IsAwaitingSteamSettle = IsAwaitingSteamSettle,
                Diagnosis = CurrentDiagnosis,
                Redaction = mode.Value,
                LogWriter = _managerLog
            });

            if (!result.Success)
            {
                var fail = string.IsNullOrWhiteSpace(result.Message)
                    ? Ui.ExportDiagnosticsFailed
                    : $"{Ui.ExportDiagnosticsFailed}：{result.Message}";
                AppendLog(fail);
                _notify(fail);
                return;
            }

            _revealInExplorer?.Invoke(result.ZipPath ?? zipPath);

            var saved = string.Format(Ui.ExportDiagnosticsSaved, result.ZipPath ?? zipPath);
            var summaryHint = LocalizationService.T("ExportDiagnosticsSummaryHint");
            AppendLog(saved);
            AppendLog(summaryHint);

            var compose = GitHubCommunityLinks.BuildDiagnosticsCompose(AppVersion, mode.Value);
            var mailMsg = DescribeCompose(OpenCompose(compose.Subject, compose.Body));
            AppendLog(mailMsg);
            _notify($"{saved}\n{summaryHint}\n{mailMsg}");
        }
        catch (Exception ex)
        {
            AppendLog($"{Ui.ExportDiagnosticsFailed}：{ex.Message}");
            _notify(Ui.ExportDiagnosticsFailed);
        }
        finally
        {
            _exportingDiagnostics = false;
            // Sticky wizard/settle nests DiagnosticsExport without changing Kind — must Complete(),
            // not "Clear only when Kind==DiagnosticsExport" (left packaging message stuck).
            if (nestedUnderSticky || _taskProgress.IsNested)
                _taskProgress.Complete();
            else if (_taskProgress.Kind == ManagerTaskKind.DiagnosticsExport)
                _taskProgress.Clear();
            SyncStickyBranchTaskStrip();
            NotifyTaskProgress();
        }
    }

    [RelayCommand]
    void ShowCatalogPage()
    {
        ActiveContentPage = MainContentPage.Catalog;
        if (CatalogMods.Count == 0)
            _ = RefreshCatalogAsync();
    }

    [RelayCommand]
    async Task RefreshCatalogAsync()
    {
        if (_checkingCatalog) return;
        if (_addingCatalogMod)
        {
            AppendLog("正在加入本地库，已跳过目录刷新。");
            CatalogStatus = "加入本地库进行中，暂不刷新目录。";
            return;
        }
        _checkingCatalog = true;
        CatalogStatus = "正在拉取目录…";
        AppendLog("正在拉取 Mod 目录…");
        _taskProgress.Begin(
            ManagerTaskKind.CatalogRefresh,
            LocalizationService.T("TaskTitleCatalogRefresh"),
            LocalizationService.T("TaskMessageCatalogRefresh"),
            sessionLock: false);
        NotifyTaskProgress();
        try
        {
            CatalogRoot root;
            var fromCache = false;
            try
            {
                root = await _catalog.FetchCatalogAsync().ConfigureAwait(true);
            }
            catch (Exception networkEx)
            {
                var cached = _catalog.TryLoadCachedCatalog();
                if (cached is null)
                    throw new InvalidOperationException(networkEx.Message, networkEx);
                root = cached;
                fromCache = true;
                RecordEvent(ManagerEventLog.CatalogFetchFailed, new Dictionary<string, string?>
                {
                    ["source"] = _catalog.LastFetchSource ?? "github",
                    ["fallback"] = "cache",
                    ["error"] = networkEx.Message
                });
                AppendLog(CatalogNetworkHint.Format(networkEx.Message, _catalog.MirrorBaseUrl, usedCache: true));
                RecalculateDiagnosis();
            }

            ApplyCatalogRoot(root);

            SelectedCatalogMod = null;
            SetCatalogSelection(Array.Empty<CatalogModItemViewModel>());
            _unselectCatalog?.Invoke();

            if (SelectedLibraryMod is not null)
                UpdateLibraryModDetail();

            var updated = string.IsNullOrWhiteSpace(root.UpdatedAt) ? "未知" : root.UpdatedAt;
            var source = fromCache ? "cache" : (_catalog.LastFetchSource ?? "github");
            CatalogStatus = fromCache
                ? string.Format(LocalizationService.T("CatalogStatusOfflineCache"), CatalogMods.Count, updated)
                : $"已加载 {CatalogMods.Count} 个条目（目录更新：{updated}）";
            AppendLog(CatalogStatus);
            AppendLog(string.Format(LocalizationService.T("RemoteSourceLog"), source));
        }
        catch (Exception ex)
        {
            CatalogStatus = CatalogNetworkHint.Format(ex.Message, _catalog.MirrorBaseUrl, usedCache: false);
            RecordEvent(ManagerEventLog.CatalogFetchFailed, new Dictionary<string, string?>
            {
                ["source"] = _catalog.LastFetchSource ?? (_catalog.MirrorBaseUrl is null ? "github" : "mirror"),
                ["error"] = ex.Message
            });
            AppendLog(CatalogStatus);
            RecalculateDiagnosis();
        }
        finally
        {
            _checkingCatalog = false;
            if (_taskProgress.Kind == ManagerTaskKind.CatalogRefresh)
            {
                _taskProgress.Clear();
                SyncStickyBranchTaskStrip();
                NotifyTaskProgress();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddCatalogMod))]
    async Task AddCatalogModToLibraryAsync()
    {
        if (_addingCatalogMod) return;
        var targets = _catalogSelection.Where(IsAddOrUpdateTarget).ToList();
        if (targets.Count == 0) return;

        var skippedInLibrary = _catalogSelection.Count - targets.Count;
        if (skippedInLibrary > 0)
            AppendLog($"已跳过 {skippedInLibrary} 个已是最新版本的目录项。");

        await RunCatalogDownloadAsync(async () =>
        {
            foreach (var item in targets)
                await AddOneCatalogModAsync(item).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    /// <summary>
    /// Per-row update from the installed list. Maps back to the catalog entry and reuses the same
    /// swap transaction as the catalog panel — including the game-running gate inside
    /// <see cref="AddOneCatalogModAsync"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateMod))]
    async Task UpdateModAsync(ModItemViewModel? item)
    {
        if (item is null || item.IsMissing || !item.HasUpdate || _addingCatalogMod)
            return;

        var match = FindCatalogMatch(item);
        if (match is null || !match.HasUpdate)
        {
            AppendLog($"「{item.DisplayName}」在目录里找不到可更新的对应项。");
            return;
        }

        await RunCatalogDownloadAsync(async () =>
        {
            await AddOneCatalogModAsync(match).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    bool CanUpdateMod(ModItemViewModel? item) =>
        item is { IsMissing: false, HasUpdate: true } && !_addingCatalogMod;

    async Task RunCatalogDownloadAsync(Func<Task> work)
    {
        _addingCatalogMod = true;
        AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
        UpdateModCommand.NotifyCanExecuteChanged();
        _taskProgress.Begin(
            ManagerTaskKind.CatalogDownload,
            LocalizationService.T("TaskTitleCatalogDownload"),
            LocalizationService.T("TaskMessageCatalogDownload"),
            sessionLock: false);
        NotifyTaskProgress();
        try
        {
            await work().ConfigureAwait(true);
        }
        finally
        {
            _addingCatalogMod = false;
            AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
            UpdateModCommand.NotifyCanExecuteChanged();
            if (_taskProgress.Kind == ManagerTaskKind.CatalogDownload)
            {
                _taskProgress.Clear();
                SyncStickyBranchTaskStrip();
                NotifyTaskProgress();
            }
        }
    }

    async Task AddOneCatalogModAsync(CatalogModItemViewModel item)
    {
        try
        {
            if (item.IsInLibrary && !item.HasUpdate)
            {
                AppendLog($"「{item.Name}」已是最新版本，跳过下载。");
                CatalogStatus = $"已在本地库：{item.Name}";
                return;
            }

            var isUpdate = item.HasUpdate;
            if (isUpdate && !CanReplaceInstalledMod(item.Name))
                return;

            // Pre-check name keywords; RiskHeuristic still runs on ImportDll / ReloadMods.
            var probe = new ModPackage
            {
                Id = item.Id,
                DisplayName = item.Mod.Name,
                Author = item.Author,
                Files =
                {
                    new DeployableFile
                    {
                        RelativePathInPackage = Path.GetFileName((item.File ?? "").Replace('\\', '/'))
                    }
                }
            };
            var risk = _riskHeuristic.Evaluate(probe);
            if (risk.HighRisk &&
                !_confirmHighRisk(
                    $"「{item.Name}」命中高风险关键词「{risk.MatchedKeyword}」。确定仅加入本地库（不会自动启用）？"))
            {
                AppendLog($"已取消加入高风险目录项：{item.Name}");
                return;
            }

            var fileName = Path.GetFileName((item.File ?? "").Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = item.Id + ".dll";

            var tempPath = Path.Combine(Path.GetTempPath(), "mmm-catalog-" + Guid.NewGuid().ToString("N"), fileName);
            var verb = isUpdate ? "正在更新" : "正在下载";
            CatalogStatus = $"{verb} {item.Name}…";
            AppendLog(CatalogStatus);
            var supersededPackages = isUpdate
                ? ModCatalogService.FindInstalled(_library.List(), item.Mod)
                : Array.Empty<ModPackage>();

            IsCatalogDownloading = true;
            CatalogDownloadPercent = 0;
            IsCatalogDownloadIndeterminate = true;
            var reporter = new Progress<DownloadProgress>(p =>
            {
                IsCatalogDownloadIndeterminate = p.Fraction is null;
                CatalogDownloadPercent = (p.Fraction ?? 0) * 100;
                CatalogStatus = p.TotalBytes > 0
                    ? $"{verb} {item.Name}… {ByteSize.Format(p.BytesDownloaded)} / {ByteSize.Format(p.TotalBytes)}"
                    : $"{verb} {item.Name}… {ByteSize.Format(p.BytesDownloaded)}";
            });

            try
            {
                await _catalog.DownloadModAsync(item.Mod, tempPath, reporter).ConfigureAwait(true);
            }
            finally
            {
                IsCatalogDownloading = false;
            }

            if (!string.IsNullOrWhiteSpace(_catalog.LastDownloadSource))
                AppendLog(string.Format(LocalizationService.T("RemoteSourceLog"), _catalog.LastDownloadSource));

            try
            {
                var forceType = ModCatalogService.ParsePackageType(item.Type);
                var pkg = _library.ImportDll(tempPath, forceType);
                try
                {
                    pkg = _library.UpdatePackageMetadata(
                        pkg.Id,
                        author: item.Author,
                        version: item.Version,
                        summary: item.Mod.Summary,
                        catalogUpdatedAt: item.UpdatedAt,
                        preview: item.Mod.Preview,
                        catalogId: item.Id);
                }
                catch (Exception metaEx)
                {
                    AppendLog($"写入目录元数据失败：{metaEx.Message}");
                }

                if (isUpdate)
                {
                    RetireSupersededPackages(supersededPackages, pkg);
                    AppendLog($"已更新到目录版本：{pkg.DisplayName} ({pkg.Id})");
                    CatalogStatus = $"已更新：{pkg.DisplayName}，请重新应用方案使其生效。";
                }
                else
                {
                    var enabled = TryEnableImportedPackages(pkg);
                    AppendLog(enabled
                        ? $"已从目录加入本地库并启用：{pkg.DisplayName} ({pkg.Id})"
                        : $"已从目录加入本地库：{pkg.DisplayName} ({pkg.Id})（未启用）");
                    CatalogStatus = $"已加入本地库：{pkg.DisplayName}";
                }

                ReloadMods();
                RefreshCatalogInLibraryFlags();
                EnrichModsFromCatalog();
                UpdateLoaderVersionWarning();
                UpdateFirstAssemblyWarning();
                RecomputeDirty();
                SelectedLibraryMod = Mods.FirstOrDefault(m =>
                    string.Equals(m.Package.Id, pkg.Id, StringComparison.OrdinalIgnoreCase));
                UpdateLibraryModDetail();
                // UpdateLibraryModDetail may flip HasUpdate via enrichment; keep the badge honest.
                OutdatedCount = Mods.Count(m => !m.IsMissing && m.HasUpdate);
            }
            finally
            {
                try
                {
                    var dir = Path.GetDirectoryName(tempPath);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // best-effort temp cleanup
                }
            }
        }
        catch (Exception ex)
        {
            CatalogStatus = $"加入本地库失败：{ex.Message}";
            AppendLog(CatalogStatus);
        }
    }

    /// <summary>
    /// Replacing a mod rewrites the library and every profile that references it, so it must not
    /// race a deploy or a live game that has the old DLL loaded.
    /// </summary>
    bool CanReplaceInstalledMod(string modName)
    {
        if (IsSessionLocked || _taskProgress.Kind == ManagerTaskKind.Deploy)
        {
            _notify(LocalizationService.T("NotifyModUpdateBusy"));
            AppendLog($"更新「{modName}」已取消：有正在进行的关键操作。");
            return false;
        }

        if (_processProbe.IsGameRunning())
        {
            _notify(LocalizationService.T("NotifyModUpdateCloseGame"));
            AppendLog($"更新「{modName}」已取消：游戏正在运行。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Points the profiles at the freshly imported package before dropping the old one, so a failure
    /// mid-way leaves the player with a working mod rather than an empty slot.
    /// </summary>
    void RetireSupersededPackages(IReadOnlyList<ModPackage> superseded, ModPackage replacement)
    {
        foreach (var old in superseded)
        {
            if (string.Equals(old.Id, replacement.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                _profiles.ReplacePackageInAllProfiles(old.Id, replacement.Id);
            }
            catch (Exception ex)
            {
                AppendLog($"方案改指到新版本失败：{old.Id} → {replacement.Id}（{ex.Message}）");
                continue;
            }

            try
            {
                _library.Delete(old.Id);
                AppendLog($"已移除旧版本：{old.DisplayName} ({old.Id})");
            }
            catch (Exception ex)
            {
                AppendLog($"旧版本已停用但未能删除：{old.Id}（{ex.Message}）");
            }
        }
    }

    bool CanAddCatalogMod() =>
        !_addingCatalogMod &&
        _catalogSelection.Any(IsAddOrUpdateTarget);

    static bool IsAddOrUpdateTarget(CatalogModItemViewModel item) =>
        !item.IsInLibrary || item.HasUpdate;

    public void SetCatalogSelection(IReadOnlyList<CatalogModItemViewModel> items)
    {
        _catalogSelection.Clear();
        _catalogSelection.AddRange(items);
        CatalogSelectionCount = _catalogSelection.Count;
        OnPropertyChanged(nameof(CatalogSelectionCount));
        AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCatalogModChanged(CatalogModItemViewModel? value)
    {
        _ = value?.LoadPreviewImageAsync();
    }

    partial void OnSelectedLibraryModChanged(ModItemViewModel? value)
    {
        EditLibraryModTaxonomyCommand.NotifyCanExecuteChanged();
        UpdateLibraryModDetail();
    }

    [RelayCommand]
    void ClearLibraryModSelection()
    {
        _unselectLibrary?.Invoke();
        SelectedLibraryMod = null;
        LibrarySelectionCount = 0;
    }

    [RelayCommand]
    void ClearCatalogModSelection()
    {
        _unselectCatalog?.Invoke();
        SelectedCatalogMod = null;
        SetCatalogSelection(Array.Empty<CatalogModItemViewModel>());
    }

    void UpdateLibraryModDetail()
    {
        var item = SelectedLibraryMod;
        if (item is null || item.IsMissing)
            return;

        var catalog = FindCatalogMatch(item);
        if (catalog is null && CatalogMods.Count == 0)
        {
            // Soft refresh once so local library detail can resolve previews without opening the panel.
            _ = SoftRefreshCatalogForLibraryDetailAsync();
            catalog = FindCatalogMatch(item);
        }

        if (catalog is not null)
            item.ApplyCatalogEnrichment(catalog.Mod);

        var previewUrl = catalog?.PreviewUrl
            ?? ModCatalogService.TryGetRawUrl(item.Package.Preview);
        _ = item.LoadPreviewImageAsync(previewUrl);
    }

    async Task SoftRefreshCatalogForLibraryDetailAsync()
    {
        if (_checkingCatalog || _addingCatalogMod || CatalogMods.Count > 0) return;
        _checkingCatalog = true;
        try
        {
            var root = await _catalog.FetchCatalogAsync().ConfigureAwait(true);
            ApplyCatalogRoot(root);
            if (SelectedLibraryMod is not null)
                UpdateLibraryModDetail();
        }
        catch
        {
            // Soft refresh: leave existing library detail as-is on network failure.
        }
        finally
        {
            _checkingCatalog = false;
        }
    }

    /// <summary>
    /// Rebuilds the catalog list from a freshly fetched root and pushes what it says back onto
    /// the installed rows. Enrichment is the only thing that lights up the library's status
    /// column, so any path that fetches a catalog has to come through here or the fetch is
    /// invisible to the player.
    /// </summary>
    void ApplyCatalogRoot(CatalogRoot root)
    {
        RunOnUiThread(() => ApplyCatalogRootCore(root));
    }

    void ApplyCatalogRootCore(CatalogRoot root)
    {
        var packages = _library.List();
        CatalogMods.Clear();
        foreach (var mod in root.Mods)
        {
            LogInvalidCatalogCategory(mod);
            var state = ModCatalogService.GetEntryState(packages, mod);
            CatalogMods.Add(new CatalogModItemViewModel(mod, state, _catalog.MirrorBaseUrl));
        }

        EnrichModsFromCatalog();
        RefreshCatalogView();
        RefreshLibraryView();
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the dispatcher that owns the catalog/library views when
    /// a WPF application is pumping messages. Headless (unit-test) callers run inline — collection
    /// synchronization is enabled at construction so CollectionView accepts the mutation.
    /// </summary>
    void RunOnUiThread(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        // Real app: views live on the UI dispatcher (same as CurrentDispatcher at VM ctor).
        // Headless / wrong-affinity: run inline — EnableCollectionSynchronization covers Clear/Add.
        var appDispatcher = Application.Current?.Dispatcher;
        if (appDispatcher is not null &&
            ReferenceEquals(appDispatcher, _uiDispatcher) &&
            !appDispatcher.HasShutdownStarted &&
            !appDispatcher.HasShutdownFinished)
        {
            try
            {
                appDispatcher.Invoke(action);
                return;
            }
            catch (TaskCanceledException)
            {
                // Dispatcher aborted mid-shutdown — fall through to inline.
            }
        }

        action();
    }

    CatalogModItemViewModel? FindCatalogMatch(ModItemViewModel item)
    {
        if (item.IsMissing)
            return null;

        // Must be the same judgement GetEntryState uses. When these two disagreed, the row could
        // be enriched from -- and updated to -- an entry the state column was not talking about.
        foreach (var catalog in CatalogMods)
        {
            if (ModCatalogService.Matches(item.Package, catalog.Mod))
                return catalog;
        }

        return null;
    }

    void EnrichModsFromCatalog()
    {
        // OutdatedCount must be recomputed even when the catalog is empty: ReloadMods otherwise
        // leaves a stale badge after the player clears or fails to load the catalog.
        if (CatalogMods.Count == 0)
        {
            foreach (var mod in Mods)
            {
                if (!mod.HasUpdate && mod.LatestVersion is null && !mod.CatalogMatched) continue;
                mod.HasUpdate = false;
                mod.LatestVersion = null;
                mod.CatalogMatched = false;
            }
        }
        else
        {
            foreach (var mod in Mods)
            {
                if (mod.IsMissing) continue;
                var match = FindCatalogMatch(mod);
                if (match is null) continue;
                mod.ApplyCatalogEnrichment(match.Mod);
            }
        }

        OutdatedCount = Mods.Count(m => !m.IsMissing && m.HasUpdate);
        UpdateModCommand.NotifyCanExecuteChanged();
    }

    void RefreshCatalogInLibraryFlags()
    {
        var packages = _library.List();
        foreach (var item in CatalogMods)
            item.State = ModCatalogService.GetEntryState(packages, item.Mod);
    }

    [RelayCommand]
    async Task CheckForUpdatesAsync()
    {
        var level = EvaluateCloseOrUpdateGate(busyDialogOpen: false);
        if (level == CriticalOpGateLevel.HardBlock)
        {
            _notify(LocalizationService.T("CriticalOpBlockUpdate"));
            return;
        }

        if (level == CriticalOpGateLevel.SoftConfirm)
        {
            var prompt = string.Format(
                LocalizationService.T("CriticalOpSoftUpdateConfirm"),
                SoftGateStateLabel());
            if (!_confirm(prompt))
                return;
            AppendLog("User accepted soft update risk during settle/wait.");
        }

        if (_checkingUpdates) return;
        _checkingUpdates = true;
        UpdateStatus = "正在检查更新…";
        AppendLog("正在检查更新…");
        _taskProgress.Begin(
            ManagerTaskKind.CheckUpdate,
            LocalizationService.T("TaskTitleCheckUpdate"),
            LocalizationService.T("TaskMessageCheckUpdate"),
            sessionLock: false);
        NotifyTaskProgress();
        try
        {
            var result = await _updateChecker.CheckAsync().ConfigureAwait(true);
            UpdateStatus = result.Message;
            AppendLog(result.Message);
            if (!string.IsNullOrWhiteSpace(result.Source))
                AppendLog(string.Format(LocalizationService.T("RemoteSourceLog"), result.Source));

            if (result.Kind == UpdateCheckKind.UpdateAvailable && !string.IsNullOrWhiteSpace(result.SetupUrl))
            {
                var detail = string.Format(
                    LocalizationService.T("ConfirmOpenDownloadLink"),
                    result.Message,
                    result.Notes ?? "",
                    result.SetupUrl);
                if (Confirm(detail))
                    TryOpenUrl(result.SetupUrl!);
            }
            else if (result.Kind == UpdateCheckKind.Failed)
            {
                RecordEvent(ManagerEventLog.UpdateCheckFailed, new Dictionary<string, string?>
                {
                    ["source"] = result.Source ?? "github",
                    ["error"] = result.Message
                });
                RecalculateDiagnosis();
                var hint = LocalizationService.T("UpdateFailedDomesticHint");
                var prompt = string.Format(LocalizationService.T("ConfirmUpdateFailedDomestic"), result.Message);
                if (Confirm(prompt))
                {
                    TryCopyText(hint);
                    AppendLog(hint);
                    UpdateStatus = LocalizationService.T("UpdateFailedCopiedHint");
                }
                else if (Confirm(LocalizationService.T("ConfirmStillOpenGitHubReleases")))
                {
                    TryOpenUrl($"https://github.com/{UpdateChecker.Owner}/{UpdateChecker.Repo}/releases/latest");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"检查更新失败：{ex.Message}";
            RecordEvent(ManagerEventLog.UpdateCheckFailed, new Dictionary<string, string?>
            {
                ["error"] = ex.Message
            });
            AppendLog(UpdateStatus);
            RecalculateDiagnosis();
        }
        finally
        {
            _checkingUpdates = false;
            if (_taskProgress.Kind == ManagerTaskKind.CheckUpdate)
            {
                _taskProgress.Clear();
                SyncStickyBranchTaskStrip();
                NotifyTaskProgress();
            }
        }
    }

    bool TryOpenUrl(string url)
    {
        if (!ExternalUrlPolicy.TryParse(url, out var safe) || safe is null)
        {
            AppendLog(string.Format(LocalizationService.T("LogBlockedUnsafeUrl"), url));
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = safe.AbsoluteUri, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    void TryCopyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            if (_copyText is not null)
                _copyText(text);
            else
                System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard may be locked; URL is still in the sync log.
        }
    }

    [RelayCommand]
    void CreateProfile()
    {
        var name = _promptText?.Invoke("新方案名称");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var created = _profiles.Create(name);
            ReloadProfiles(selectId: created.Id);
            AppendLog($"已创建方案：{created.Name}");
        }
        catch (Exception ex)
        {
            AppendLog($"创建方案失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void RenameProfile()
    {
        if (SelectedProfile is null) return;
        var name = _promptText?.Invoke("重命名方案");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            _profiles.Rename(SelectedProfile.Id, name);
            ReloadProfiles(selectId: SelectedProfile.Id);
            AppendLog($"已重命名方案：{name.Trim()}");
        }
        catch (Exception ex)
        {
            AppendLog($"重命名方案失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void DuplicateProfile()
    {
        if (SelectedProfile is null) return;
        var name = _promptText?.Invoke("复制为新方案");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var copy = _profiles.Duplicate(SelectedProfile.Id, name);
            ReloadProfiles(selectId: copy.Id);
            AppendLog($"已复制方案：{copy.Name}");
        }
        catch (Exception ex)
        {
            AppendLog($"复制方案失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        if (!Confirm(string.Format(LocalizationService.T("ConfirmDeleteProfile"), SelectedProfile.Name)))
            return;
        try
        {
            var id = SelectedProfile.Id;
            var name = SelectedProfile.Name;
            _profiles.Delete(id);
            var config = LoadConfig();
            ReloadProfiles(selectId: config.ActiveProfileId);
            AppendLog($"已删除方案：{name}");
        }
        catch (Exception ex)
        {
            AppendLog($"删除方案失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void DeleteMod(ModItemViewModel? mod)
    {
        if (mod is null) return;
        if (!Confirm(string.Format(LocalizationService.T("ConfirmDeleteModFromLibrary"), mod.DisplayName)))
            return;
        try
        {
            _library.Delete(mod.Package.Id);
            ReloadMods();
            RecomputeDirty();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            AppendLog($"已删除 Mod：{mod.DisplayName}");
        }
        catch (Exception ex)
        {
            AppendLog($"删除 Mod 失败：{ex.Message}");
        }
    }

    [RelayCommand]
    void SelectProfile(ProfileItemViewModel? profile)
    {
        if (profile is null) return;
        SelectedProfile = profile;
    }

    [RelayCommand]
    void ToggleHighRisk(ModItemViewModel? mod)
    {
        if (mod is null || mod.IsMissing) return;
        mod.Package.HighRisk = !mod.Package.HighRisk;
        try
        {
            PersistPackageMeta(mod.Package);
            mod.NotifyRiskChanged();
            AppendLog($"{mod.DisplayName} 高风险标记（临时）：{(mod.Package.HighRisk ? "是" : "否")}；刷新列表后将按名称关键词重新判定。");
        }
        catch (Exception ex)
        {
            AppendLog($"更新高风险标记失败：{ex.Message}");
        }
    }

    /// <summary>
    /// Enable newly imported packages on the active profile (skips high-risk unless confirmed).
    /// Returns true if at least one package was enabled.
    /// </summary>
    bool TryEnableImportedPackages(params ModPackage[] packages) =>
        TryEnableImportedPackages((IEnumerable<ModPackage>)packages);

    bool TryEnableImportedPackages(IEnumerable<ModPackage> packages)
    {
        if (SelectedProfile is null) return false;
        var any = false;
        foreach (var pkg in packages)
        {
            if (pkg is null || string.IsNullOrWhiteSpace(pkg.Id)) continue;
            if (!_riskGate.CanEnable(pkg.HighRisk, _confirmHighRisk))
            {
                AppendLog($"已导入高风险 Mod 但未启用：{pkg.DisplayName}");
                continue;
            }

            try
            {
                _profiles.SetEnabled(SelectedProfile.Id, pkg.Id, true);
                any = true;
            }
            catch (Exception ex)
            {
                AppendLog($"自动启用 {pkg.DisplayName} 失败：{ex.Message}");
            }
        }

        return any;
    }

    public void OnModEnabledChanged(ModItemViewModel item, bool enabled)
    {
        if (SelectedProfile is null) return;

        if (enabled && !_riskGate.CanEnable(item.Package.HighRisk, _confirmHighRisk))
        {
            item.SetEnabledSilent(false);
            AppendLog("已取消启用高风险 Mod。");
            return;
        }

        try
        {
            _profiles.SetEnabled(SelectedProfile.Id, item.Package.Id, enabled);
            // Recompute from profile vs deploy manifest (includes enabled-id set, not only files).
            RecomputeDirty();
            UpdateLoaderVersionWarning();
            UpdateFirstAssemblyWarning();
            AppendLog($"{(enabled ? "启用" : "禁用")}：{item.DisplayName}");
        }
        catch (Exception ex)
        {
            item.SetEnabledSilent(!enabled);
            AppendLog($"更新启用状态失败：{ex.Message}");
        }
    }

    void ReloadProfiles(string? selectId)
    {
        Profiles.Clear();
        ProfileItemViewModel? selected = null;
        foreach (var profile in _profiles.List())
        {
            var item = new ProfileItemViewModel(profile);
            Profiles.Add(item);
            if (selectId is not null &&
                string.Equals(profile.Id, selectId, StringComparison.OrdinalIgnoreCase))
                selected = item;
        }

        SelectedProfile = selected ?? Profiles.FirstOrDefault();
    }

    void ReloadMods()
    {
        // CollectionView rejects SourceCollection changes off the creating dispatcher unless
        // collection synchronization is enabled. Catalog download resumes on the thread pool
        // (service ConfigureAwait(false)); ConfigureAwait(true) alone is not enough without a
        // SynchronizationContext. Prefer UI Invoke in the real app; run inline when headless.
        RunOnUiThread(ReloadModsCore);
    }

    void ReloadModsCore()
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (SelectedProfile is not null)
        {
            try
            {
                foreach (var id in _profiles.Get(SelectedProfile.Id).EnabledPackageIds)
                    enabled.Add(id);
            }
            catch
            {
                // profile missing
            }
        }

        var library = _library.List().ToList();
        foreach (var pkg in library)
        {
            var risk = _riskHeuristic.Evaluate(pkg);
            if (pkg.HighRisk == risk.HighRisk) continue;

            pkg.HighRisk = risk.HighRisk;
            try
            {
                PersistPackageMeta(pkg);
                AppendLog(risk.HighRisk
                    ? $"{pkg.DisplayName} 因关键词「{risk.MatchedKeyword}」自动标为高风险"
                    : $"{pkg.DisplayName} 未命中风险关键词，已取消高风险标记");
            }
            catch (Exception ex)
            {
                AppendLog($"更新 {pkg.DisplayName} 高风险标记失败：{ex.Message}");
            }
        }

        var libraryIds = new HashSet<string>(library.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
        var previousId = SelectedLibraryMod?.Package.Id;

        Mods.Clear();
        foreach (var pkg in library.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(pkg.CategoryOverride) &&
                !ModTaxonomy.TryParseCategory(pkg.CategoryOverride, out _))
            {
                AppendLog($"本地包 '{pkg.Id}': 无效分类覆盖 '{pkg.CategoryOverride}'，按目录/未分类处理。");
            }
            Mods.Add(new ModItemViewModel(this, pkg, enabled.Contains(pkg.Id)));
        }

        EnrichModsFromCatalog();

        var missing = enabled.Where(id => !libraryIds.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var id in missing)
            Mods.Add(ModItemViewModel.CreateMissing(this, id));

        SelectedLibraryMod = Mods.FirstOrDefault(m =>
            previousId is not null &&
            string.Equals(m.Package.Id, previousId, StringComparison.OrdinalIgnoreCase));

        if (missing.Count > 0)
        {
            var list = string.Join(", ", missing);
            MissingEnabledPackagesWarning = $"\u65b9\u6848\u4e2d\u6709\u7f3a\u5931\u7684\u5305\uff1a{list}";
            AppendLog(MissingEnabledPackagesWarning);
        }
        else
        {
            MissingEnabledPackagesWarning = "";
        }

        RefreshLibraryView();
        RefreshCatalogView();
    }

    void RebuildFilterOptionLabels()
    {
        _suppressFilterRefresh = true;
        try
        {
            var allLabel = Ui.FilterAll;
            var categoryOptions = new List<CategoryFilterOption>
            {
                new(null, allLabel)
            };
            foreach (var cat in ModTaxonomy.AllFilterCategories)
                categoryOptions.Add(new CategoryFilterOption(cat, Ui.CategoryLabel(cat)));

            CatalogCategoryFilterOptions = categoryOptions;
            LibraryCategoryFilterOptions = categoryOptions;
            OnPropertyChanged(nameof(CatalogCategoryFilterOptions));
            OnPropertyChanged(nameof(LibraryCategoryFilterOptions));

            SortModeOptions = new[]
            {
                new SortModeOption(ModSortMode.NameAsc, Ui.SortByName),
                new SortModeOption(ModSortMode.UpdatedAtDesc, Ui.SortByUpdatedAtDesc)
            };
            OnPropertyChanged(nameof(SortModeOptions));

            SelectedCatalogCategoryFilter ??= CatalogCategoryFilterOptions[0];
            SelectedLibraryCategoryFilter ??= LibraryCategoryFilterOptions[0];
            SelectedCatalogSortMode ??= SortModeOptions[0];
            SelectedLibrarySortMode ??= SortModeOptions[0];

            SelectedCatalogCategoryFilter = CatalogCategoryFilterOptions.FirstOrDefault(o =>
                o.Category == SelectedCatalogCategoryFilter?.Category) ?? CatalogCategoryFilterOptions[0];
            SelectedLibraryCategoryFilter = LibraryCategoryFilterOptions.FirstOrDefault(o =>
                o.Category == SelectedLibraryCategoryFilter?.Category) ?? LibraryCategoryFilterOptions[0];
            SelectedCatalogSortMode = SortModeOptions.FirstOrDefault(o =>
                o.Mode == SelectedCatalogSortMode?.Mode) ?? SortModeOptions[0];
            SelectedLibrarySortMode = SortModeOptions.FirstOrDefault(o =>
                o.Mode == SelectedLibrarySortMode?.Mode) ?? SortModeOptions[0];

            RebuildCatalogTagOptions();
            RebuildLibraryTagOptions();
        }
        finally
        {
            _suppressFilterRefresh = false;
        }

        RefreshCatalogView();
        RefreshLibraryView();
    }

    void RebuildCatalogTagOptions()
    {
        var previous = SelectedCatalogTagFilter?.Tag;
        var tags = CatalogMods
            .SelectMany(m => m.EffectiveTags)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => ModTaxonomy.GetTagDisplayName(t), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        CatalogAvailableTagOptions.Clear();
        CatalogAvailableTagOptions.Add(new TagFilterOption(null, Ui.FilterAll));
        foreach (var tag in tags)
            CatalogAvailableTagOptions.Add(new TagFilterOption(tag, ModTaxonomy.GetTagDisplayName(tag)));

        var suppress = _suppressFilterRefresh;
        _suppressFilterRefresh = true;
        try
        {
            SelectedCatalogTagFilter = CatalogAvailableTagOptions.FirstOrDefault(o =>
                string.Equals(o.Tag, previous, StringComparison.Ordinal))
                ?? CatalogAvailableTagOptions[0];
        }
        finally
        {
            _suppressFilterRefresh = suppress;
        }
    }

    void RebuildLibraryTagOptions()
    {
        var previous = SelectedLibraryTagFilter?.Tag;
        var tags = Mods
            .Where(m => !m.IsMissing)
            .SelectMany(m => m.EffectiveTags)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => ModTaxonomy.GetTagDisplayName(t), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        LibraryAvailableTagOptions.Clear();
        LibraryAvailableTagOptions.Add(new TagFilterOption(null, Ui.FilterAll));
        foreach (var tag in tags)
            LibraryAvailableTagOptions.Add(new TagFilterOption(tag, ModTaxonomy.GetTagDisplayName(tag)));

        var suppress = _suppressFilterRefresh;
        _suppressFilterRefresh = true;
        try
        {
            SelectedLibraryTagFilter = LibraryAvailableTagOptions.FirstOrDefault(o =>
                string.Equals(o.Tag, previous, StringComparison.Ordinal))
                ?? LibraryAvailableTagOptions[0];
        }
        finally
        {
            _suppressFilterRefresh = suppress;
        }
    }

    void RefreshCatalogView()
    {
        if (_suppressFilterRefresh) return;
        _suppressFilterRefresh = true;
        try
        {
            RebuildCatalogTagOptions();
            ApplySort(CatalogModsView, SelectedCatalogSortMode?.Mode ?? ModSortMode.NameAsc, catalog: true);
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
        CatalogModsView.Refresh();
    }

    void RefreshLibraryView()
    {
        if (_suppressFilterRefresh) return;
        _suppressFilterRefresh = true;
        try
        {
            RebuildLibraryTagOptions();
            ApplySort(LibraryModsView, SelectedLibrarySortMode?.Mode ?? ModSortMode.NameAsc, catalog: false);
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
        LibraryModsView.Refresh();
    }

    static void ApplySort(ICollectionView view, ModSortMode mode, bool catalog)
    {
        if (view is not ListCollectionView lcv) return;
        lcv.CustomSort = mode switch
        {
            ModSortMode.UpdatedAtDesc when catalog =>
                Comparer<object>.Create((a, b) =>
                {
                    var ca = (CatalogModItemViewModel)a!;
                    var cb = (CatalogModItemViewModel)b!;
                    var cmp = ModListFilter.CompareUpdatedAtDesc(ca.UpdatedAt, cb.UpdatedAt);
                    return cmp != 0
                        ? cmp
                        : string.Compare(ca.Name, cb.Name, StringComparison.CurrentCultureIgnoreCase);
                }),
            ModSortMode.UpdatedAtDesc =>
                Comparer<object>.Create((a, b) =>
                {
                    var ca = (ModItemViewModel)a!;
                    var cb = (ModItemViewModel)b!;
                    var cmp = ModListFilter.CompareUpdatedAtDesc(ca.CatalogUpdatedAt, cb.CatalogUpdatedAt);
                    return cmp != 0
                        ? cmp
                        : string.Compare(ca.DisplayName, cb.DisplayName, StringComparison.CurrentCultureIgnoreCase);
                }),
            _ when catalog =>
                Comparer<object>.Create((a, b) =>
                    string.Compare(
                        ((CatalogModItemViewModel)a!).Name,
                        ((CatalogModItemViewModel)b!).Name,
                        StringComparison.CurrentCultureIgnoreCase)),
            _ =>
                Comparer<object>.Create((a, b) =>
                    string.Compare(
                        ((ModItemViewModel)a!).DisplayName,
                        ((ModItemViewModel)b!).DisplayName,
                        StringComparison.CurrentCultureIgnoreCase))
        };
    }

    bool FilterCatalogItem(object obj)
    {
        if (obj is not CatalogModItemViewModel item) return false;
        if (!ModListFilter.MatchesSearch(
                CatalogSearchText, item.Name, item.Author, item.Summary, item.Id))
            return false;
        if (!ModListFilter.MatchesCategory(SelectedCatalogCategoryFilter?.Category, item.EffectiveCategory))
            return false;
        if (!ModListFilter.MatchesTag(SelectedCatalogTagFilter?.Tag, item.EffectiveTags))
            return false;
        return true;
    }

    bool FilterLibraryItem(object obj)
    {
        if (obj is not ModItemViewModel item) return false;
        if (!ModListFilter.MatchesSearch(
                LibrarySearchText, item.DisplayName, item.Author, item.Summary, item.Package.Id))
            return false;
        if (!ModListFilter.MatchesCategory(SelectedLibraryCategoryFilter?.Category, item.EffectiveCategory))
            return false;
        if (!ModListFilter.MatchesTag(SelectedLibraryTagFilter?.Tag, item.EffectiveTags))
            return false;
        return true;
    }

    void RecomputeDirty()
    {
        if (SelectedProfile is null)
        {
            IsDirty = false;
            return;
        }

        Profile profile;
        try
        {
            profile = _profiles.Get(SelectedProfile.Id);
        }
        catch
        {
            IsDirty = true;
            return;
        }

        var packages = _library.List().ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var desired = BuildDesiredEntries(profile, packages);
        var manifest = _store.LoadOrDefault(CurrentDeployManifestPath(), () => new DeployManifest());

        var desiredIds = profile.EnabledPackageIds
            .Where(id => packages.ContainsKey(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var actualIds = manifest.Files
            .Select(f => f.PackageId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.Equals(manifest.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            IsDirty = desiredIds.Count > 0 || desired.Count > 0 || manifest.Files.Count > 0;
            return;
        }

        if (!PathsEqual(manifest.GamePath, GamePath))
        {
            IsDirty = true;
            return;
        }

        if (desiredIds.Count != actualIds.Count ||
            !desiredIds.SequenceEqual(actualIds, StringComparer.OrdinalIgnoreCase))
        {
            IsDirty = true;
            return;
        }

        var actual = manifest.Files
            .Select(f => (Normalize(f.RelativePath), f.PackageId, f.Sha256 ?? ""))
            .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IsDirty = !desired.SequenceEqual(actual);
    }

    static List<(string Rel, string PackageId, string Sha)> BuildDesiredEntries(
        Profile profile,
        IReadOnlyDictionary<string, ModPackage> packages)
    {
        var list = new List<(string, string, string)>();
        foreach (var packageId in profile.EnabledPackageIds)
        {
            if (!packages.TryGetValue(packageId, out var package))
                continue;

            foreach (var file in package.Files)
            {
                string rel;
                try
                {
                    rel = DeployPlanner.MapRelativeGamePath(package.Type, file.RelativePathInPackage);
                }
                catch (InvalidOperationException)
                {
                    // Match deploy: rejected paths (e.g. UserData/Loader.cfg) are not desired files.
                    continue;
                }

                list.Add((Normalize(rel), package.Id, file.Sha256 ?? ""));
            }
        }

        return list
            .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    void UpdateLoaderVersionWarning()
    {
        var loader = GameStatus?.MelonLoaderVersion;
        if (string.IsNullOrWhiteSpace(loader))
        {
            LoaderVersionWarning = "";
            return;
        }

        var mismatches = Mods
            .Where(m => !string.IsNullOrWhiteSpace(m.RequiredMelonLoaderVersion)
                        && !VersionMatches(m.RequiredMelonLoaderVersion!, loader))
            .Select(m => $"{m.DisplayName} 需要 {m.RequiredMelonLoaderVersion}")
            .ToList();

        LoaderVersionWarning = mismatches.Count == 0
            ? ""
            : $"MelonLoader 版本可能不匹配（当前 {loader}）：{string.Join("；", mismatches)}";
    }


    void UpdateFirstAssemblyWarning()
    {
        if (GameStatus?.Kind is not (GameStatusKind.Ready or GameStatusKind.LoaderPartial))
        {
            FirstAssemblyWarning = "";
            return;
        }

        if (!_melonOptimizer.NeedsFirstAssemblyGeneration(GamePath))
        {
            FirstAssemblyWarning = "";
            return;
        }

        FirstAssemblyWarning =
            "检测到 MelonLoader 已安装，但尚未完成首次程序集生成（缺少 MelonLoader\\Il2CppAssemblies）。" +
            "首次启动游戏时可能需联网（部分网络需代理），黑屏或控制台滚动属正常，请耐心等待完成；" +
            "不要把其他电脑已初始化过的 MelonLoader 文件夹直接拷贝到本机。";
    }

    static bool VersionMatches(string required, string actual)
    {
        static string Norm(string v)
        {
            var s = v.Trim();
            // FileVersion often has 4 parts; compare by prefix ignoring trailing .0
            while (s.EndsWith(".0", StringComparison.Ordinal)) s = s[..^2];
            return s;
        }

        return string.Equals(Norm(required), Norm(actual), StringComparison.OrdinalIgnoreCase)
               || actual.StartsWith(required, StringComparison.OrdinalIgnoreCase)
               || required.StartsWith(actual, StringComparison.OrdinalIgnoreCase);
    }

    void PersistPackageMeta(ModPackage pkg)
    {
        var meta = new
        {
            id = pkg.Id,
            displayName = pkg.DisplayName,
            version = pkg.Version,
            author = pkg.Author,
            type = pkg.Type switch
            {
                ModPackageType.MelonMod => "melon_mod",
                ModPackageType.MelonPlugin => "melon_plugin",
                ModPackageType.MelonUserLibs => "melon_userlibs",
                ModPackageType.MelonUserData => "melon_userdata",
                _ => "melon_mod"
            },
            highRisk = pkg.HighRisk,
            requiredMelonLoaderVersion = pkg.RequiredMelonLoaderVersion,
            summary = pkg.Summary,
            catalogUpdatedAt = pkg.CatalogUpdatedAt,
            preview = pkg.Preview,
            categoryOverride = pkg.CategoryOverride,
            extraTags = pkg.ExtraTags is null ? null : ModTaxonomy.NormalizeTags(pkg.ExtraTags).ToList(),
            files = pkg.Files
        };
        var json = JsonSerializer.Serialize(meta, PackageJsonOptions);
        File.WriteAllText(Path.Combine(pkg.PackageDirectory, "package.json"), json);
    }

    void LoadBranchSwitchState()
    {
        _suppressBranchSwitchSave = true;
        try
        {
            try
            {
                _branchSwitch.TryRepairFromJournal();
            }
            catch
            {
                // Isolation: journal repair must not block loading branch-switch state.
            }

            var cfg = _branchSwitch.LoadConfig();
            BranchSwitchEnabled = cfg.Enabled;
            ActiveGameBranch = cfg.ActiveBranch;
            BetaBranchName = string.IsNullOrWhiteSpace(cfg.BetaBranchName)
                ? BranchSwitchConfig.DefaultSteamBetaBranchName
                : cfg.BetaBranchName;
            OfficialProfileId = string.IsNullOrWhiteSpace(cfg.OfficialProfileId) ? "default" : cfg.OfficialProfileId;
            BetaProfileId = string.IsNullOrWhiteSpace(cfg.BetaProfileId) ? "default" : cfg.BetaProfileId;
            BranchWizardStep = cfg.WizardStep;
            IsAwaitingSteamSettle = cfg.Enabled && cfg.WizardStep == BranchWizardStep.AwaitingSteamSettle;
            // Do not force DegradeToManualBeta on restore — that always shows "测试服" confirm
            // even when settling Official. Degrade is only set after silent-beta failure at runtime.
            DegradeToManualBeta = false;

            // Three-entry simplify: never resume mid-enable after restart — roll back sticky wizard.
            if (!cfg.Enabled
                && cfg.WizardStep is not BranchWizardStep.None and not BranchWizardStep.Ready)
            {
                try
                {
                    if (!_processProbe.IsGameOrSteamRunning())
                    {
                        _branchSwitch.TryRollbackEnableSession(
                            string.IsNullOrWhiteSpace(GamePath) ? cfg.SteamLinkPath : GamePath);
                    }
                    else
                    {
                        // Cannot move disks while Steam/game runs: clear sticky step (no resume)
                        // but KEEP SessionOwned* so Emergency can still delete session stores.
                        cfg.WizardStep = BranchWizardStep.None;
                        _branchSwitch.SaveConfig(cfg);
                        AppendLog(LocalizationService.T("LogEnableDualPendingEmergencySteamBusy"));
                    }
                }
                catch { /* emergency button remains */ }

                cfg = _branchSwitch.LoadConfig();
                BranchSwitchEnabled = cfg.Enabled;
                BranchWizardStep = cfg.WizardStep;
                IsAwaitingSteamSettle = false;
            }

            SelectBoundProfile(ActiveGameBranch);
            if (cfg.Enabled
                && !string.IsNullOrWhiteSpace(cfg.OfficialStorePath)
                && !string.IsNullOrWhiteSpace(cfg.BetaStorePath))
            {
                // Do not notify on every startup; only log if something changed/failed.
                try
                {
                    var sync = _melonDualSync.EnsureOnBothStores(cfg.OfficialStorePath, cfg.BetaStorePath);
                    if (!sync.Success
                        || (sync.Message?.Contains("安装", StringComparison.Ordinal) == true)
                        || (sync.Message?.Contains("复制", StringComparison.Ordinal) == true)
                        || (sync.Message?.Contains("警告", StringComparison.Ordinal) == true)
                        || (sync.Message?.Contains("失败", StringComparison.Ordinal) == true)
                        || (sync.Message?.Contains("播种", StringComparison.Ordinal) == true))
                        AppendLog(sync.Message);
                }
                catch (Exception ex)
                {
                    AppendLog($"启动时补齐双服 MelonLoader 失败：{ex.Message}");
                }
            }
            RecomputeDirty();
            RefreshBranchStatusText();
            NotifyBranchGates();
            // Poll starts from RefreshStatusCore / Resume — not during ctor Load.
        }
        catch
        {
            BranchSwitchEnabled = false;
            IsAwaitingSteamSettle = false;
            DegradeToManualBeta = false;
            BranchWizardStep = BranchWizardStep.None;
            StopWizardDownloadPoll();
            RefreshBranchStatusText();
            NotifyBranchGates();
        }
        finally
        {
            _suppressBranchSwitchSave = false;
        }
    }

    void BusyBegin(string message)
    {
        _taskProgress.Begin(
            ManagerTaskKind.GenericBusy,
            LocalizationService.T("BusyProgressTitle"),
            message,
            sessionLock: true,
            sticky: false);
        NotifyTaskProgress();
        _beginBusy?.Invoke(LocalizationService.T("BusyProgressTitle"), message);
    }

    void BusyMessage(string message)
    {
        _taskProgress.Report(message);
        NotifyTaskProgress();
        _setBusyMessage?.Invoke(message);
    }

    void BusyEnd()
    {
        _taskProgress.Complete();
        SyncStickyBranchTaskStrip();
        NotifyTaskProgress();
        _endBusy?.Invoke();
    }

    async Task RunBusyAsync(string initialMessage, Func<Task> work)
    {
        var prev = _busyWorkCts;
        var cts = new CancellationTokenSource();
        _busyWorkCts = cts;
        try { prev?.Dispose(); } catch { /* ignore */ }

        BusyBegin(initialMessage);
        try { await work().ConfigureAwait(true); }
        finally
        {
            BusyEnd();
            if (ReferenceEquals(_busyWorkCts, cts))
                _busyWorkCts = null;
            try { cts.Dispose(); } catch { /* ignore */ }
        }
    }

    CancellationToken BusyWorkToken => _busyWorkCts?.Token ?? CancellationToken.None;

    void NotifyTaskProgress()
    {
        OnPropertyChanged(nameof(HasCurrentTask));
        OnPropertyChanged(nameof(TaskTitle));
        OnPropertyChanged(nameof(TaskMessage));
        OnPropertyChanged(nameof(TaskProgressValue));
        OnPropertyChanged(nameof(IsTaskIndeterminate));
        OnPropertyChanged(nameof(ShowTaskPercent));
        OnPropertyChanged(nameof(TaskPercentLabel));
        OnPropertyChanged(nameof(IsSessionLocked));
        NotifyBranchGates();
    }

    void SyncStickyBranchTaskStrip()
    {
        if (IsAwaitingSteamSettle)
        {
            var title = LocalizationService.T("TaskTitleBranchSettle");
            var msg = string.Format(
                LocalizationService.T("TaskMessageBranchSettleFmt"),
                ActiveBranchDisplayName);
            if (_taskProgress.Kind == ManagerTaskKind.BranchSettle && _taskProgress.IsSticky)
                _taskProgress.Report(msg);
            else if (!_taskProgress.HasTask || _taskProgress.Kind is ManagerTaskKind.BranchWizard or ManagerTaskKind.None)
            {
                if (_taskProgress.HasTask && _taskProgress.Kind == ManagerTaskKind.BranchWizard)
                    _taskProgress.Clear();
                _taskProgress.Begin(ManagerTaskKind.BranchSettle, title, msg, sessionLock: true, sticky: true);
            }
            NotifyTaskProgress();
            return;
        }

        if (IsBranchWizardInProgress)
        {
            var title = LocalizationService.T("TaskTitleBranchWizard");
            var msg = BranchWizardStep switch
            {
                BranchWizardStep.WaitingDownloadB => ResolveWizardWaitingDownloadTaskMessage(),
                BranchWizardStep.AwaitingSteamSettle => LocalizationService.T("TaskMessageBranchSettle"),
                _ => LocalizationService.T("TaskMessageWizardInProgress")
            };
            if (_taskProgress.Kind == ManagerTaskKind.BranchWizard && _taskProgress.IsSticky)
                _taskProgress.Report(msg);
            else if (!_taskProgress.HasTask
                     || _taskProgress.Kind is ManagerTaskKind.None or ManagerTaskKind.BranchSettle)
            {
                if (_taskProgress.Kind == ManagerTaskKind.BranchSettle)
                    _taskProgress.Clear();
                _taskProgress.Begin(ManagerTaskKind.BranchWizard, title, msg, sessionLock: true, sticky: true);
            }
            NotifyTaskProgress();
            return;
        }

        if (_taskProgress.Kind is ManagerTaskKind.BranchWizard or ManagerTaskKind.BranchSettle)
        {
            _taskProgress.Clear();
            NotifyTaskProgress();
        }
    }

    async Task SwitchToBranchAsync(GameBranch target)
    {
        if (!BranchSwitchEnabled || IsBranchSwitchBusy) return;

        if (TryNotifySteamWritingAndAbort())
            return;

        try
        {
            var cfg = _branchSwitch.LoadConfig();
            var targetStore = target == GameBranch.Official ? cfg.OfficialStorePath : cfg.BetaStorePath;
            if (!SteamGameLocator.LooksLikeGameRoot(targetStore))
            {
                var msg = LocalizationService.T("NotifySwitchBlockedTargetHollow");
                AppendLog(msg);
                _notify(msg);
                return;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"切服前检查目标仓失败：{ex.Message}");
            return;
        }

        if (_branchSwitch.IsAlignedWith(target))
        {
            var linkPath = string.IsNullOrWhiteSpace(GamePath) ? _branchSwitch.LoadConfig().SteamLinkPath : GamePath;
            var detect = _detector.Detect(linkPath ?? "");
            if (detect.Kind == GameStatusKind.Ready)
            {
                var already = LocalizationService.T("NotifyAlreadyOnGameBranch");
                AppendLog(already);
                _notify(already);
                return;
            }

            var incomplete = LocalizationService.T("NotifyAlignedButGameIncomplete");
            AppendLog(incomplete);
            if (!string.IsNullOrWhiteSpace(detect.Message))
                AppendLog(detect.Message);
            AppendLog(LocalizationService.T("HintActiveStoreIncompleteSwitchOther"));
            _notify(incomplete);
            return;
        }

        if (!Confirm(LocalizationService.T("ConfirmSwitchGameBranch")))
            return;

        IsBranchSwitchBusy = true;
        try
        {
            BranchOperationResult? silentResult = null;
            await RunBusyAsync(LocalizationService.T("BusyWaitingSteam"), async () =>
            {
                if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
                    return;

                var detail = target == GameBranch.Beta ? "SwitchToBeta" : "SwitchToOfficial";
                await RunWithCriticalOpAsync(CriticalOpKind.BranchDiskWrite, detail, async () =>
                {
                    BusyMessage(LocalizationService.T("BusyMovingGameFolder"));

                    var leaveBranch = ActiveGameBranch;
                    var snap = _branchSwitch.TrySnapshotSettledAcf(leaveBranch);
                    if (!snap.Success && !string.IsNullOrWhiteSpace(snap.Message))
                        AppendLog($"切服前未保存 {leaveBranch} ACF 快照：{snap.Message}");

                    var swap = await Task.Run(() => _branchSwitch.TrySwapJunction(target)).ConfigureAwait(true);
                    if (!swap.Success)
                    {
                        AppendLog(MapBranchOperationFailure(swap.Message, LocalizationService.T("LogSwapFolderFailed")));
                        return;
                    }

                    BusyMessage(LocalizationService.T("BusySwitchingSteamBranch"));

                    _suppressBranchSwitchSave = true;
                    try
                    {
                        ActiveGameBranch = target;
                    }
                    finally
                    {
                        _suppressBranchSwitchSave = false;
                    }

                    ClearLastLaunchAfterStoreChange();
                    SelectBoundProfile(target);

                    silentResult = _branchSwitch.TryPrepareSteamBranchMetadata(target);
                    if (silentResult.Success
                        && string.Equals(silentResult.Message, "restored-acf-snapshot", StringComparison.Ordinal))
                    {
                        AppendLog("已恢复目标服 Steam 清单快照（避免重复下载）");
                    }
                }).ConfigureAwait(true);
            }).ConfigureAwait(true);

            if (silentResult is not null)
            {
                await RunBusyAsync(
                    LocalizationService.T("BusyGeneratingMelonAssemblies"),
                    () => EnsureMelonLoaderForDualStoresAsync(preferTarget: target)).ConfigureAwait(true);
                await SettleAfterSilentBetaAsync(silentResult).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"切服失败：{ex.Message}");
        }
        finally
        {
            IsBranchSwitchBusy = false;
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    async Task RunWizardFromStartAsync()
    {
        if (!Confirm(LocalizationService.T("ConfirmStartDualBranchWizard")))
            return;

        GameBranch current;
        if (_pickCurrentBranch is not null)
        {
            var picked = _pickCurrentBranch();
            if (picked is null)
                return;
            current = picked.Value;
        }
        else
        {
            // Tests / headless fallback: Yes=Official, No=Beta (legacy).
            current = Confirm(LocalizationService.T("ConfirmCurrentIsOfficial"))
                ? GameBranch.Official
                : GameBranch.Beta;
        }

        var betaName = string.IsNullOrWhiteSpace(BetaBranchName)
            ? BranchSwitchConfig.DefaultSteamBetaBranchName
            : BetaBranchName.Trim();
        if (string.IsNullOrWhiteSpace(betaName))
        {
            FailWizard(LocalizationService.T("FailWizardNoBetaName"));
            return;
        }

        if (!TryResolveSteamLayout(GamePath, out var steamLink, out var officialStore, out var betaStore))
        {
            FailWizard(LocalizationService.T("FailWizardGamePathNotSteamCommon"));
            return;
        }

        if (SteamBranchLayout.IsBranchStorePath(steamLink))
        {
            if (!SteamBranchLayout.TryGetCanonicalSteamLink(steamLink, out var canonicalLink))
            {
                FailWizard(LocalizationService.T("FailWizardGamePathIsBranchStore"));
                return;
            }

            if (SteamGameLocator.LooksLikeGameRoot(canonicalLink))
            {
                AppendLog(string.Format(LocalizationService.T("LogWizardNormalizedStorePath"), steamLink, canonicalLink));
                steamLink = canonicalLink;
                officialStore = Path.Combine(Path.GetDirectoryName(canonicalLink)!, SteamBranchLayout.OfficialStoreFolderName);
                betaStore = Path.Combine(Path.GetDirectoryName(canonicalLink)!, SteamBranchLayout.BetaStoreFolderName);
                GamePath = canonicalLink;
                try
                {
                    var cfgFix = LoadConfig();
                    cfgFix.GamePath = canonicalLink;
                    SaveConfig(cfgFix);
                }
                catch { /* best-effort */ }
            }
            else
            {
                FailWizard(LocalizationService.T("FailWizardGamePathIsBranchStore"));
                return;
            }
        }

        if (!SteamGameLocator.LooksLikeGameRoot(steamLink))
        {
            FailWizard(LocalizationService.T("FailWizardInvalidGamePath"));
            return;
        }

        var destA = current == GameBranch.Official ? officialStore : betaStore;
        if (Path.Exists(destA) && !PathsEqual(destA, steamLink))
        {
            FailWizard(string.Format(LocalizationService.T("FailWizardOtherStoreExists"), destA));
            return;
        }

        var acf = SteamBetaKeyEditor.FindAppManifestPath(steamLink);
        if (!File.Exists(acf))
        {
            FailWizard(LocalizationService.T("FailWizardNoAppManifest"));
            return;
        }

        try
        {
            using var _ = File.OpenRead(acf);
        }
        catch (Exception ex)
        {
            FailWizard(string.Format(LocalizationService.T("FailWizardCannotReadManifest"), ex.Message));
            return;
        }

        var cfg = _branchSwitch.LoadConfig();
        cfg.SteamLinkPath = steamLink;
        cfg.OfficialStorePath = officialStore;
        cfg.BetaStorePath = betaStore;
        cfg.ActiveBranch = current;
        cfg.BetaBranchName = betaName.Trim();
        cfg.Enabled = false;
        cfg.WizardStep = BranchWizardStep.Declared;
        cfg.ClearSessionStoreOwnership();
        _branchSwitch.SaveConfig(cfg);

        _suppressBranchSwitchSave = true;
        try
        {
            BetaBranchName = cfg.BetaBranchName;
            ActiveGameBranch = current;
            BranchWizardStep = BranchWizardStep.Declared;
            ClearLastLaunchRequestedAt();
        }
        finally
        {
            _suppressBranchSwitchSave = false;
        }

        var segmentAOk = false;
        string? segmentAFailure = null;
        await RunBusyAsync(LocalizationService.T("BusyWaitingSteam"), async () =>
        {
            if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
                return;

            await RunWithCriticalOpAsync(CriticalOpKind.BranchDiskWrite, "WizardArchiveA", async () =>
            {
                // Unique clean window: live ACF for current branch before ArchiveA moves the folder.
                var liveSnap = await Task.Run(() => _branchSwitch.TrySnapshotLiveAcfForWizardBranch(current))
                    .ConfigureAwait(true);
                if (liveSnap.Success)
                    AppendLog($"已保存 {current} Steam 清单快照（向导归档窗口），供下次切服免下载");
                else if (!string.IsNullOrWhiteSpace(liveSnap.Message))
                    AppendLog($"向导归档前未保存 {current} 快照（不阻断）：{liveSnap.Message}");

                BusyMessage(LocalizationService.T("BusyMovingGameFolder"));
                var archiveA = await Task.Run(() => _branchSwitch.ArchiveCurrentAs(current)).ConfigureAwait(true);
                if (!archiveA.Success)
                {
                    segmentAFailure = MapArchiveFailureMessage(archiveA.Message, destA);
                    return;
                }

                segmentAOk = true;
            }).ConfigureAwait(true);
        }).ConfigureAwait(true);

        if (segmentAFailure is not null)
        {
            AbortWizardAfterFailure(segmentAFailure);
            return;
        }

        if (!segmentAOk)
            return;

        // Stay on ArchivedA until silent-beta prep finishes; Continue sets WaitingDownloadB.
        await ContinueWizardAfterArchiveAAsync(current).ConfigureAwait(true);
    }

    async Task ContinueWizardAfterArchiveAAsync(GameBranch current)
    {
        var other = current == GameBranch.Official ? GameBranch.Beta : GameBranch.Official;
        var cfg = _branchSwitch.LoadConfig();
        var otherStore = other == GameBranch.Official ? cfg.OfficialStorePath : cfg.BetaStorePath;
        if (SteamGameLocator.LooksLikeGameRoot(otherStore))
        {
            await FinishWizardAfterStoresReadyAsync(current).ConfigureAwait(true);
            return;
        }

        // In-session wait after ArchiveA: do not force Steam exit (would cancel download).
        if (cfg.WizardStep == BranchWizardStep.WaitingDownloadB)
        {
            SetWizardStep(BranchWizardStep.WaitingDownloadB);
            PreferManualSteamOpen("NotifyWizardWaitingDownloadAuto");
            StartWizardDownloadPoll(current);
            return;
        }

        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
            return;

        var silentOther = _branchSwitch.TrySilentSetBeta(other);
        if (!silentOther.Success)
        {
            RecordEvent(ManagerEventLog.BetaKeyWriteFailed, new Dictionary<string, string?>
            {
                ["target"] = other.ToString(),
                ["error"] = silentOther.Message
            });
            var fail = string.IsNullOrWhiteSpace(silentOther.Message)
                ? LocalizationService.T("NotifySilentBetaFailedOther")
                : silentOther.Message;
            AbortWizardAfterFailure(string.Format(LocalizationService.T("NotifySilentBetaFailedAbort"), fail));
            return;
        }

        if (_steamRestartCooldown > TimeSpan.Zero)
            await _delay(_steamRestartCooldown).ConfigureAwait(true);

        SetWizardStep(BranchWizardStep.WaitingDownloadB);
        PreferManualSteamOpen("NotifyWizardWaitingDownloadAuto");
        StartWizardDownloadPoll(current);
    }

    /// <summary>A branch download that has not finished after this long is stuck, not slow.</summary>
    static readonly TimeSpan WizardDownloadPollMaxDuration = TimeSpan.FromHours(6);

    void StopWizardDownloadPoll()
    {
        try { _wizardDownloadPollCts?.Cancel(); }
        catch { /* ignore */ }
        _wizardDownloadPollCts?.Dispose();
        _wizardDownloadPollCts = null;
        _wizardDownloadPollGeneration++;
    }

    void StartWizardDownloadPoll(GameBranch current)
    {
        if (BranchWizardStep != BranchWizardStep.WaitingDownloadB)
            return;

        StopWizardDownloadPoll();
        var cts = new CancellationTokenSource();
        _wizardDownloadPollCts = cts;
        var gen = _wizardDownloadPollGeneration;
        var token = cts.Token;
        _ = RunWizardDownloadPollAsync(current, gen, token);
        RefreshBranchStatusText();
    }

    /// <summary>Steam cannot download into a library folder that no longer exists.</summary>
    bool WizardPollTargetExists()
    {
        try
        {
            var link = _branchSwitch.LoadConfig().SteamLinkPath;
            if (string.IsNullOrWhiteSpace(link))
                return false;
            var parent = Path.GetDirectoryName(Path.GetFullPath(link));
            return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent);
        }
        catch
        {
            return false;
        }
    }

    async Task RunWizardDownloadPollAsync(GameBranch current, int generation, CancellationToken token)
    {
        var interval = TimeSpan.FromSeconds(2);
        var giveUpAt = DateTimeOffset.Now + WizardDownloadPollMaxDuration;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (generation != _wizardDownloadPollGeneration)
                    return;
                if (BranchWizardStep != BranchWizardStep.WaitingDownloadB)
                    return;
                if (DateTimeOffset.Now >= giveUpAt || !WizardPollTargetExists())
                {
                    StopWizardDownloadPoll();
                    return;
                }

                if (!_wizardArchiveBRunning && !IsBranchSwitchBusy && IsWizardDownloadReadyNow())
                {
                    AppendLog(LocalizationService.T("LogWizardDownloadAutoContinue"));
                    await CompleteWizardAfterDownloadBAsync(current).ConfigureAwait(true);
                    return;
                }

                // Refresh sticky strip so "download done — exit Steam" appears while still waiting.
                SyncStickyBranchTaskStrip();

                await _delay(interval).ConfigureAwait(true);
                // Floor pacing: fixtures often inject delay => Task.CompletedTask.
                await Task.Delay(250, token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on teardown / cancel
        }
        catch (Exception ex)
        {
            AppendLog($"等待另一服下载时出错：{ex.Message}");
        }
    }

    internal bool IsWizardDownloadReadyNow()
    {
        try
        {
            var cfg = _branchSwitch.LoadConfig();
            var link = cfg.SteamLinkPath;
            string? acfText = null;
            if (!string.IsNullOrWhiteSpace(link))
            {
                var acfPath = SteamBetaKeyEditor.FindAppManifestPath(link);
                if (File.Exists(acfPath))
                    acfText = File.ReadAllText(acfPath);
            }

            return BranchStoreHealth.IsWizardDownloadReady(
                link,
                acfText,
                _processProbe.IsGameOrSteamRunning());
        }
        catch
        {
            return false;
        }
    }

    bool IsWizardDownloadDiskReadyNow()
    {
        try
        {
            var cfg = _branchSwitch.LoadConfig();
            var link = cfg.SteamLinkPath;
            string? acfText = null;
            if (!string.IsNullOrWhiteSpace(link))
            {
                var acfPath = SteamBetaKeyEditor.FindAppManifestPath(link);
                if (File.Exists(acfPath))
                    acfText = File.ReadAllText(acfPath);
            }

            return BranchStoreHealth.IsWizardDownloadDiskReady(link, acfText);
        }
        catch
        {
            return false;
        }
    }

    string ResolveWizardWaitingDownloadTaskMessage()
    {
        if (IsWizardDownloadDiskReadyNow() && _processProbe.IsGameOrSteamRunning())
            return LocalizationService.T("TaskMessageWizardWaitingExitSteam");
        return LocalizationService.T("TaskMessageWizardWaitingDownload");
    }

    async Task CompleteWizardAfterDownloadBAsync(GameBranch current)
    {
        if (_wizardArchiveBRunning || IsBranchSwitchBusy)
            return;
        if (BranchWizardStep != BranchWizardStep.WaitingDownloadB)
            return;
        if (!IsWizardDownloadReadyNow())
            return;

        _wizardArchiveBRunning = true;
        StopWizardDownloadPoll();
        IsBranchSwitchBusy = true;
        var other = current == GameBranch.Official ? GameBranch.Beta : GameBranch.Official;
        var cfg = _branchSwitch.LoadConfig();
        var otherStore = other == GameBranch.Official ? cfg.OfficialStorePath : cfg.BetaStorePath;
        var linked = false;
        string? segmentBcFailure = null;
        try
        {
            await RunBusyAsync(LocalizationService.T("BusyWaitingSteam"), async () =>
            {
                if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
                    return;

                await RunWithCriticalOpAsync(CriticalOpKind.BranchDiskWrite, "WizardArchiveB", async () =>
                {
                    // Unique clean window: live ACF for other branch before ArchiveB moves the folder.
                    var liveSnap = await Task.Run(() => _branchSwitch.TrySnapshotLiveAcfForWizardBranch(other))
                        .ConfigureAwait(true);
                    if (liveSnap.Success)
                        AppendLog($"已保存 {other} Steam 清单快照（向导下载窗口），供下次切服免下载");
                    else if (!string.IsNullOrWhiteSpace(liveSnap.Message))
                        AppendLog($"向导归档前未保存 {other} 快照（不阻断）：{liveSnap.Message}");

                    BusyMessage(LocalizationService.T("BusyMovingGameFolder"));
                    var archiveB = await Task.Run(() => _branchSwitch.ArchiveDownloadedAs(other)).ConfigureAwait(true);
                    if (!archiveB.Success)
                    {
                        var destHint = string.IsNullOrWhiteSpace(otherStore)
                            ? (other == GameBranch.Official
                                ? SteamBranchLayout.OfficialStoreFolderName
                                : SteamBranchLayout.BetaStoreFolderName)
                            : otherStore;
                        segmentBcFailure = MapArchiveFailureMessage(archiveB.Message, destHint);
                        return;
                    }

                    BusyMessage(LocalizationService.T("BusyCreatingJunction"));
                    var link = await Task.Run(() => _branchSwitch.CreateLinkTo(current)).ConfigureAwait(true);
                    if (!link.Success)
                    {
                        segmentBcFailure = MapBranchOperationFailure(
                            link.Message,
                            LocalizationService.T("FailWizardCreateLinkFailed"));
                        return;
                    }

                    ApplyLinkedWizardState(current);
                    linked = true;
                }).ConfigureAwait(true);
            }).ConfigureAwait(true);

            if (segmentBcFailure is not null)
            {
                // Three-entry: enable structural/archive/link failure must full-rollback (no sticky WaitingDownloadB).
                AbortWizardAfterFailure(segmentBcFailure);
                return;
            }

            if (!linked)
            {
                AbortWizardAfterFailure(LocalizationService.T("FailWizardCreateLinkFailed"));
                return;
            }

            try
            {
                await RunBusyAsync(
                    LocalizationService.T("BusyGeneratingMelonAssemblies"),
                    () => EnsureMelonLoaderForDualStoresAsync(preferTarget: current)).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                AppendLog("双仓已联接；Melon 同步被取消，继续结算。");
            }
            catch (Exception melonEx)
            {
                AppendLog($"双仓已联接；Melon 同步未完成：{melonEx.Message}");
            }

            var silent = _branchSwitch.TryPrepareSteamBranchMetadata(current);
            await SettleAfterSilentBetaAsync(silent, LocalizationService.T("NotifyWizardDoneWaitingSteam")).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            if (linked)
            {
                AppendLog(string.Format(LocalizationService.T("NotifyBranchWizardFailed"), ex.Message));
                try
                {
                    var silent = _branchSwitch.TryPrepareSteamBranchMetadata(current);
                    await SettleAfterSilentBetaAsync(silent, LocalizationService.T("NotifyWizardDoneWaitingSteam")).ConfigureAwait(true);
                }
                catch (Exception settleEx)
                {
                    AppendLog($"联接后结算失败：{settleEx.Message}");
                }
            }
            else
            {
                AbortWizardAfterFailure(string.Format(LocalizationService.T("NotifyBranchWizardFailed"), ex.Message));
            }
        }
        finally
        {
            _wizardArchiveBRunning = false;
            IsBranchSwitchBusy = false;
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    async Task FinishWizardAfterStoresReadyAsync(GameBranch current)
    {
        if (!await TryCreateLinkAndApplyAsync(current).ConfigureAwait(true))
            return;

        var silent = _branchSwitch.TryPrepareSteamBranchMetadata(current);
        await SettleAfterSilentBetaAsync(silent, LocalizationService.T("NotifyWizardDoneWaitingSteam")).ConfigureAwait(true);
    }

    async Task<bool> TryCreateLinkAndApplyAsync(GameBranch current)
    {
        var linked = false;
        string? linkFailure = null;
        await RunBusyAsync(LocalizationService.T("BusyCreatingJunction"), async () =>
        {
            await RunWithCriticalOpAsync(CriticalOpKind.BranchDiskWrite, "WizardLink", async () =>
            {
                var link = await Task.Run(() => _branchSwitch.CreateLinkTo(current)).ConfigureAwait(true);
                if (!link.Success)
                {
                    linkFailure = string.IsNullOrWhiteSpace(link.Message)
                        ? LocalizationService.T("FailWizardCreateLinkFailed")
                        : link.Message;
                    return;
                }

                ApplyLinkedWizardState(current);
                linked = true;
            }).ConfigureAwait(true);
        }).ConfigureAwait(true);

        if (linkFailure is not null)
        {
            AbortWizardAfterFailure(MapBranchOperationFailure(
                linkFailure,
                LocalizationService.T("FailWizardCreateLinkFailed")));
            return false;
        }

        if (linked)
        {
            try
            {
                await RunBusyAsync(
                    LocalizationService.T("BusyGeneratingMelonAssemblies"),
                    () => EnsureMelonLoaderForDualStoresAsync(preferTarget: current)).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                AppendLog("双仓已联接；Melon 同步被取消，继续结算。");
            }
            catch (Exception melonEx)
            {
                AppendLog($"双仓已联接；Melon 同步未完成：{melonEx.Message}");
            }
        }

        return linked;
    }

    void ApplyLinkedWizardState(GameBranch current)
    {
        _branchSwitch.MigrateLegacyManifestIfNeeded(current);

        _suppressBranchSwitchSave = true;
        try
        {
            BranchSwitchEnabled = true;
            ActiveGameBranch = current;
            BranchWizardStep = BranchWizardStep.Linked;
            ClearLastLaunchRequestedAt();
        }
        finally
        {
            _suppressBranchSwitchSave = false;
        }

        TryUpdateBranchConfig(cfg =>
        {
            cfg.Enabled = true;
            cfg.ActiveBranch = current;
            cfg.WizardStep = BranchWizardStep.Linked;
            cfg.ClearSessionStoreOwnership();
        });

        RefreshStatus();
    }

    async Task EnsureMelonLoaderForDualStoresAsync(GameBranch? preferTarget = null)
    {
        try
        {
            var cfg = _branchSwitch.LoadConfig();
            if (string.IsNullOrWhiteSpace(cfg.OfficialStorePath) || string.IsNullOrWhiteSpace(cfg.BetaStorePath))
                return;

            var result = _melonDualSync.EnsureOnBothStores(cfg.OfficialStorePath, cfg.BetaStorePath);
            if (!string.IsNullOrWhiteSpace(result.Message))
                AppendLog(result.Message);

            if (!result.Success)
            {
                var warn = LocalizationService.T("NotifyMelonLoaderDualStoreIncomplete");
                AppendLog(warn);
                _notify(warn);
                return;
            }

            var targetPath = preferTarget switch
            {
                GameBranch.Official => cfg.OfficialStorePath,
                GameBranch.Beta => cfg.BetaStorePath,
                _ => string.IsNullOrWhiteSpace(GamePath) ? cfg.SteamLinkPath : GamePath
            };
            await EnsureMelonAssembliesForStoreAsync(targetPath).ConfigureAwait(true);
            RefreshStatusCore(offerAssemblyGeneratePrompt: false);
        }
        catch (Exception ex)
        {
            AppendLog($"补齐双服 MelonLoader 失败：{ex.Message}");
        }
    }

    async Task EnsureMelonAssembliesForStoreAsync(string? storePath)
    {
        if (string.IsNullOrWhiteSpace(storePath))
            return;
        if (_detector.Detect(storePath).Kind is not GameStatusKind.LoaderPresentAssembliesMissing)
            return;

        AppendLog($"正在为「{Path.GetFileName(storePath)}」生成 MelonLoader 程序集…");

        var dispatcher = Application.Current?.Dispatcher;
        void Progress(string msg)
        {
            if (dispatcher is null || dispatcher.CheckAccess())
                AppendLog(msg);
            else
                _ = dispatcher.BeginInvoke(() => AppendLog(msg));
        }

        // Await (do not GetResult) so the WPF dispatcher stays responsive while Unity runs.
        // Blocking the UI thread previously caused Win32Exception 1816 (not enough quota) crashes.
        var gen = await _melonAssemblyGenerator
            .EnsureAssembliesAsync(storePath, progress: Progress, cancellationToken: BusyWorkToken)
            .ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(gen.Message))
            AppendLog(gen.Message);
        if (!gen.Success && !gen.Skipped)
            _notify(gen.Message);
    }

    void WarnLeftoverDualStores(string? aroundPath, bool userChoseKeep)
    {
        var leftovers = SteamBranchLayout.EnumerateExistingStoreFolders(aroundPath)
            .Where(SteamGameLocator.LooksLikeGameRoot)
            .ToList();
        if (leftovers.Count == 0)
            return;

        var joined = string.Join("\n", leftovers);
        var key = userChoseKeep
            ? "NotifyBranchTeardownLeftoverStores"
            : "NotifyBranchTeardownLeftoverStoresResidual";
        var msg = string.Format(LocalizationService.T(key), joined);
        AppendLog(msg);
        _notify(msg);
    }

    internal     static string MapArchiveFailureMessage(string? raw, string destHint)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return LocalizationService.T("FailWizardArchiveCurrentFailed");

        if (raw.Contains(BranchOpMessages.StorePathExistsIncomplete, StringComparison.OrdinalIgnoreCase)
            || (raw.Contains("Store path already exists", StringComparison.OrdinalIgnoreCase)
                && raw.Contains("not a complete", StringComparison.OrdinalIgnoreCase)))
            return string.Format(LocalizationService.T("FailWizardOtherStoreIncomplete"), destHint);

        if (raw.Contains(BranchOpMessages.StorePathExistsLinkConflict, StringComparison.OrdinalIgnoreCase))
            return string.Format(LocalizationService.T("FailWizardOtherStoreLinkConflict"), destHint);

        if (raw.Contains("Store path already exists", StringComparison.OrdinalIgnoreCase))
            return string.Format(LocalizationService.T("FailWizardOtherStoreExists"), destHint);

        if (raw.Contains(BranchOpMessages.SteamLinkPathAlreadyExists, StringComparison.OrdinalIgnoreCase)
            || raw.Contains(BranchOpMessages.SteamLinkPathWrongJunction, StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Steam link path already exists", StringComparison.OrdinalIgnoreCase))
            return LocalizationService.T("FailWizardSteamLinkExists");

        return raw;
    }

    internal static bool IsStructuralWizardArchiveFailureRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return raw.Contains(BranchOpMessages.StorePathExistsIncomplete, StringComparison.OrdinalIgnoreCase)
               || raw.Contains(BranchOpMessages.StorePathExistsLinkConflict, StringComparison.OrdinalIgnoreCase)
               || raw.Contains(BranchOpMessages.SteamLinkPathAlreadyExists, StringComparison.OrdinalIgnoreCase)
               || raw.Contains(BranchOpMessages.SteamLinkPathWrongJunction, StringComparison.OrdinalIgnoreCase);
    }

    static string MapBranchOperationFailure(string? raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (raw.Contains(BranchOpMessages.SteamLinkHollowNoDonor, StringComparison.OrdinalIgnoreCase)
            || (raw.Contains("Steam link path is not a valid game root", StringComparison.OrdinalIgnoreCase)
                && raw.Contains("no complete leftover", StringComparison.OrdinalIgnoreCase)))
            return LocalizationService.T("FailOrphanSteamLinkHollowNoDonor");

        if (raw.Contains("Steam link path is not a valid game root", StringComparison.OrdinalIgnoreCase))
            return LocalizationService.T("FailOrphanSteamLinkNotGameRoot");

        if (raw.Contains("Current store is not a valid game root", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Restored path is not a valid game root", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Branch store is not a valid game root", StringComparison.OrdinalIgnoreCase))
            return LocalizationService.T("FailBranchStoreIncompleteSteamDownload");

        if (raw.Contains(BranchOpMessages.SteamLinkPathAlreadyExists, StringComparison.OrdinalIgnoreCase)
            || raw.Contains(BranchOpMessages.SteamLinkPathWrongJunction, StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Steam link path already exists", StringComparison.OrdinalIgnoreCase))
            return LocalizationService.T("FailWizardSteamLinkExists");

        return raw;
    }

    void SetWizardStep(BranchWizardStep step)
    {
        BranchWizardStep = step;
        TryUpdateBranchConfig(cfg => cfg.WizardStep = step);
        NotifyBranchGates();
        RefreshBranchStatusText();
    }

    void FailWizard(string message)
    {
        StopWizardDownloadPoll();
        AppendLog(message);
        _notify(message);
    }

    /// <summary>
    /// Archive/link failure after Declared must not leave a sticky mid-wizard residue.
    /// If Steam link is still a valid game root, clear wizard step so deploy stays on that path.
    /// </summary>
    void AbortWizardAfterFailure(string message)
    {
        FailWizard(message);
        RollbackEnableDualToSingle(notify: false);
    }

    /// <summary>
    /// Reset incomplete enable-dual via session rollback (no mid-wizard resume).
    /// </summary>
    void AbandonIncompleteWizard(bool notifyFailure) =>
        RollbackEnableDualToSingle(notify: notifyFailure);

    static bool TryResolveSteamLayout(string gamePath, out string steamLink, out string officialStore, out string betaStore)
    {
        steamLink = "";
        officialStore = "";
        betaStore = "";
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;

        try
        {
            steamLink = Path.GetFullPath(gamePath);
        }
        catch
        {
            return false;
        }

        var common = Path.GetDirectoryName(steamLink);
        if (string.IsNullOrEmpty(common)
            || !string.Equals(Path.GetFileName(common), "common", StringComparison.OrdinalIgnoreCase))
            return false;

        var steamapps = Path.GetDirectoryName(common);
        if (string.IsNullOrEmpty(steamapps)
            || !string.Equals(Path.GetFileName(steamapps), "steamapps", StringComparison.OrdinalIgnoreCase))
            return false;

        officialStore = Path.Combine(common, "Mechabellum_official");
        betaStore = Path.Combine(common, "Mechabellum_beta");
        return true;
    }

    async Task<bool> WaitForSteamAndGameExitAsync() =>
        await _steamLifecycle.EnsureClientAndGameExitedAsync().ConfigureAwait(true);

    /// <summary>
    /// Do not auto-launch Steam via protocol URIs: with multi-account "ask who is playing"
    /// enabled, steam://open/games pops the account picker and fights WaitForExit.
    /// Prompt the user to open Steam themselves when a download/validate is needed.
    /// </summary>
    void PreferManualSteamOpen(string reasonKey)
    {
        var msg = LocalizationService.T(reasonKey);
        _steamLifecycle.PreferManualOpen(msg);
    }

    void EnterSteamSettle()
    {
        IsAwaitingSteamSettle = true;
        BranchWizardStep = BranchWizardStep.AwaitingSteamSettle;
        TryUpdateBranchConfig(cfg => cfg.WizardStep = BranchWizardStep.AwaitingSteamSettle);
        NotifyBranchGates();
        RefreshBranchStatusText();
    }

    async Task SettleAfterSilentBetaAsync(BranchOperationResult silent, string? successLog = null)
    {
        EnterSteamSettle();
        if (!silent.Success || silent.DegradeToManualBeta)
        {
            RecordEvent(ManagerEventLog.BetaKeyWriteFailed, new Dictionary<string, string?>
            {
                ["error"] = silent.Message,
                ["degradeToManual"] = silent.DegradeToManualBeta ? "true" : "false"
            });
            DegradeToManualBeta = true;
            if (!string.IsNullOrWhiteSpace(silent.Message))
                AppendLog(silent.Message);
            var noStart = LocalizationService.T("NotifySilentBetaNoStart");
            AppendLog(noStart);
            _notify(noStart);
            return;
        }

        DegradeToManualBeta = false;
        if (_steamRestartCooldown > TimeSpan.Zero)
            await _delay(_steamRestartCooldown).ConfigureAwait(true);

        var restored = string.Equals(silent.Message, "restored-acf-snapshot", StringComparison.Ordinal);
        if (restored)
        {
            AppendLog(LocalizationService.T("LogSettleSnapshotNoAutoSteam"));
        }
        else
        {
            PreferManualSteamOpen("NotifyOpenSteamManuallyForSettle");
        }

        if (!string.IsNullOrWhiteSpace(successLog))
            AppendLog(successLog);
    }

    void DeployBoundProfileAndClearSettle()
    {
        if (TryBlockSettleWhenUpdateUnhealthy())
            return;

        SelectBoundProfile(ActiveGameBranch);

        RefreshStatusCore(offerAssemblyGeneratePrompt: false);
        if (GameStatus?.Kind != GameStatusKind.Ready)
        {
            var msg = GameStatus?.Message ?? LocalizationService.T("NotifySettleBlockedGameNotReady");
            AppendLog(msg);
            AppendLog(string.Format(
                LocalizationService.T("LogSettleKeptAwaitingDownloadFmt"),
                ActiveBranchDisplayName));
            _notify(LocalizationService.T("NotifySettleBlockedGameNotReady"));
            return;
        }

        if (!ApplyProfile(ignoreBranchGate: true))
        {
            AppendLog(LocalizationService.T("LogSettleKeptAwaiting"));
            _notify(LocalizationService.T("NotifySettleBlockedDeployFailed"));
            return;
        }

        var snap = _branchSwitch.TrySnapshotSettledAcf(ActiveGameBranch);
        if (!snap.Success)
        {
            if (!string.IsNullOrWhiteSpace(snap.Message))
                AppendLog($"结算后未保存 ACF 快照：{snap.Message}");

            if (snap.IsGameRunningBlock)
            {
                AppendLog(LocalizationService.T("LogSettleKeptAwaiting"));
                _notify(LocalizationService.T("NotifySettleBlockedGameRunning"));
                return;
            }

            if (snap.IsManifestNotSettled)
            {
                AppendLog(LocalizationService.T("LogSettleKeptAwaiting"));
                _notify(LocalizationService.T("NotifySettleBlockedAcfNotSettled"));
                return;
            }

            if (snap.IsSnapshotNotAligned)
            {
                AppendLog(LocalizationService.T("LogSettleKeptAwaiting"));
                _notify(LocalizationService.T("NotifySettleBlockedSnapshotNotAligned"));
                return;
            }

            // Never promote Ready without a successful snapshot — hollow/corrupt ACF must stay in settle.
            AppendLog(LocalizationService.T("LogSettleKeptAwaiting"));
            _notify(LocalizationService.T("NotifySettleBlockedSnapshotFailed"));
            return;
        }

        AppendLog($"已保存 {ActiveGameBranch} Steam 清单快照，供下次切服免下载");
        ClearSteamSettle();
    }

    bool TryBlockSettleWhenUpdateUnhealthy()
    {
        try
        {
            var link = string.IsNullOrWhiteSpace(GamePath)
                ? _branchSwitch.LoadConfig().SteamLinkPath
                : GamePath;
            if (string.IsNullOrWhiteSpace(link))
                return false;

            var acfPath = SteamBetaKeyEditor.FindAppManifestPath(link);
            if (!File.Exists(acfPath))
                return false;

            var text = File.ReadAllText(acfPath);
            if (!SteamBetaKeyEditor.LooksUpdateUnhealthy(text))
                return false;

            var flags = SteamBetaKeyEditor.DecodeStateFlags(
                SteamBetaKeyEditor.ReadQuotedValue(text, "StateFlags"));
            var msg = LocalizationService.T("NotifySettleBlockedUpdateUnhealthy");
            AppendLog(msg);
            if (!string.IsNullOrWhiteSpace(flags))
                AppendLog($"StateFlags: {flags}");
            _notify(msg);
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"检查 Steam 更新状态失败：{ex.Message}");
            return false;
        }
    }

    void ClearSteamSettle()
    {
        IsAwaitingSteamSettle = false;
        DegradeToManualBeta = false;
        BranchWizardStep = BranchWizardStep.Ready;
        TryUpdateBranchConfig(cfg =>
        {
            cfg.WizardStep = BranchWizardStep.Ready;
            cfg.ActiveBranch = ActiveGameBranch;
            cfg.ClearSessionStoreOwnership();
        });
        NotifyBranchGates();
        RefreshBranchStatusText();
        if (IsStaleReadyRecovery())
            ClearRecoveryGate();
    }

    void SelectBoundProfile(GameBranch branch)
    {
        var id = branch == GameBranch.Official ? OfficialProfileId : BetaProfileId;
        var match = Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            SelectedProfile = match;
    }

    void TryUpdateBranchConfig(Action<BranchSwitchConfig> mutate)
    {
        try
        {
            var cfg = _branchSwitch.LoadConfig();
            mutate(cfg);
            _branchSwitch.SaveConfig(cfg);
        }
        catch
        {
            // Isolation: branch-switch persistence must not crash the manager.
        }
    }

    void NotifyBranchGates()
    {
        OnPropertyChanged(nameof(CanDeployOrLaunch));
        OnPropertyChanged(nameof(CanApplyProfile));
        OnPropertyChanged(nameof(UseApplyAccent));
        OnPropertyChanged(nameof(IsBranchWizardBlocking));
        OnPropertyChanged(nameof(IsBranchWizardInProgress));
        OnPropertyChanged(nameof(IsSessionLocked));
        OnPropertyChanged(nameof(DeployBlockedReason));
        OnPropertyChanged(nameof(ShowDeployBlockedReason));
        OnPropertyChanged(nameof(CanSwitchGameBranch));
        OnPropertyChanged(nameof(IsSteamWritingGame));
        OnPropertyChanged(nameof(CanStartBranchWizard));
        OnPropertyChanged(nameof(ShowEmergencyRecoverSingle));
        OnPropertyChanged(nameof(CanEmergencyRecoverSingle));
        OnPropertyChanged(nameof(CanTeardownBranchSwitch));
        OnPropertyChanged(nameof(ShowRepairOrphanDualLayout));
        OnPropertyChanged(nameof(CanRepairOrphanDualLayout));
        ApplyProfileCommand.NotifyCanExecuteChanged();
        ApplyAndLaunchCommand.NotifyCanExecuteChanged();
        EmergencyRecoverSingleCommand.NotifyCanExecuteChanged();
        TeardownBranchSwitchCommand.NotifyCanExecuteChanged();
        RepairOrphanDualLayoutCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(NeedsMelonLoaderInstall));
        OnPropertyChanged(nameof(CanInstallMelonLoader));
        InstallMelonLoaderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanConfirmManualBeta));
        OnPropertyChanged(nameof(SettleConfirmButtonText));
        ConfirmManualBetaCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowConfirmWizardExitSteam));
        OnPropertyChanged(nameof(CanConfirmWizardExitSteam));
        OnPropertyChanged(nameof(WizardExitSteamConfirmButtonText));
        ConfirmWizardExitSteamCommand.NotifyCanExecuteChanged();
    }

    string? TryDescribeSteamBetaKeyMismatch()
    {
        try
        {
            if (!BranchSwitchEnabled)
                return null;
            if (IsAwaitingSteamSettle || IsBranchWizardInProgress)
                return null;

            var cfg = _branchSwitch.LoadConfig();
            if (string.IsNullOrWhiteSpace(cfg.SteamLinkPath))
                return null;

            var acf = SteamBetaKeyEditor.FindAppManifestPath(cfg.SteamLinkPath);
            if (string.IsNullOrWhiteSpace(acf) || !File.Exists(acf))
                return null;

            var actual = (new SteamBetaKeyEditor(_processProbe).ReadBetaKey(acf) ?? "").Trim();
            if (ActiveGameBranch == GameBranch.Official)
            {
                if (string.IsNullOrEmpty(actual))
                    return null;
            }
            else
            {
                var expected = (cfg.BetaBranchName ?? "").Trim();
                if (string.IsNullOrEmpty(expected)
                    || string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            return string.Format(
                LocalizationService.T("NotifySteamBetaKeyMismatch"),
                ActiveBranchDisplayName,
                string.IsNullOrEmpty(actual) ? "(none)" : actual);
        }
        catch
        {
            return null;
        }
    }

    void RefreshBranchStatusText()
    {
        if (BranchWizardStep == BranchWizardStep.WaitingDownloadB)
            BranchStatusText = LocalizationService.T("BranchStatusEnablingWaitDownload");
        else if (!BranchSwitchEnabled)
        {
            var needsEmergency = false;
            try
            {
                var cfg = _branchSwitch.LoadConfig();
                needsEmergency = cfg.SessionOwnedOfficialStore
                    || cfg.SessionOwnedBetaStore
                    || ShowEmergencyRecoverSingle;
            }
            catch
            {
                needsEmergency = ShowEmergencyRecoverSingle;
            }

            BranchStatusText = IsBranchWizardInProgress
                ? LocalizationService.T("BranchStatusEnabling")
                : needsEmergency
                    ? LocalizationService.T("BranchStatusNeedsEmergency")
                    : LocalizationService.T("BranchStatusUnconfigured");
        }
        else if (IsAwaitingSteamSettle || BranchWizardStep == BranchWizardStep.AwaitingSteamSettle)
            BranchStatusText = string.Format(
                LocalizationService.T("BranchStatusWaitingSteamFmt"),
                ActiveBranchDisplayName);
        else if (IsBranchWizardBlocking)
            BranchStatusText = LocalizationService.T("BranchStatusEnabling");
        else
            BranchStatusText = ActiveGameBranch == GameBranch.Official
                ? LocalizationService.T("BranchStatusOfficial")
                : LocalizationService.T("BranchStatusBeta");

        if (TryDescribeSteamBetaKeyMismatch() is { } mismatch)
            BranchStatusText = BranchStatusText + " · " + mismatch;

        SyncStickyBranchTaskStrip();
    }

    string CurrentDeployManifestPath() =>
        _paths.GetDeployManifestPath(ActiveGameBranch, BranchSwitchEnabled);

    string CurrentDeployManifestPrevPath() =>
        _paths.GetDeployManifestPrevPath(ActiveGameBranch, BranchSwitchEnabled);

    partial void OnIsAwaitingSteamSettleChanged(bool value)
    {
        NotifyBranchGates();
        RefreshBranchStatusText();
        OnPropertyChanged(nameof(ShowConfirmManualBeta));
    }

    partial void OnDegradeToManualBetaChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowConfirmManualBeta));
        OnPropertyChanged(nameof(SettleConfirmButtonText));
    }

    partial void OnIsBranchSwitchBusyChanged(bool value) => NotifyBranchGates();

    partial void OnBranchSwitchEnabledChanged(bool value)
    {
        if (!_suppressBranchSwitchSave)
            TryUpdateBranchConfig(cfg => cfg.Enabled = value);
        NotifyBranchGates();
        RefreshBranchStatusText();
    }

    partial void OnActiveGameBranchChanged(GameBranch value)
    {
        if (!_suppressBranchSwitchSave)
            TryUpdateBranchConfig(cfg => cfg.ActiveBranch = value);
        OnPropertyChanged(nameof(SettleConfirmButtonText));
        RefreshBranchStatusText();
    }

    partial void OnBetaBranchNameChanged(string value)
    {
        if (!_suppressBranchSwitchSave)
            TryUpdateBranchConfig(cfg => cfg.BetaBranchName = value ?? "");
    }

    partial void OnOfficialProfileIdChanged(string value)
    {
        if (!_suppressBranchSwitchSave)
            TryUpdateBranchConfig(cfg => cfg.OfficialProfileId = value ?? "");
    }

    partial void OnBetaProfileIdChanged(string value)
    {
        if (!_suppressBranchSwitchSave)
            TryUpdateBranchConfig(cfg => cfg.BetaProfileId = value ?? "");
    }

    partial void OnBranchWizardStepChanged(BranchWizardStep value)
    {
        NotifyBranchGates();
        RefreshBranchStatusText();
        OnPropertyChanged(nameof(StatusKindLabel));
    }

    AppConfig LoadConfig() =>
        _store.LoadOrDefault(_paths.ConfigPath, () => new AppConfig());

    void SaveConfig(AppConfig config) => _store.Save(_paths.ConfigPath, config);

    /// <summary>
    /// Last launch is per current store. After a junction/branch change, the old timestamp
    /// must not judge the new store's Latest.log (false "not injected").
    /// </summary>
    void ClearLastLaunchRequestedAt()
    {
        var config = LoadConfig();
        if (config.LastLaunchRequestedAt is null && config.LastApplyStartedAt is null)
            return;
        config.LastLaunchRequestedAt = null;
        config.LastApplyStartedAt = null;
        SaveConfig(config);
    }

    void ClearLastLaunchAfterStoreChange()
    {
        ClearLastLaunchRequestedAt();
        RefreshStatusCore(offerAssemblyGeneratePrompt: false);
    }

    void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _managerLog.Append(line);
        LatestLogLine = line;
        LogText = string.IsNullOrEmpty(LogText) ? line : LogText + Environment.NewLine + line;
    }

    void RecordEvent(string code, IReadOnlyDictionary<string, string?>? data = null) =>
        _events.Write(code, data);

    void RecordLoaderNotInjectedOnce()
    {
        var last = _events.ReadRecent(1);
        if (last.Count > 0 && last[0].Code == ManagerEventLog.LoaderNotInjected)
            return;
        RecordEvent(ManagerEventLog.LoaderNotInjected);
    }

    internal void LogTaxonomyWarning(string message) => AppendLog(message);

    void LogInvalidCatalogCategory(CatalogMod mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.Category) &&
            !ModTaxonomy.TryParseCategory(mod.Category, out _))
        {
            AppendLog($"目录条目 '{mod.Id}': 无效分类 '{mod.Category}'，按未分类处理。");
        }
    }

    static bool IsPortableRoot(string? dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot)) return false;
        var portable = Path.Combine(AppContext.BaseDirectory, "data");
        return PathsEqual(dataRoot, portable);
    }

    static string Normalize(string relativePath) =>
        relativePath.Replace('\\', '/').Trim('/');

    static bool PathsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        try
        {
            return string.Equals(
                Path.GetFullPath(a.Trim()),
                Path.GetFullPath(b.Trim()),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
