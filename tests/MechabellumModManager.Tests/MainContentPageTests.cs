using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class MainContentPageTests
{
    [Fact]
    public void Defaults_to_Library_page()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.ActiveContentPage.Should().Be(MainContentPage.Library);
        vm.IsLibraryPage.Should().BeTrue();
        vm.IsCatalogPage.Should().BeFalse();
        vm.IsSettingsPage.Should().BeFalse();
    }

    [Fact]
    public void ShowCatalogPage_is_exclusive()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.ShowCatalogPageCommand.Execute(null);

        vm.ActiveContentPage.Should().Be(MainContentPage.Catalog);
        vm.IsCatalogPage.Should().BeTrue();
        vm.IsLibraryPage.Should().BeFalse();
        vm.IsSettingsPage.Should().BeFalse();
    }

    [Fact]
    public void ShowSettingsPage_then_Library_is_exclusive()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        vm.ShowSettingsPageCommand.Execute(null);
        vm.IsSettingsPage.Should().BeTrue();

        vm.ShowLibraryPageCommand.Execute(null);
        vm.ActiveContentPage.Should().Be(MainContentPage.Library);
        vm.IsLibraryPage.Should().BeTrue();
        vm.IsSettingsPage.Should().BeFalse();
        vm.IsCatalogPage.Should().BeFalse();
    }

    sealed class Fixture : IDisposable
    {
        public string DataRoot { get; }
        public string GameRoot { get; }
        readonly PathsService _paths;
        readonly JsonStore _store;
        readonly ProfileService _profiles;
        readonly ModLibraryService _library;
        readonly GameDetector _detector;
        readonly DeployService _deploy;

        Fixture(string dataRoot, string gameRoot)
        {
            DataRoot = dataRoot;
            GameRoot = gameRoot;
            _paths = new PathsService(dataRoot);
            _paths.EnsureCreated();
            _store = new JsonStore();
            _profiles = new ProfileService(_paths, _store);
            _profiles.EnsureDefaults();
            _library = new ModLibraryService(_paths, new AssemblyInspector(), _store, _profiles);
            _detector = new GameDetector();
            _deploy = new DeployService(_paths, _store, new DeployPlanner(), _detector, new ProcessProbe());
        }

        public static Fixture CreateReady()
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-page-" + Guid.NewGuid().ToString("N"));
            var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-page-game-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(gameRoot);
            File.WriteAllText(Path.Combine(gameRoot, "Mechabellum.exe"), "");
            File.WriteAllText(Path.Combine(gameRoot, "GameAssembly.dll"), "");
            Directory.CreateDirectory(Path.Combine(gameRoot, "MelonLoader", "Il2CppAssemblies"));
            File.WriteAllText(Path.Combine(gameRoot, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "asm");
            File.WriteAllText(Path.Combine(gameRoot, "version.dll"), "");

            var fx = new Fixture(dataRoot, gameRoot);
            fx._store.Save(fx._paths.ConfigPath, new AppConfig
            {
                GamePath = gameRoot,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            return fx;
        }

        public MainViewModel CreateVm() =>
            new(
                _paths,
                _store,
                _detector,
                _library,
                _profiles,
                _deploy,
                new GameLauncher(new NoopStarter(), () => false),
                new RiskGate(),
                confirmHighRisk: _ => true);

        public void Dispose()
        {
            try { if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, true); } catch { /* ignore */ }
            try { if (Directory.Exists(GameRoot)) Directory.Delete(GameRoot, true); } catch { /* ignore */ }
        }

        sealed class NoopStarter : IProcessStarter
        {
            public void StartShell(string uriOrPath) { }
        }
    }
}
