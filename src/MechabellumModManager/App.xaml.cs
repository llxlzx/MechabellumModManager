using System.IO;
using System.Windows;
using MechabellumModManager.Dialogs;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Microsoft.Win32;

namespace MechabellumModManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        window.DataContext = ComposeMainViewModel(window);
        MainWindow = window;
        window.Show();
    }

    static MainViewModel ComposeMainViewModel(MainWindow window)
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
            promptEditTaxonomy: pkg => PromptEditTaxonomy(window, pkg),
            copyText: text => Clipboard.SetText(text),
            unselectLibrary: window.UnselectLibraryMods,
            unselectCatalog: window.UnselectCatalogMods,
            branchSwitch: branchSwitch,
            processProbe: probe,
            processStarter: starter);
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
            Title = "选择 Mechabellum 游戏目录"
        };
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

    static string? BrowseImportFolder(Window owner)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要导入的文件夹"
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

    static bool PromptSubmitGuide(Window owner)
    {
        var dialog = new SubmitGuideDialog { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    static (string? Override, IReadOnlyList<string> ExtraTags)? PromptEditTaxonomy(Window owner, ModPackage package)
    {
        var ui = (owner.DataContext as MainViewModel)?.Ui ?? new UiStrings();
        var dialog = new EditModTaxonomyDialog(package, ui) { Owner = owner };
        if (dialog.ShowDialog() != true)
            return null;
        return dialog.Result;
    }
}
