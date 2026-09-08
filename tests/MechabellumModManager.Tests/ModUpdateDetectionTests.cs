using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

public class ModUpdateDetectionTests
{
    [Fact]
    public void Catalog_enrichment_keeps_showing_the_version_the_player_actually_installed()
    {
        using var harness = new EnrichmentHarness();

        var pkg = new ModPackage
        {
            Id = "grid-aaaaaaaa",
            CatalogId = "show-grid",
            DisplayName = "Show Grid",
            Type = ModPackageType.MelonMod,
            Version = "1.0.0",
            CatalogUpdatedAt = "2026-01-01"
        };
        var item = new ModItemViewModel(harness.Owner, pkg, isEnabled: false);

        item.ApplyCatalogEnrichment(new CatalogMod
        {
            Id = "show-grid",
            Name = "Show Grid",
            Author = "巴巴",
            Summary = "新简介",
            Version = "1.2.0",
            UpdatedAt = "2026-09-06"
        });

        pkg.Version.Should().Be("1.0.0");
        pkg.CatalogUpdatedAt.Should().Be("2026-01-01");
        item.Version.Should().Be("1.0.0");
        pkg.Author.Should().Be("巴巴");
        pkg.Summary.Should().Be("新简介");
    }

    [Fact]
    public void Catalog_enrichment_fills_version_fields_that_the_package_never_had()
    {
        using var harness = new EnrichmentHarness();

        var pkg = new ModPackage
        {
            Id = "grid-bbbbbbbb",
            CatalogId = "show-grid",
            DisplayName = "Show Grid",
            Type = ModPackageType.MelonMod
        };
        var item = new ModItemViewModel(harness.Owner, pkg, isEnabled: false);

        item.ApplyCatalogEnrichment(new CatalogMod
        {
            Id = "show-grid",
            Name = "Show Grid",
            Version = "1.2.0",
            UpdatedAt = "2026-09-06"
        });

        pkg.Version.Should().Be("1.2.0");
        pkg.CatalogUpdatedAt.Should().Be("2026-09-06");
    }

    [Fact]
    public void Entry_the_player_never_installed_is_NotInstalled()
    {
        var packages = new[] { Installed("other", version: "1.0.0") };

        ModCatalogService.GetEntryState(packages, Catalog("show-grid", version: "1.0.0"))
            .Should().Be(CatalogEntryState.NotInstalled);
    }

    [Fact]
    public void Matching_file_hash_means_UpToDate_even_when_the_catalog_version_looks_newer()
    {
        var packages = new[] { Installed("show-grid", version: "1.0.0", fileHash: "aa11") };

        ModCatalogService.GetEntryState(
                packages,
                Catalog("show-grid", version: "9.9.9", sha256: "aa11"))
            .Should().Be(CatalogEntryState.UpToDate);
    }

    [Fact]
    public void Different_file_hash_means_UpdateAvailable_even_when_the_version_did_not_change()
    {
        var packages = new[] { Installed("show-grid", version: "1.0.0", fileHash: "aa11") };

        ModCatalogService.GetEntryState(
                packages,
                Catalog("show-grid", version: "1.0.0", sha256: "bb22"))
            .Should().Be(CatalogEntryState.UpdateAvailable);
    }

    [Fact]
    public void Catalog_hash_is_inconclusive_when_the_package_recorded_none()
    {
        var packages = new[] { Installed("show-grid", version: "1.0.0", fileHash: "") };

        ModCatalogService.GetEntryState(
                packages,
                Catalog("show-grid", version: "1.0.0", sha256: "bb22"))
            .Should().Be(CatalogEntryState.UpToDate);
    }

    [Fact]
    public void Newer_catalog_version_means_UpdateAvailable()
    {
        var packages = new[] { Installed("show-grid", version: "1.0.0") };

        ModCatalogService.GetEntryState(packages, Catalog("show-grid", version: "1.2.0"))
            .Should().Be(CatalogEntryState.UpdateAvailable);
    }

    [Fact]
    public void Same_version_means_UpToDate()
    {
        var packages = new[] { Installed("show-grid", version: "1.2.0") };

        ModCatalogService.GetEntryState(packages, Catalog("show-grid", version: "1.2.0"))
            .Should().Be(CatalogEntryState.UpToDate);
    }

    [Fact]
    public void A_locally_newer_version_does_not_prompt_a_downgrade()
    {
        var packages = new[] { Installed("show-grid", version: "2.0.0") };

        ModCatalogService.GetEntryState(packages, Catalog("show-grid", version: "1.2.0"))
            .Should().Be(CatalogEntryState.UpToDate);
    }

    [Fact]
    public void Newer_catalog_date_means_UpdateAvailable_when_versions_are_equal()
    {
        var packages = new[]
        {
            Installed("show-grid", version: "1.0.0", catalogUpdatedAt: "2026-01-01")
        };

        ModCatalogService.GetEntryState(
                packages,
                Catalog("show-grid", version: "1.0.0", updatedAt: "2026-09-06"))
            .Should().Be(CatalogEntryState.UpdateAvailable);
    }

