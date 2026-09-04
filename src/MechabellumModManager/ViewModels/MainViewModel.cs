using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
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
    readonly RiskHeuristic _riskHeuristic;
    readonly SteamGameLocator _steamLocator;
    readonly UpdateChecker _updateChecker;
    readonly ModCatalogService _catalog;
    readonly AssemblyInspector _assemblyInspector;
    readonly Func<string, bool> _confirmHighRisk;
    readonly Func<string, bool> _confirm;
    readonly Func<string, MessageBoxResult, bool>? _confirmChoice;
    readonly Action<string> _notify;
    readonly Func<string?>? _browseFolder;
    readonly Func<string?>? _openDll;
    readonly Func<string?>? _openZip;
    readonly Func<string, string?>? _promptText;
    readonly Func<ModPackageType?>? _pickPackageType;
    readonly Func<string?>? _openFolder;
    readonly Func<string, (ReportCategory Category, string Notes)?>? _promptReport;
    readonly Func<bool>? _promptSubmitGuide;
    readonly Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags)?>? _promptEditTaxonomy;
    readonly Action<string>? _copyText;
    readonly BranchSwitchService _branchSwitch;
    readonly IProcessProbe _processProbe;
    readonly IProcessStarter _processStarter;
    readonly Func<TimeSpan, Task> _delay;
    readonly TimeSpan _steamExitTimeout;
    readonly TimeSpan _steamExitCooldown;
    readonly TimeSpan _steamRestartCooldown;
    bool _loggedMelonOptimize;
    bool _suppressBranchSwitchSave;
    bool _checkingUpdates;
    bool _checkingCatalog;
    bool _addingCatalogMod;
    bool _autoImportedFromGame;
    bool _suppressLanguageSave;
    bool _reporting;
    bool _suppressFilterRefresh;

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
        Func<string?>? openFolder = null,
        Func<string, (ReportCategory Category, string Notes)?>? promptReport = null,
        Func<bool>? promptSubmitGuide = null,
        Func<ModPackage, (string? Override, IReadOnlyList<string> ExtraTags)?>? promptEditTaxonomy = null,
        Action<string>? copyText = null,
        BranchSwitchService? branchSwitch = null,
        IProcessProbe? processProbe = null,
        IProcessStarter? processStarter = null,
        Func<TimeSpan, Task>? delay = null,
        TimeSpan? steamExitTimeout = null,
        TimeSpan? steamExitCooldown = null,
        TimeSpan? steamRestartCooldown = null,
        Func<string, MessageBoxResult, bool>? confirmChoice = null)
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
        _riskHeuristic = riskHeuristic ?? new RiskHeuristic();
        _steamLocator = steamLocator ?? new SteamGameLocator();
        _updateChecker = updateChecker ?? new UpdateChecker();
        _catalog = catalog ?? new ModCatalogService();
        _assemblyInspector = assemblyInspector ?? new AssemblyInspector();
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
        _openFolder = openFolder;
        _promptReport = promptReport;
        _promptSubmitGuide = promptSubmitGuide;
        _promptEditTaxonomy = promptEditTaxonomy;
        _copyText = copyText;
        _processProbe = processProbe ?? new ProcessProbe();
        _processStarter = processStarter ?? new ShellProcessStarter();
        _delay = delay ?? (span => Task.Delay(span));
        _steamExitTimeout = steamExitTimeout ?? TimeSpan.FromSeconds(45);
        _steamExitCooldown = steamExitCooldown ?? TimeSpan.FromSeconds(3);
        _steamRestartCooldown = steamRestartCooldown ?? TimeSpan.FromSeconds(2);
        _branchSwitch = branchSwitch ?? new BranchSwitchService(
            paths,
            store,
            _processProbe,
            new JunctionService(),
            new SteamBetaKeyEditor(_processProbe));
        ApplyProfileCommand = new RelayCommand(() => _ = ApplyProfile(), () => CanDeployOrLaunch);

        Ui = new UiStrings();
        Profiles = new ObservableCollection<ProfileItemViewModel>();
        Mods = new ObservableCollection<ModItemViewModel>();
        CatalogMods = new ObservableCollection<CatalogModItemViewModel>();
        CatalogModsView = CollectionViewSource.GetDefaultView(CatalogMods);
        CatalogModsView.Filter = FilterCatalogItem;
        LibraryModsView = CollectionViewSource.GetDefaultView(Mods);
        LibraryModsView.Filter = FilterLibraryItem;
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
        _profiles.EnsureDefaults();

        var config = LoadConfig();
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
        var skipLocator = BranchSwitchEnabled
            || IsBranchWizardBlocking
            || File.Exists(_paths.BranchSwitchJournalPath);

        if ((BranchSwitchEnabled || skipLocator) && !string.IsNullOrWhiteSpace(branchCfg.SteamLinkPath))
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
    public ObservableCollection<TagFilterOption> CatalogAvailableTagOptions { get; }
    public ObservableCollection<TagFilterOption> LibraryAvailableTagOptions { get; }
    public IReadOnlyList<CategoryFilterOption> CatalogCategoryFilterOptions { get; private set; } = Array.Empty<CategoryFilterOption>();
    public IReadOnlyList<CategoryFilterOption> LibraryCategoryFilterOptions { get; private set; } = Array.Empty<CategoryFilterOption>();
    public IReadOnlyList<SortModeOption> SortModeOptions { get; private set; } = Array.Empty<SortModeOption>();
    public IReadOnlyList<LaunchModeOption> LaunchModeOptions { get; }
    public IReadOnlyList<LanguageOption> LanguageOptions { get; }
    public UiStrings Ui { get; }

    public bool IsReady => GameStatus?.Kind == GameStatusKind.Ready;

    public bool CanDeployOrLaunch =>
        IsReady && !IsAwaitingSteamSettle && !IsBranchWizardBlocking && !IsBranchSwitchBusy;

    public bool ShowConfirmManualBeta => IsAwaitingSteamSettle || DegradeToManualBeta;

    public bool CanSwitchGameBranch =>
        BranchSwitchEnabled && !IsAwaitingSteamSettle && !IsBranchSwitchBusy;

    public bool CanStartBranchWizard =>
        !IsAwaitingSteamSettle && !IsBranchSwitchBusy;

    public bool CanTeardownBranchSwitch =>
        BranchSwitchEnabled && !IsAwaitingSteamSettle && !IsBranchSwitchBusy;

    public string SettleConfirmButtonText =>
        DegradeToManualBeta
            ? LocalizationService.T("BranchSwitchConfirmManual")
            : LocalizationService.T("BranchSwitchConfirmSettle");

    public bool IsBranchWizardBlocking =>
        BranchWizardStep is not BranchWizardStep.None and not BranchWizardStep.Ready;

    public string StatusKindLabel => GameStatus?.Kind switch
    {
        GameStatusKind.Ready => "就绪",
        GameStatusKind.GameOkLoaderMissing => "缺少 Loader",
        GameStatusKind.LoaderPartial => "Loader 不完整",
        GameStatusKind.GameMissing => "未找到游戏",
        _ => "未知"
    };

    public string DirtyHint => IsDirty ? "方案已改，游戏目录未同步 — 请点击「应用方案」" : "已与游戏目录同步";

    [ObservableProperty] private GameStatus? _gameStatus;
    [ObservableProperty] private ProfileItemViewModel? _selectedProfile;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _riskBanner = "";
    [ObservableProperty] private string _loaderVersionWarning = "";
    [ObservableProperty] private string _firstAssemblyWarning = "";
    [ObservableProperty] private string _missingEnabledPackagesWarning = "";
    [ObservableProperty] private string _gamePath = "";
    [ObservableProperty] private LaunchMode _launchMode;
    [ObservableProperty] private bool _usePortableDataRoot;
    [ObservableProperty] private bool _settingsExpanded;
    [ObservableProperty] private bool _catalogExpanded;
    [ObservableProperty] private string _catalogStatus = "";
    [ObservableProperty] private CatalogModItemViewModel? _selectedCatalogMod;
    [ObservableProperty] private ModItemViewModel? _selectedLibraryMod;
    [ObservableProperty] private string _appVersion = UpdateChecker.ReadLocalVersion();
    [ObservableProperty] private string _updateStatus = "";
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

    partial void OnSelectedUiLanguageCodeChanged(string value)
    {
        if (_suppressLanguageSave) return;
        ApplyUiLanguage(value, save: true, refreshUi: true);
    }

    void ApplyUiLanguage(string? code, bool save, bool refreshUi)
    {
        var configured = string.IsNullOrWhiteSpace(code) ? "system" : code.Trim();
        var resolved = LocalizationService.ResolveConfiguredLanguage(configured);
        LocalizationService.Apply(resolved);

        _suppressLanguageSave = true;
        try
        {
            SelectedUiLanguageCode = configured;
            var system = LanguageOptions.FirstOrDefault(o => o.Code == "system");
            if (system is not null)
                system.Label = LocalizationService.T("LanguageSystem");
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

    partial void OnGameStatusChanged(GameStatus? value)
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(StatusKindLabel));
        NotifyBranchGates();
    }

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(DirtyHint));

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
        GameStatus = _detector.Detect(GamePath);
        UpdateLoaderVersionWarning();
        UpdateFirstAssemblyWarning();
        AppendLog(GameStatus.Message);
        TryOptimizeMelonLoader(logAlways: false);
        TryAutoImportFromGame();
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
        if (GameStatus?.Kind is not (GameStatusKind.Ready or GameStatusKind.LoaderPartial))
            return;

        try
        {
            var result = _melonOptimizer.ApplyRecommendedSettings(GamePath);
            if (result.Changed || logAlways || !_loggedMelonOptimize)
            {
                AppendLog(result.Message);
                _loggedMelonOptimize = true;
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

    bool ApplyProfile(bool ignoreBranchGate)
    {
        if (!ignoreBranchGate && (IsAwaitingSteamSettle || IsBranchWizardBlocking))
        {
            AppendLog("正在等待 Steam 结算或双服配置未完成，暂不可部署。");
            return false;
        }

        if (SelectedProfile is null)
        {
            AppendLog("未选择方案。");
            return false;
        }

        RefreshStatus();
        if (GameStatus?.Kind != GameStatusKind.Ready)
        {
            AppendLog(GameStatus?.Message ?? "游戏状态未就绪，无法部署。");
            return false;
        }

        try
        {
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
            if (!result.Success) return false;

            RecomputeDirty();
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"部署失败：{ex.Message}");
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeployOrLaunch))]
    void ApplyAndLaunch()
    {
        if (!ApplyProfile()) return;
        if (GameStatus?.Kind != GameStatusKind.Ready) return;

        TryOptimizeMelonLoader(logAlways: true);

        var config = LoadConfig();
        config.GamePath = GamePath;
        config.LaunchMode = LaunchMode;
        var launch = _launcher.Launch(config);
        if (!launch.Success)
            AppendLog(launch.Message);
        else
        {
            AppendLog("已请求启动游戏。");
            if (_melonOptimizer.NeedsFirstAssemblyGeneration(GamePath))
                AppendLog("首次启动提示：若长时间黑屏/控制台滚动，是 MelonLoader 正在生成程序集，请耐心等待完成。");
        }
    }

    [RelayCommand]
    async Task SwitchToOfficial() => await SwitchToBranchAsync(GameBranch.Official);

    [RelayCommand]
    async Task SwitchToBeta() => await SwitchToBranchAsync(GameBranch.Beta);

    [RelayCommand]
    async Task StartBranchWizard()
    {
        if (IsBranchSwitchBusy || IsAwaitingSteamSettle) return;

        IsBranchSwitchBusy = true;
        try
        {
            if (BranchSwitchEnabled)
            {
                if (!Confirm(LocalizationService.T("ConfirmBranchRebuildWizard")))
                    return;
                if (!await RunTeardownCoreAsync(deleteOtherStore: false).ConfigureAwait(true))
                    return;
            }

            var existing = _branchSwitch.LoadConfig();
            if (existing.WizardStep is BranchWizardStep.ArchivedA
                or BranchWizardStep.WaitingDownloadB
                or BranchWizardStep.ArchivedB
                or BranchWizardStep.Linked)
            {
                await ResumeWizardAsync(existing).ConfigureAwait(true);
                return;
            }

            await RunWizardFromStartAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog($"双服向导失败：{ex.Message}");
            _notify(string.Format(LocalizationService.T("NotifyBranchWizardFailed"), ex.Message));
        }
        finally
        {
            IsBranchSwitchBusy = false;
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    [RelayCommand]
    async Task TeardownBranchSwitch()
    {
        if (!BranchSwitchEnabled || IsBranchSwitchBusy || IsAwaitingSteamSettle) return;
        if (!Confirm(LocalizationService.T("ConfirmBranchTeardown")))
            return;

        var deleteOther = Confirm(LocalizationService.T("ConfirmBranchDeleteOtherStore"), MessageBoxResult.No);
        IsBranchSwitchBusy = true;
        try
        {
            await RunTeardownCoreAsync(deleteOther).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog($"解除双服配置失败：{ex.Message}");
            _notify(string.Format(LocalizationService.T("NotifyBranchTeardownFailed"), ex.Message));
        }
        finally
        {
            IsBranchSwitchBusy = false;
            NotifyBranchGates();
            RefreshBranchStatusText();
        }
    }

    bool Confirm(string message, MessageBoxResult defaultResult = MessageBoxResult.Yes) =>
        _confirmChoice?.Invoke(message, defaultResult) ?? _confirm(message);

    [RelayCommand]
    async Task ConfirmManualBeta()
    {
        if (!IsAwaitingSteamSettle && !DegradeToManualBeta) return;

        for (var i = 0; i < 8 && _processProbe.IsSteamRunning(); i++)
            await _delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(true);

        if (_processProbe.IsSteamRunning())
        {
            _notify(LocalizationService.T("NotifySteamStillRunningSettle"));
            if (!Confirm(LocalizationService.T("ConfirmDeployWhileSteamRunning")))
                return;
        }

        DeployBoundProfileAndClearSettle();
    }

    [RelayCommand]
    void ToggleSettings() => SettingsExpanded = !SettingsExpanded;

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

            var (ok, _, domestic) = GitHubCommunityLinks.TryOpenCompose(
                compose.Subject,
                compose.Body,
                TryCopyText);

            if (!ok)
            {
                AppendLog($"{Ui.MailOpenFailed}：{GitHubCommunityLinks.Inbox}");
                _notify(Ui.MailOpenFailed);
                return Task.CompletedTask;
            }

            var msg = domestic ? Ui.ReportMailOpenedDomestic : Ui.ReportMailOpenedInternational;
            AppendLog($"{msg}\n{GitHubCommunityLinks.Inbox}");
            _notify(msg);
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
        var (ok, _, domestic) = GitHubCommunityLinks.TryOpenCompose(
            compose.Subject,
            compose.Body,
            TryCopyText);

        if (!ok)
        {
            AppendLog($"{Ui.MailOpenFailed}：{GitHubCommunityLinks.Inbox}");
            _notify(Ui.MailOpenFailed);
            return;
        }

        var msg = domestic ? Ui.SubmitMailOpenedDomestic : Ui.SubmitMailOpenedInternational;
        AppendLog(msg);
        _notify(msg);
    }

    [RelayCommand]
    void ToggleCatalog()
    {
        CatalogExpanded = !CatalogExpanded;
        if (CatalogExpanded && CatalogMods.Count == 0)
            _ = RefreshCatalogAsync();
    }

    [RelayCommand]
    async Task RefreshCatalogAsync()
    {
        if (_checkingCatalog) return;
        _checkingCatalog = true;
        CatalogStatus = "正在拉取目录…";
        AppendLog("正在拉取 Mod 目录…");
        try
        {
            var root = await _catalog.FetchCatalogAsync().ConfigureAwait(true);
            var packages = _library.List();
            var previousId = SelectedCatalogMod?.Id;

            CatalogMods.Clear();
            foreach (var mod in root.Mods)
            {
                LogInvalidCatalogCategory(mod);
                var inLib = ModCatalogService.IsInLibraryByFileName(packages, mod.File);
                CatalogMods.Add(new CatalogModItemViewModel(mod, inLib));
            }

            SelectedCatalogMod = CatalogMods.FirstOrDefault(m =>
                previousId is not null &&
                string.Equals(m.Id, previousId, StringComparison.OrdinalIgnoreCase))
                ?? CatalogMods.FirstOrDefault();

            EnrichModsFromCatalog();
            if (SelectedLibraryMod is not null)
                UpdateLibraryModDetail();

            RefreshCatalogView();
            RefreshLibraryView();

            var updated = string.IsNullOrWhiteSpace(root.UpdatedAt) ? "未知" : root.UpdatedAt;
            CatalogStatus = $"已加载 {CatalogMods.Count} 个条目（目录更新：{updated}）";
            AppendLog(CatalogStatus);
        }
        catch (Exception ex)
        {
            CatalogStatus =
                $"拉取目录失败：{ex.Message}。请确认可访问 GitHub（raw.githubusercontent.com），必要时配置代理。";
            AppendLog(CatalogStatus);
        }
        finally
        {
            _checkingCatalog = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddCatalogMod))]
    async Task AddCatalogModToLibraryAsync()
    {
        if (SelectedCatalogMod is null || _addingCatalogMod) return;
        _addingCatalogMod = true;
        AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();

        var item = SelectedCatalogMod;
        try
        {
            if (item.IsInLibrary)
            {
                AppendLog($"「{item.Name}」已在本地库（同名文件），跳过下载。");
                CatalogStatus = $"已在本地库：{item.Name}";
                return;
            }

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
            CatalogStatus = $"正在下载 {item.Name}…";
            AppendLog(CatalogStatus);
            await _catalog.DownloadModAsync(item.Mod, tempPath).ConfigureAwait(true);

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
                        preview: item.Mod.Preview);
                }
                catch (Exception metaEx)
                {
                    AppendLog($"写入目录元数据失败：{metaEx.Message}");
                }

                AppendLog($"已从目录加入本地库：{pkg.DisplayName} ({pkg.Id})（未启用）");
                CatalogStatus = $"已加入本地库：{pkg.DisplayName}";
                ReloadMods();
                RefreshCatalogInLibraryFlags();
                UpdateLoaderVersionWarning();
                UpdateFirstAssemblyWarning();
                RecomputeDirty();
                SelectedLibraryMod = Mods.FirstOrDefault(m =>
                    string.Equals(m.Package.Id, pkg.Id, StringComparison.OrdinalIgnoreCase));
                UpdateLibraryModDetail();
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
        finally
        {
            _addingCatalogMod = false;
            AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
        }
    }

    bool CanAddCatalogMod() => SelectedCatalogMod is not null && !_addingCatalogMod;

    partial void OnSelectedCatalogModChanged(CatalogModItemViewModel? value)
    {
        AddCatalogModToLibraryCommand.NotifyCanExecuteChanged();
        _ = value?.LoadPreviewImageAsync();
    }

    partial void OnSelectedLibraryModChanged(ModItemViewModel? value)
    {
        EditLibraryModTaxonomyCommand.NotifyCanExecuteChanged();
        UpdateLibraryModDetail();
    }

    [RelayCommand]
    void ClearLibraryModSelection() => SelectedLibraryMod = null;

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
            ?? (string.IsNullOrWhiteSpace(item.Package.Preview)
                ? null
                : ModCatalogService.GetRawUrl(item.Package.Preview));
        _ = item.LoadPreviewImageAsync(previewUrl);
    }

    async Task SoftRefreshCatalogForLibraryDetailAsync()
    {
        if (_checkingCatalog || CatalogMods.Count > 0) return;
        _checkingCatalog = true;
        try
        {
            var root = await _catalog.FetchCatalogAsync().ConfigureAwait(true);
            var packages = _library.List();
            CatalogMods.Clear();
            foreach (var mod in root.Mods)
            {
                LogInvalidCatalogCategory(mod);
                var inLib = ModCatalogService.IsInLibraryByFileName(packages, mod.File);
                CatalogMods.Add(new CatalogModItemViewModel(mod, inLib));
            }

            EnrichModsFromCatalog();
            if (SelectedLibraryMod is not null)
                UpdateLibraryModDetail();
            RefreshCatalogView();
            RefreshLibraryView();
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

    CatalogModItemViewModel? FindCatalogMatch(ModItemViewModel item)
    {
        foreach (var catalog in CatalogMods)
        {
            var catalogFile = Path.GetFileName((catalog.File ?? "").Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(catalogFile))
                continue;

            foreach (var file in item.Package.Files)
            {
                var localName = Path.GetFileName((file.RelativePathInPackage ?? "").Replace('\\', '/'));
                if (string.Equals(localName, catalogFile, StringComparison.OrdinalIgnoreCase))
                    return catalog;
            }
        }

        return null;
    }

    void EnrichModsFromCatalog()
    {
        if (CatalogMods.Count == 0) return;
        foreach (var mod in Mods)
        {
            if (mod.IsMissing) continue;
            var match = FindCatalogMatch(mod);
            if (match is null) continue;
            mod.ApplyCatalogEnrichment(match.Mod);
        }
    }

    void RefreshCatalogInLibraryFlags()
    {
        var packages = _library.List();
        foreach (var item in CatalogMods)
            item.IsInLibrary = ModCatalogService.IsInLibraryByFileName(packages, item.File);
    }

    [RelayCommand]
    async Task CheckForUpdatesAsync()
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        UpdateStatus = "正在检查更新…";
        AppendLog("正在检查更新…");
        try
        {
            var result = await _updateChecker.CheckAsync().ConfigureAwait(true);
            UpdateStatus = result.Message;
            AppendLog(result.Message);

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
                var fallback = $"https://github.com/{UpdateChecker.Owner}/{UpdateChecker.Repo}/releases/latest";
                if (Confirm(string.Format(LocalizationService.T("ConfirmOpenGitHubReleases"), result.Message)))
                    TryOpenUrl(fallback);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"检查更新失败：{ex.Message}";
            AppendLog(UpdateStatus);
        }
        finally
        {
            _checkingUpdates = false;
        }
    }

    static bool TryOpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
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
            IsDirty = true;
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

        if (!string.Equals(manifest.ProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            IsDirty = desired.Count > 0 || manifest.Files.Count > 0;
            return;
        }

        if (!PathsEqual(manifest.GamePath, GamePath))
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
            DegradeToManualBeta = IsAwaitingSteamSettle;
            SelectBoundProfile(ActiveGameBranch);
            if (cfg.Enabled
                && !string.IsNullOrWhiteSpace(cfg.OfficialStorePath)
                && !string.IsNullOrWhiteSpace(cfg.BetaStorePath))
            {
                // Do not notify on every startup; only log if something changed/failed.
                try
                {
                    var sync = _melonDualSync.EnsureOnBothStores(cfg.OfficialStorePath, cfg.BetaStorePath);
                    if (!sync.Success || (sync.Message?.Contains("安装", StringComparison.Ordinal) == true)
                        || (sync.Message?.Contains("复制", StringComparison.Ordinal) == true))
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
        }
        catch
        {
            BranchSwitchEnabled = false;
            IsAwaitingSteamSettle = false;
            DegradeToManualBeta = false;
            BranchWizardStep = BranchWizardStep.None;
            RefreshBranchStatusText();
            NotifyBranchGates();
        }
        finally
        {
            _suppressBranchSwitchSave = false;
        }
    }

    async Task SwitchToBranchAsync(GameBranch target)
    {
        if (!BranchSwitchEnabled || IsBranchSwitchBusy || IsAwaitingSteamSettle) return;

        if (_branchSwitch.IsAlignedWith(target))
        {
            var already = LocalizationService.T("NotifyAlreadyOnGameBranch");
            AppendLog(already);
            _notify(already);
            return;
        }

        if (!Confirm(LocalizationService.T("ConfirmSwitchGameBranch")))
            return;

        IsBranchSwitchBusy = true;
        try
        {
            if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
                return;

            // Capture leaving branch depot metadata while ACF still matches that store.
            var leaveBranch = ActiveGameBranch;
            var snap = _branchSwitch.TrySnapshotSettledAcf(leaveBranch);
            if (!snap.Success && !string.IsNullOrWhiteSpace(snap.Message))
                AppendLog($"切服前未保存 {leaveBranch} ACF 快照：{snap.Message}");

            var swap = _branchSwitch.TrySwapJunction(target);
            if (!swap.Success)
            {
                AppendLog(string.IsNullOrWhiteSpace(swap.Message) ? LocalizationService.T("LogSwapFolderFailed") : swap.Message);
                return;
            }

            _suppressBranchSwitchSave = true;
            try
            {
                ActiveGameBranch = target;
            }
            finally
            {
                _suppressBranchSwitchSave = false;
            }

            SelectBoundProfile(target);

            EnsureMelonLoaderForDualStores(preferTarget: target);

            var silent = _branchSwitch.TryPrepareSteamBranchMetadata(target);
            if (silent.Success
                && string.Equals(silent.Message, "restored-acf-snapshot", StringComparison.Ordinal))
            {
                AppendLog("已恢复目标服 Steam 清单快照（避免重复下载）");
            }

            await SettleAfterSilentBetaAsync(silent).ConfigureAwait(true);
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

        var current = Confirm(LocalizationService.T("ConfirmCurrentIsOfficial"))
            ? GameBranch.Official
            : GameBranch.Beta;

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

        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
            return;

        var cfg = _branchSwitch.LoadConfig();
        cfg.SteamLinkPath = steamLink;
        cfg.OfficialStorePath = officialStore;
        cfg.BetaStorePath = betaStore;
        cfg.ActiveBranch = current;
        cfg.BetaBranchName = betaName.Trim();
        cfg.Enabled = false;
        cfg.WizardStep = BranchWizardStep.Declared;
        _branchSwitch.SaveConfig(cfg);

        _suppressBranchSwitchSave = true;
        try
        {
            BetaBranchName = cfg.BetaBranchName;
            ActiveGameBranch = current;
            BranchWizardStep = BranchWizardStep.Declared;
        }
        finally
        {
            _suppressBranchSwitchSave = false;
        }

        var archiveA = _branchSwitch.ArchiveCurrentAs(current);
        if (!archiveA.Success)
        {
            FailWizard(string.IsNullOrWhiteSpace(archiveA.Message) ? LocalizationService.T("FailWizardArchiveCurrentFailed") : archiveA.Message);
            return;
        }

        SetWizardStep(BranchWizardStep.WaitingDownloadB);
        await ContinueWizardAfterArchiveAAsync(current).ConfigureAwait(true);
    }

    async Task ResumeWizardAsync(BranchSwitchConfig cfg)
    {
        var current = cfg.ActiveBranch;
        if (cfg.WizardStep is BranchWizardStep.ArchivedB or BranchWizardStep.Linked)
        {
            await FinishWizardAfterStoresReadyAsync(current).ConfigureAwait(true);
            return;
        }

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

        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
            return;

        var silentOther = _branchSwitch.TrySilentSetBeta(other);
        if (!silentOther.Success)
        {
            _notify(string.IsNullOrWhiteSpace(silentOther.Message)
                ? LocalizationService.T("NotifySilentBetaFailedOther")
                : silentOther.Message);
        }

        if (_steamRestartCooldown > TimeSpan.Zero)
            await _delay(_steamRestartCooldown).ConfigureAwait(true);
        TryStartSteam();

        if (!Confirm(LocalizationService.T("ConfirmDownloadOtherBranchContinue")))
        {
            AppendLog(LocalizationService.T("LogDualBranchWizardPaused"));
            return;
        }

        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
            return;

        var archiveB = _branchSwitch.ArchiveDownloadedAs(other);
        if (!archiveB.Success)
        {
            FailWizard(string.IsNullOrWhiteSpace(archiveB.Message) ? LocalizationService.T("FailWizardArchiveOtherFailed") : archiveB.Message);
            return;
        }

        await FinishWizardAfterStoresReadyAsync(current).ConfigureAwait(true);
    }

    async Task FinishWizardAfterStoresReadyAsync(GameBranch current)
    {
        var link = _branchSwitch.CreateLinkTo(current);
        if (!link.Success)
        {
            FailWizard(string.IsNullOrWhiteSpace(link.Message) ? LocalizationService.T("FailWizardCreateLinkFailed") : link.Message);
            return;
        }

        _branchSwitch.MigrateLegacyManifestIfNeeded(current);

        _suppressBranchSwitchSave = true;
        try
        {
            BranchSwitchEnabled = true;
            ActiveGameBranch = current;
            BranchWizardStep = BranchWizardStep.Linked;
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
        });

        RefreshStatus();

        EnsureMelonLoaderForDualStores(preferTarget: current);

        var silent = _branchSwitch.TrySilentSetBeta(current);
        await SettleAfterSilentBetaAsync(silent, LocalizationService.T("NotifyWizardDoneWaitingSteam")).ConfigureAwait(true);
    }

    void EnsureMelonLoaderForDualStores(GameBranch? preferTarget = null)
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
            }
            else if (preferTarget is not null)
            {
                // Refresh after ensuring the currently linked path is ready.
                RefreshStatus();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"补齐双服 MelonLoader 失败：{ex.Message}");
        }
    }

    async Task<bool> RunTeardownCoreAsync(bool deleteOtherStore)
    {
        if (!await WaitForSteamAndGameExitAsync().ConfigureAwait(true))
            return false;

        var result = _branchSwitch.TryTeardown(deleteOtherStore);
        if (!result.Success)
        {
            FailWizard(string.IsNullOrWhiteSpace(result.Message) ? LocalizationService.T("FailWizardTeardownFailed") : result.Message);
            return false;
        }

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
        NotifyBranchGates();
        RefreshBranchStatusText();
        AppendLog(LocalizationService.T("LogBranchTeardownDone"));
        _notify(LocalizationService.T("NotifyBranchTeardownDone"));
        return true;
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
        AppendLog(message);
        _notify(message);
    }

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

    async Task<bool> WaitForSteamAndGameExitAsync()
    {
        if (!_processProbe.IsGameOrSteamRunning())
            return true;

        try
        {
            _processStarter.StartShell("steam://exit");
        }
        catch (Exception ex)
        {
            AppendLog(string.Format(LocalizationService.T("LogSteamExitRequestFailed"), ex.Message));
        }

        var deadline = DateTime.UtcNow + _steamExitTimeout;
        while (_processProbe.IsGameOrSteamRunning() && DateTime.UtcNow < deadline)
            await _delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(true);

        if (_processProbe.IsGameOrSteamRunning())
        {
            AppendLog(LocalizationService.T("LogSteamOrGameStillRunning"));
            return false;
        }

        if (_steamExitCooldown > TimeSpan.Zero)
            await _delay(_steamExitCooldown).ConfigureAwait(true);

        // Re-check after cooldown: steamwebhelper often lags behind steam.exe.
        if (_processProbe.IsGameOrSteamRunning())
        {
            AppendLog(LocalizationService.T("LogSteamOrGameStillRunning"));
            return false;
        }

        return true;
    }

    void TryStartSteam()
    {
        try
        {
            _processStarter.StartShell("steam://open/games");
        }
        catch (Exception ex)
        {
            AppendLog($"启动 Steam 失败：{ex.Message}");
        }
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
        TryStartSteam();
        if (!string.IsNullOrWhiteSpace(successLog))
            AppendLog(successLog);
    }

    void DeployBoundProfileAndClearSettle()
    {
        SelectBoundProfile(ActiveGameBranch);
        ApplyProfile(ignoreBranchGate: true);
        var snap = _branchSwitch.TrySnapshotSettledAcf(ActiveGameBranch);
        if (snap.Success)
            AppendLog($"已保存 {ActiveGameBranch} Steam 清单快照，供下次切服免下载");
        else if (!string.IsNullOrWhiteSpace(snap.Message))
            AppendLog($"结算后未保存 ACF 快照：{snap.Message}");
        ClearSteamSettle();
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
        });
        NotifyBranchGates();
        RefreshBranchStatusText();
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
        OnPropertyChanged(nameof(IsBranchWizardBlocking));
        OnPropertyChanged(nameof(CanSwitchGameBranch));
        OnPropertyChanged(nameof(CanStartBranchWizard));
        OnPropertyChanged(nameof(CanTeardownBranchSwitch));
        ApplyProfileCommand.NotifyCanExecuteChanged();
        ApplyAndLaunchCommand.NotifyCanExecuteChanged();
    }

    void RefreshBranchStatusText()
    {
        if (!BranchSwitchEnabled)
            BranchStatusText = BranchWizardStep is BranchWizardStep.None
                ? LocalizationService.T("BranchStatusUnconfigured")
                : LocalizationService.T("BranchStatusIncomplete");
        else if (IsAwaitingSteamSettle || BranchWizardStep == BranchWizardStep.AwaitingSteamSettle)
            BranchStatusText = LocalizationService.T("BranchStatusWaitingSteam");
        else if (IsBranchWizardBlocking)
            BranchStatusText = LocalizationService.T("BranchStatusIncomplete");
        else
            BranchStatusText = ActiveGameBranch == GameBranch.Official
                ? LocalizationService.T("BranchStatusOfficial")
                : LocalizationService.T("BranchStatusBeta");
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
    }

    AppConfig LoadConfig() =>
        _store.LoadOrDefault(_paths.ConfigPath, () => new AppConfig());

    void SaveConfig(AppConfig config) => _store.Save(_paths.ConfigPath, config);

    void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogText = string.IsNullOrEmpty(LogText) ? line : LogText + Environment.NewLine + line;
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
