using System.Globalization;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class UiLanguageSwitchTests
{
    [Fact]
    public void Switching_from_ru_to_system_applies_os_language_not_thread_culture()
    {
        LocalizationService.SystemUiCultureProvider = () => CultureInfo.GetCultureInfo("zh-CN");
        try
        {
            using var fx = Fixture.CreateReady(uiLanguage: "ru");
            var vm = fx.CreateVm();

            vm.SelectedUiLanguageCode.Should().Be("ru");
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be("ru");

            vm.SelectedUiLanguageCode = "system";

            vm.SelectedUiLanguageCode.Should().Be("system");
            fx.LoadConfig().UiLanguage.Should().Be("system");
            CultureInfo.CurrentUICulture.Name.Should().Be("zh-CN");
            LocalizationService.T("LanguageSystem").Should().Be("跟随系统");
        }
        finally
        {
            LocalizationService.SystemUiCultureProvider = null;
            LocalizationService.Apply("zh-CN");
        }
    }

    [Fact]
    public void Switching_from_en_to_system_persists_system_code()
    {
        using var fx = Fixture.CreateReady(uiLanguage: "en");
        var vm = fx.CreateVm();

        vm.SelectedUiLanguageCode.Should().Be("en");
        vm.SelectedUiLanguageCode = "system";

        vm.SelectedUiLanguageCode.Should().Be("system");
        fx.LoadConfig().UiLanguage.Should().Be("system");
    }

    [Fact]
    public void Switching_zhCN_to_system_when_system_is_zhCN_keeps_system_and_logs()
    {
        using var fx = Fixture.CreateReady(uiLanguage: "zh-CN");
        var vm = fx.CreateVm();

        vm.SelectedUiLanguageCode.Should().Be("zh-CN");
        vm.SelectedUiLanguageCode = "system";

        vm.SelectedUiLanguageCode.Should().Be("system");
        fx.LoadConfig().UiLanguage.Should().Be("system");
        // When OS UI is already zh-CN, resolved culture unchanged — expect feedback in log.
        if (LocalizationService.ResolveSystemLanguage() == "zh-CN")
            vm.LogText.Should().Contain("跟随系统");
    }

    sealed class Fixture : IDisposable
    {
        public string DataRoot { get; }
        readonly PathsService _paths;
        readonly JsonStore _store;
        readonly ProfileService _profiles;
        readonly ModLibraryService _library;
        readonly GameDetector _detector;
        readonly DeployService _deploy;
        readonly string _gameRoot;

        Fixture(string dataRoot, string gameRoot)
        {
            DataRoot = dataRoot;
            _gameRoot = gameRoot;
            _paths = new PathsService(dataRoot);
            _paths.EnsureCreated();
            _store = new JsonStore();
            _profiles = new ProfileService(_paths, _store);
            _profiles.EnsureDefaults();
            _library = new ModLibraryService(_paths, new AssemblyInspector(), _store, _profiles);
            _detector = new GameDetector();
            _deploy = new DeployService(_paths, _store, new DeployPlanner(), _detector, new ProcessProbe());
        }

        public static Fixture CreateReady(string uiLanguage)
        {
            var dataRoot = Path.Combine(Path.GetTempPath(), "mmm-lang-" + Guid.NewGuid().ToString("N"));
            var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-lang-game-" + Guid.NewGuid().ToString("N"));
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
                LaunchMode = LaunchMode.ExeOnly,
                UiLanguage = uiLanguage
            });
            return fx;
        }

        public AppConfig LoadConfig() => _store.LoadOrDefault(_paths.ConfigPath, () => new AppConfig());

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
            try { if (Directory.Exists(_gameRoot)) Directory.Delete(_gameRoot, true); } catch { /* ignore */ }
        }

        sealed class NoopStarter : IProcessStarter
        {
            public void StartShell(string uriOrPath) { }
        }
    }
}
