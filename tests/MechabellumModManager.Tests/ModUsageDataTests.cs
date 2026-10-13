using System.Text.Json;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class ModUsageDataTests
{
    [Fact]
    public void WriteAndLoad_preserves_usageOverride()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-usage-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var store = new JsonStore();
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var lib = new ModLibraryService(paths, new AssemblyInspector(), store, profiles);

            var pkgId = "usage-test-aaaaaaaa";
            var pkgDir = Path.Combine(paths.LibraryRoot, "mods", pkgId);
            Directory.CreateDirectory(pkgDir);
            File.WriteAllBytes(Path.Combine(pkgDir, "Usage.dll"), new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            File.WriteAllText(
                Path.Combine(pkgDir, "package.json"),
                """
                {
                  "id": "usage-test-aaaaaaaa",
                  "displayName": "Usage Test",
                  "type": "melon_mod",
                  "usageOverride": "Tournament",
                  "files": [ { "relativePathInPackage": "Usage.dll", "sha256": "aabb" } ]
                }
                """);
            store.Save(Path.Combine(paths.LibraryRoot, "index.json"), new { packageIds = new[] { pkgId } });

            var loaded = lib.List().Should().ContainSingle().Subject;
            loaded.UsageOverride.Should().Be("Tournament");

            loaded.UsageOverride = "Casual";
            lib.SavePackageMeta(loaded);

            lib.List().Should().ContainSingle().Subject.UsageOverride.Should().Be("Casual");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(pkgDir, "package.json")));
            doc.RootElement.GetProperty("usageOverride").GetString().Should().Be("Casual");
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Catalog_json_usage_is_read_and_invalid_value_shows_unrated()
    {
        var root = JsonSerializer.Deserialize<CatalogRoot>(
            """
            {
              "mods": [
                { "id": "a", "name": "A", "file": "mods/a/A.dll", "usage": "Casual" },
                { "id": "b", "name": "B", "file": "mods/b/B.dll", "usage": "0" },
                { "id": "c", "name": "C", "file": "mods/c/C.dll" }
              ]
            }
            """)!;

        root.Mods.Select(m => m.Usage).Should().Equal("Casual", "0", null);
        root.Mods
            .Select(m => new CatalogModItemViewModel(m, CatalogEntryState.NotInstalled).EffectiveUsage)
            .Should().Equal(ModUsage.Casual, ModUsage.Unrated, ModUsage.Unrated);
    }

    [Fact]
    public void ApplyCatalogEnrichment_keeps_local_usage_override()
    {
        using var fx = Fixture.CreateReady();
        var vm = fx.CreateVm();

        var overridden = new ModItemViewModel(vm, new ModPackage
        {
            Id = "x",
            DisplayName = "X",
            Type = ModPackageType.MelonMod,
            UsageOverride = "Tournament"
        }, isEnabled: false);
        overridden.ApplyCatalogEnrichment(new CatalogMod { Id = "x", Name = "X", Usage = "Casual" });

        overridden.Package.UsageOverride.Should().Be("Tournament");
        overridden.Package.CatalogUsage.Should().Be("Casual");
        overridden.EffectiveUsage.Should().Be(ModUsage.Tournament);

        var following = new ModItemViewModel(vm, new ModPackage
        {
            Id = "y",
            DisplayName = "Y",
            Type = ModPackageType.MelonMod
        }, isEnabled: false);
        following.EffectiveUsage.Should().Be(ModUsage.Unrated);
        following.ApplyCatalogEnrichment(new CatalogMod { Id = "y", Name = "Y", Usage = "Casual" });
        following.EffectiveUsage.Should().Be(ModUsage.Casual);
    }

    [Theory]
    [InlineData(ModUsage.Tournament, "UsageTournament")]
    [InlineData(ModUsage.Casual, "UsageCasual")]
    [InlineData(ModUsage.Unrated, "UsageUnrated")]
    public void UiStrings_UsageLabel_is_localized(ModUsage usage, string key)
    {
        var ui = new UiStrings();
        ui.UsageLabel(usage).Should().Be(LocalizationService.T(key));
        ui.UsageLabel(usage).Should().NotBe(key);
    }

    [Theory]
    [InlineData("FilterUsage")]
    [InlineData("UsageFollowCatalog")]
    [InlineData("LogInvalidUsageOverride")]
    [InlineData("LogInvalidCatalogUsage")]
    public void New_resource_keys_resolve(string key)
    {
        LocalizationService.T(key).Should().NotBe(key).And.NotBeNullOrWhiteSpace();
    }
}
