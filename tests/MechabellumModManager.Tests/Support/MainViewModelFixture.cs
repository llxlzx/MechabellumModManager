using System.Windows;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

namespace MechabellumModManager.Tests.Support;

public sealed class MainViewModelFixture : IDisposable
{
    public string DataRoot { get; }
    public string GameRoot { get; }
    public string SteamLink { get; set; } = "";
    public string OfficialStore { get; set; } = "";
    public string BetaStore { get; set; } = "";
    public PathsService Paths { get; }
    public JsonStore Store { get; }
    public ProfileService Profiles { get; }
    public ModLibraryService Library { get; }
    public FakeProcessProbe Probe { get; } = new();
    public JunctionService Junctions { get; } = new();
    public CriticalOpGuard? Guard { get; set; }

    readonly GameDetector _detector;
    readonly DeployService _deploy;
    readonly BranchSwitchService _branchSwitch;

    MainViewModelFixture(string dataRoot, string gameRoot)
    {
        DataRoot = dataRoot;
        GameRoot = gameRoot;
        Paths = new PathsService(dataRoot);
        Paths.EnsureCreated();
        Store = new JsonStore();
        Profiles = new ProfileService(Paths, Store);
        Profiles.EnsureDefaults();
        Library = new ModLibraryService(Paths, new AssemblyInspector(), Store, Profiles);
        _detector = new GameDetector();
        _deploy = new DeployService(Paths, Store, new DeployPlanner(), _detector, new ProcessProbe());
        _branchSwitch = new BranchSwitchService(
            Paths, Store, Probe, Junctions, new SteamBetaKeyEditor(Probe));
    }

    public static MainViewModelFixture CreateReady(bool highRisk = false)
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-vmfx-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-vmfx-game-" + Guid.NewGuid().ToString("N"));
        CreateReadyGame(gameRoot);