    [Fact]
    public void An_outdated_install_still_counts_as_in_library()
    {
        var packages = new[] { Installed("show-grid", version: "1.0.0", fileHash: "aa11") };
        var catalog = Catalog("show-grid", version: "1.2.0", sha256: "bb22");

        ModCatalogService.GetEntryState(packages, catalog).Should().Be(CatalogEntryState.UpdateAvailable);
        ModCatalogService.IsInLibrary(packages, catalog).Should().BeTrue();
    }

    [Fact]
    public void Catalog_item_reports_a_distinct_status_for_each_state()
    {
        var mod = Catalog("show-grid", version: "1.2.0");

        var fresh = new CatalogModItemViewModel(mod, CatalogEntryState.NotInstalled);
        var current = new CatalogModItemViewModel(mod, CatalogEntryState.UpToDate);
        var stale = new CatalogModItemViewModel(mod, CatalogEntryState.UpdateAvailable);

        fresh.IsInLibrary.Should().BeFalse();
        fresh.HasUpdate.Should().BeFalse();

        current.IsInLibrary.Should().BeTrue();
        current.HasUpdate.Should().BeFalse();

        stale.IsInLibrary.Should().BeTrue();
        stale.HasUpdate.Should().BeTrue();

        new[] { fresh.StatusText, current.StatusText, stale.StatusText }
            .Should().OnlyHaveUniqueItems().And.NotContainNulls();
    }

    [Fact]
    public void Library_item_flags_itself_when_the_catalog_moved_on()
    {
        using var harness = new EnrichmentHarness();

        var pkg = new ModPackage
        {
            Id = "grid-aaaaaaaa",
            CatalogId = "show-grid",
            DisplayName = "Show Grid",
            Type = ModPackageType.MelonMod,
            Version = "1.0.0"
        };
        var item = new ModItemViewModel(harness.Owner, pkg, isEnabled: false);

        item.ApplyCatalogEnrichment(Catalog("show-grid", version: "1.2.0"));
        item.HasUpdate.Should().BeTrue();
        item.UpdateStatusText.Should().NotBeEmpty();

        item.ApplyCatalogEnrichment(Catalog("show-grid", version: "1.0.0"));
        item.HasUpdate.Should().BeFalse();
        item.UpdateStatusText.Should().BeEmpty();
    }

    static ModPackage Installed(
        string catalogId,
        string? version = null,
        string? fileHash = null,
        string? catalogUpdatedAt = null) =>
        new()
        {
            Id = catalogId + "-aaaaaaaa",
            CatalogId = catalogId,
            DisplayName = catalogId,
            Version = version,
            CatalogUpdatedAt = catalogUpdatedAt,
            Files =
            {
                new DeployableFile { RelativePathInPackage = "Mod.dll", Sha256 = fileHash ?? "" }
            }
        };

    static CatalogMod Catalog(
        string id,
        string? version = null,
        string? sha256 = null,
        string? updatedAt = null) =>
        new()
        {
            Id = id,
            Name = id,
            File = $"mods/{id}/Mod.dll",
            Version = version,
            Sha256 = sha256,
            UpdatedAt = updatedAt
        };

    /// <summary>Minimal wiring for the one collaborator ModItemViewModel needs: a MainViewModel owner.</summary>
    sealed class EnrichmentHarness : IDisposable
    {
        readonly string _data;
        readonly string _game;

        public MainViewModel Owner { get; }

        public EnrichmentHarness()
        {
            _data = Path.Combine(Path.GetTempPath(), "mmm-upd-" + Guid.NewGuid().ToString("N"));
            _game = Path.Combine(Path.GetTempPath(), "mmm-upd-game-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_game);
            File.WriteAllText(Path.Combine(_game, "Mechabellum.exe"), "");
            File.WriteAllText(Path.Combine(_game, "GameAssembly.dll"), "");
            Directory.CreateDirectory(Path.Combine(_game, "MelonLoader"));
            File.WriteAllText(Path.Combine(_game, "version.dll"), "");

            var paths = new PathsService(_data);
            paths.EnsureCreated();
            var store = new JsonStore();
            store.Save(paths.ConfigPath, new AppConfig
            {
                GamePath = _game,
                ActiveProfileId = "default",
                LaunchMode = LaunchMode.ExeOnly
            });
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var library = new ModLibraryService(paths, new AssemblyInspector(), store, profiles);
            var detector = new GameDetector();
            var deploy = new DeployService(paths, store, new DeployPlanner(), detector, new ProcessProbe());
            var launcher = new GameLauncher(new NoopStarter(), () => false);
            Owner = new MainViewModel(
                paths, store, detector, library, profiles, deploy, launcher, new RiskGate(),
                confirmHighRisk: _ => true);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_data)) Directory.Delete(_data, true); } catch { /* cleanup */ }
            try { if (Directory.Exists(_game)) Directory.Delete(_game, true); } catch { /* cleanup */ }
        }

        sealed class NoopStarter : IProcessStarter
        {
            public void StartShell(string uriOrPath) { }
        }
    }
}
