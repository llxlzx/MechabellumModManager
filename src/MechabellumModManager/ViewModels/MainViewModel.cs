using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    readonly RiskHeuristic _riskHeuristic;
    readonly SteamGameLocator _steamLocator;
    readonly UpdateChecker _updateChecker;
    readonly Func<string, bool> _confirmHighRisk;
    readonly Func<string, bool> _confirm;
    readonly Func<string?>? _browseFolder;
    readonly Func<string?>? _openDll;
    readonly Func<string?>? _openZip;
    readonly Func<string, string?>? _promptText;
    readonly Func<ModPackageType?>? _pickPackageType;
    readonly Func<string?>? _openFolder;
    bool _loggedMelonOptimize;
    bool _checkingUpdates;

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
        RiskHeuristic? riskHeuristic = null,
        SteamGameLocator? steamLocator = null,
        UpdateChecker? updateChecker = null,
        Func<string, bool>? confirmHighRisk = null,
        Func<string, bool>? confirm = null,
        Func<string?>? browseFolder = null,
        Func<string?>? openDll = null,
        Func<string?>? openZip = null,
        Func<string, string?>? promptText = null,
        Func<ModPackageType?>? pickPackageType = null,
        Func<string?>? openFolder = null)
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
        _riskHeuristic = riskHeuristic ?? new RiskHeuristic();
        _steamLocator = steamLocator ?? new SteamGameLocator();
        _updateChecker = updateChecker ?? new UpdateChecker();
        // Default deny: UI must wire confirmation dialogs.
        _confirmHighRisk = confirmHighRisk ?? (_ => false);
        _confirm = confirm ?? (_ => false);
        _browseFolder = browseFolder;
        _openDll = openDll;
        _openZip = openZip;
        _promptText = promptText;
        _pickPackageType = pickPackageType;
        _openFolder = openFolder;
        ApplyProfileCommand = new RelayCommand(() => _ = ApplyProfile(), () => IsReady);

        Profiles = new ObservableCollection<ProfileItemViewModel>();
        Mods = new ObservableCollection<ModItemViewModel>();
        RiskBanner = RiskGate.BannerText;
        LaunchModeOptions = new[]
        {
            new LaunchModeOption(LaunchMode.SteamThenExe, "Steam 优先，失败则直启"),
            new LaunchModeOption(LaunchMode.SteamOnly, "仅 Steam"),
            new LaunchModeOption(LaunchMode.ExeOnly, "仅直启 exe")
        };

        _paths.EnsureCreated();
        _profiles.EnsureDefaults();

        var config = LoadConfig();
        _gamePath = ResolveInitialGamePath(config.GamePath);
        _launchMode = config.LaunchMode;
        _usePortableDataRoot = IsPortableRoot(config.DataRoot);

        if (!string.Equals(config.GamePath ?? "", _gamePath, StringComparison.OrdinalIgnoreCase))
        {
            config.GamePath = _gamePath;
            SaveConfig(config);
        }

        ReloadProfiles(selectId: config.ActiveProfileId);
        RefreshStatus();
        ReloadMods();
        RecomputeDirty();
        UpdateLoaderVersionWarning();

        if (string.IsNullOrWhiteSpace(_gamePath))
            AppendLog("未自动找到游戏目录。请在「设置」中浏览选择 Mechabellum 安装路径。");
        else if (!SteamGameLocator.LooksLikeGameRoot(_gamePath))
            AppendLog("当前游戏路径无效。请在「设置」中重新选择包含 Mechabellum.exe 的目录。");
    }

    string ResolveInitialGamePath(string? configured)
    {
        if (SteamGameLocator.LooksLikeGameRoot(configured))
            return Path.GetFullPath(configured!);

        var found = _steamLocator.TryFind();
        if (!string.IsNullOrWhiteSpace(found))
        {
            AppendLog($"已自动定位游戏目录：{found}");
            return found;
        }

        return configured?.Trim() ?? "";
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }
    public ObservableCollection<ModItemViewModel> Mods { get; }
    public IReadOnlyList<LaunchModeOption> LaunchModeOptions { get; }

    public bool IsReady => GameStatus?.Kind == GameStatusKind.Ready;

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
    [ObservableProperty] private string _missingEnabledPackagesWarning = "";
    [ObservableProperty] private string _gamePath = "";
    [ObservableProperty] private LaunchMode _launchMode;
    [ObservableProperty] private bool _usePortableDataRoot;
    [ObservableProperty] private bool _settingsExpanded;
    [ObservableProperty] private string _appVersion = UpdateChecker.ReadLocalVersion();
    [ObservableProperty] private string _updateStatus = "";

    partial void OnGameStatusChanged(GameStatus? value)
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(StatusKindLabel));
        ApplyProfileCommand.NotifyCanExecuteChanged();
        ApplyAndLaunchCommand.NotifyCanExecuteChanged();
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

        ReloadMods();
        RecomputeDirty();
        UpdateLoaderVersionWarning();
    }

    [RelayCommand]
    void RefreshStatus()
    {
        GameStatus = _detector.Detect(GamePath);
        UpdateLoaderVersionWarning();
        AppendLog(GameStatus.Message);
        TryOptimizeMelonLoader(logAlways: false);
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
    public bool ApplyProfile()
    {
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
            var result = _deploy.Apply(profile, packages, GamePath, allowOverwriteUnmanaged: false);

            if (!result.Success &&
                result.Plan.ConflictsUnmanaged.Count > 0 &&
                result.Plan.IntraProfileNameCollisions.Count == 0)
            {
                var sample = string.Join("\n", result.Plan.ConflictsUnmanaged.Take(8).Select(Path.GetFileName));
                var more = result.Plan.ConflictsUnmanaged.Count > 8
                    ? $"\n…共 {result.Plan.ConflictsUnmanaged.Count} 个"
                    : "";
                var prompt =
                    "检测到游戏目录中已有非本管理器托管的同名文件。\n" +
                    "确认覆盖并接管这些文件？\n\n" + sample + more;
                if (_confirm(prompt))
                    result = _deploy.Apply(profile, packages, GamePath, allowOverwriteUnmanaged: true);
                else
                {
                    AppendLog("已取消覆盖非托管文件。");
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

    [RelayCommand(CanExecute = nameof(IsReady))]
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
    void ToggleSettings() => SettingsExpanded = !SettingsExpanded;

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
                var detail = $"{result.Message}\n\n{result.Notes}\n\n是否打开下载链接？\n{result.SetupUrl}";
                if (_confirm(detail))
                    OpenUrl(result.SetupUrl!);
            }
            else if (result.Kind == UpdateCheckKind.Failed)
            {
                var fallback = $"https://github.com/{UpdateChecker.Owner}/{UpdateChecker.Repo}/releases/latest";
                if (_confirm($"{result.Message}\n\n是否打开 GitHub Releases 页面？"))
                    OpenUrl(fallback);
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

    static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Ignore — user can copy URL from log / dialog.
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
        if (!_confirm($"确定删除方案「{SelectedProfile.Name}」？此操作不可撤销。"))
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
        if (!_confirm($"确定从库中删除「{mod.DisplayName}」？将同时从所有方案中移除。"))
            return;
        try
        {
            _library.Delete(mod.Package.Id);
            ReloadMods();
            RecomputeDirty();
            UpdateLoaderVersionWarning();
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

        Mods.Clear();
        foreach (var pkg in library.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
            Mods.Add(new ModItemViewModel(this, pkg, enabled.Contains(pkg.Id)));

        var missing = enabled.Where(id => !libraryIds.Contains(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var id in missing)
            Mods.Add(ModItemViewModel.CreateMissing(this, id));

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
        var manifest = _store.LoadOrDefault(_paths.DeployManifestPath, () => new DeployManifest());

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
            files = pkg.Files
        };
        var json = JsonSerializer.Serialize(meta, PackageJsonOptions);
        File.WriteAllText(Path.Combine(pkg.PackageDirectory, "package.json"), json);
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