        var fx = new MainViewModelFixture(dataRoot, gameRoot);
        SeedLibrary(fx, highRisk);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = gameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
        return fx;
    }

    public static MainViewModelFixture CreateReadyForCriticalOp()
    {
        var fx = CreateReady();
        fx.Guard = new CriticalOpGuard(fx.Paths);
        return fx;
    }

    public static MainViewModelFixture CreateReadyDualFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-vmfx-d-" + Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "data");
        var steamapps = Path.Combine(root, "steamapps");
        var common = Path.Combine(steamapps, "common");
        Directory.CreateDirectory(common);

        var official = Path.Combine(common, "Mechabellum_official");
        var beta = Path.Combine(common, "Mechabellum_beta");
        var steamLink = Path.Combine(common, "Mechabellum");
        CreateReadyGame(official);
        CreateReadyGame(beta);
        File.WriteAllText(Path.Combine(official, "marker.txt"), "official");
        File.WriteAllText(Path.Combine(beta, "marker.txt"), "beta");

        var fx = new MainViewModelFixture(dataRoot, steamLink);
        fx.SteamLink = steamLink;
        fx.OfficialStore = official;
        fx.BetaStore = beta;
        fx.Junctions.CreateJunction(steamLink, official);
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"oldbeta"
            	}
            }
            """);

        SeedLibrary(fx);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = steamLink,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
        return fx;
    }

    public static MainViewModelFixture CreateWizardStart()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-vmfx-w-" + Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "data");
        var steamapps = Path.Combine(root, "steamapps");
        var common = Path.Combine(steamapps, "common");
        Directory.CreateDirectory(common);

        var steamLink = Path.Combine(common, "Mechabellum");
        var official = Path.Combine(common, "Mechabellum_official");
        var beta = Path.Combine(common, "Mechabellum_beta");
        SeedGameRoot(steamLink, "current");
        CreateReadyGame(steamLink);

        var fx = new MainViewModelFixture(dataRoot, steamLink);
        fx.SteamLink = steamLink;
        fx.OfficialStore = official;
        fx.BetaStore = beta;
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"UserConfig"
            	{
            		"language"		"english"
            		"BetaKey"		"oldbeta"
            	}
            }
            """);

        SeedLibrary(fx);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = steamLink,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
        return fx;
    }

    public static MainViewModelFixture CreateMissingAssemblies()
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-vmfx-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-vmfx-game-" + Guid.NewGuid().ToString("N"));
        CreateMissingAssembliesGame(gameRoot);
        var fx = new MainViewModelFixture(dataRoot, gameRoot);
        fx.Guard = new CriticalOpGuard(fx.Paths);
        SeedLibrary(fx);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = gameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
        return fx;
    }

    public static MainViewModelFixture CreateLoaderPresentAssembliesMissing(bool highRisk = false)
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-vmfx-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-vmfx-game-" + Guid.NewGuid().ToString("N"));
        CreateLoaderMissingAssembliesGame(gameRoot);
        var fx = new MainViewModelFixture(dataRoot, gameRoot);
        SeedLibrary(fx, highRisk);
        fx.Store.Save(fx.Paths.ConfigPath, new AppConfig
        {
            GamePath = gameRoot,
            ActiveProfileId = "default",
            LaunchMode = LaunchMode.ExeOnly
        });
        return fx;
    }

    static void SeedLibrary(MainViewModelFixture fx, bool highRisk = false)
    {
        var pkgId = highRisk ? "cheat-aaaaaaaa" : "cam-aaaaaaaa";
        var displayName = highRisk ? "CheatCam" : "Cam";
        var dllName = highRisk ? "CheatCam.dll" : "Cam.dll";
        var pkgDir = Path.Combine(fx.Paths.LibraryRoot, "mods", pkgId);
        Directory.CreateDirectory(pkgDir);
        File.WriteAllBytes(Path.Combine(pkgDir, dllName), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
        var highRiskJson = highRisk ? "true" : "false";
        File.WriteAllText(
            Path.Combine(pkgDir, "package.json"),
            $$"""
            {
              "id": "{{pkgId}}",
              "displayName": "{{displayName}}",
              "type": "melon_mod",
              "highRisk": {{highRiskJson}},
              "files": [ { "relativePathInPackage": "{{dllName}}", "sha256": "aabbccdd" } ]
            }
            """);
        fx.Store.Save(Path.Combine(fx.Paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });
    }

    public MainViewModel CreateVm(
        Func<string, bool>? confirm = null,
        IProcessStarter? starter = null,
        Func<TimeSpan, Task>? delay = null,
        TimeSpan? steamExitTimeout = null,
        Func<string, string?>? promptText = null,
        Action<string>? notify = null,
        Func<string, MessageBoxResult, bool>? confirmChoice = null,
        Action<string, string>? beginBusy = null,
        Action<string>? setBusyMessage = null,
        Action? endBusy = null,
        Func<string?>? browseFolder = null,
        CriticalOpGuard? criticalOp = null,
        Action? requestProcessExit = null,
        Func<string, bool>? confirmHighRisk = null,
        bool preserveNullConfirmHighRisk = false,
        UpdateChecker? updateChecker = null,
        Func<ModPackageType?>? pickPackageType = null,
        Func<string?>? openDll = null,
        Func<string?>? openZip = null)
    {
        var processStarter = starter ?? new RecordingProcessStarter();
        var launcher = new GameLauncher(processStarter, () => false);
        var resolvedConfirmHighRisk = preserveNullConfirmHighRisk
            ? confirmHighRisk
            : (confirmHighRisk ?? (_ => true));
        return new MainViewModel(
            Paths,
            Store,
            _detector,
            Library,
            Profiles,
            _deploy,
            launcher,
            new RiskGate(),
            updateChecker: updateChecker,
            confirmHighRisk: resolvedConfirmHighRisk,
            confirm: confirm ?? (_ => false),
            notify: notify,
            browseFolder: browseFolder,
            openDll: openDll,
            openZip: openZip,
            promptText: promptText,
            pickPackageType: pickPackageType,
            branchSwitch: _branchSwitch,
            processProbe: Probe,
            processStarter: processStarter,
            delay: delay ?? (_ => Task.CompletedTask),
            steamExitTimeout: steamExitTimeout ?? TimeSpan.FromMilliseconds(50),
            steamExitCooldown: TimeSpan.Zero,
            steamRestartCooldown: TimeSpan.Zero,
            confirmChoice: confirmChoice,
            beginBusy: beginBusy,
            setBusyMessage: setBusyMessage,
            endBusy: endBusy,
            criticalOp: criticalOp ?? Guard,
            requestProcessExit: requestProcessExit);
    }

    public void WriteBranchConfig(BranchSwitchConfig cfg) =>
        _branchSwitch.SaveConfig(cfg);

    public BranchSwitchConfig LoadBranchConfig() => _branchSwitch.LoadConfig();

    public void Dispose()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(SteamLink) && Junctions.IsJunction(SteamLink))
                Junctions.DeleteJunction(SteamLink);
        }
        catch
        {
            // Best-effort unlink.
        }

        TryDelete(DataRoot);
        TryDelete(GameRoot);
        if (!string.IsNullOrWhiteSpace(SteamLink))
        {
            var root = Path.GetFullPath(Path.Combine(SteamLink, "..", "..", ".."));
            TryDelete(root);
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Temp leftover is non-fatal.
        }
    }

    static void CreateReadyGame(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "Il2CppAssemblies"));
        File.WriteAllText(Path.Combine(root, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "asm");
        File.WriteAllText(Path.Combine(root, "version.dll"), "");
    }

    static void CreateMissingAssembliesGame(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        File.WriteAllText(Path.Combine(root, "version.dll"), "");
    }

    static void CreateLoaderMissingAssembliesGame(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "net6"));
        File.WriteAllBytes(Path.Combine(root, "MelonLoader", "net6", "MelonLoader.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllBytes(Path.Combine(root, "version.dll"), new byte[] { 0x4D, 0x5A });
    }

    public static void SeedGameRoot(string dir, string marker)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "exe");
        File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "dll");
        File.WriteAllText(Path.Combine(dir, "marker.txt"), marker);
        Directory.CreateDirectory(Path.Combine(dir, "MelonLoader", "Il2CppAssemblies"));
        File.WriteAllText(Path.Combine(dir, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "asm");
        File.WriteAllText(Path.Combine(dir, "version.dll"), "");
    }

    sealed class RecordingProcessStarter : IProcessStarter
    {
        public void StartShell(string uriOrPath) { }
    }
}
