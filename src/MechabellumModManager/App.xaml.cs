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

    static MainViewModel ComposeMainViewModel(Window owner)
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
        var launcher = new GameLauncher(new ShellProcessStarter(), probe);
        var riskGate = new RiskGate();
        var config = store.LoadOrDefault(paths.ConfigPath, () => new AppConfig());
        var relay = new RelayClient(config.RelayBaseUrl);

        return new MainViewModel(
            paths,
            store,
            detector,
            library,
            profiles,
            deploy,
            launcher,
            riskGate,
            relay: relay,
            assemblyInspector: inspector,
            confirmHighRisk: msg => Confirm(owner, msg, LocalizationService.T("Confirm"), MessageBoxImage.Warning),
            confirm: msg => Confirm(owner, msg, LocalizationService.T("Confirm"), MessageBoxImage.Question),
            browseFolder: () => BrowseFolder(owner),
            openDll: () => OpenFile(owner, "Melon Mod DLL|*.dll|所有文件|*.*"),
            openZip: () => OpenFile(owner, "Mod 压缩包|*.zip|所有文件|*.*"),
            promptText: title => Prompt(owner, title),
            pickPackageType: () => PickPackageType(owner),
            openFolder: () => BrowseImportFolder(owner),
            promptReport: modName => PromptReport(owner, modName),
            promptSubmitMod: () => PromptSubmitMod(owner));
    }

    static PathsService ResolvePaths(JsonStore store)
    {
        var appData = new PathsService();
        var config = store.LoadOrDefault(appData.ConfigPath, () => new AppConfig());
        if (!string.IsNullOrWhiteSpace(config.DataRoot))
            return new PathsService(config.DataRoot);
        return appData;
    }

    static bool Confirm(Window owner, string message, string title, MessageBoxImage icon) =>
        MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;

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
            Owner = owner,
            Title = title
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

    static (ReportCategory Category, string Notes)? PromptReport(Window owner, string modName)
    {
        var dialog = new ReportModDialog(modName) { Owner = owner };
        if (dialog.ShowDialog() != true)
            return null;
        return (dialog.Category, dialog.Notes);
    }

    static SubmitModFields? PromptSubmitMod(Window owner)
    {
        var dialog = new SubmitModDialog { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result is null)
            return null;
        var r = dialog.Result;
        return new SubmitModFields
        {
            DllPath = r.DllPath,
            Name = r.Name,
            Author = r.Author,
            Version = r.Version,
            Summary = r.Summary
        };
    }
}
