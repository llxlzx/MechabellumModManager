using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;
using MechabellumModManager.ViewModels;

/// <summary>
/// End-to-end cover for updating an already-installed catalog mod: the gates that let it through,
/// and the transaction that swaps it without losing the player's profile wiring.
/// </summary>
public class ModUpdateFlowTests
{
    static readonly byte[] OldBytes = [0x4D, 0x5A, 0x90, 0x00, 0x01];
    static readonly byte[] NewBytes = [0x4D, 0x5A, 0x90, 0x00, 0x02];

    [Fact]
    public async Task Updating_an_installed_mod_swaps_the_package_and_repoints_the_profile()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");
        fx.Profiles.SetEnabled("default", "cam-aaaaaaaa", true);
        fx.Profiles.SetEnabled("default", oldId, true);

        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"));

        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        var item = vm.CatalogMods.Should().ContainSingle().Subject;
        item.HasUpdate.Should().BeTrue();
        item.IsInLibrary.Should().BeTrue();

        vm.SetCatalogSelection([item]);
        vm.AddCatalogModToLibraryCommand.CanExecute(null).Should().BeTrue();
        await vm.AddCatalogModToLibraryCommand.ExecuteAsync(null);

        var packages = fx.Library.List();
        packages.Should().NotContain(p => p.Id == oldId);
        var replacement = packages.Should().ContainSingle(p => p.CatalogId == "show-grid").Subject;
        File.ReadAllBytes(Path.Combine(replacement.PackageDirectory, "ShowGrid.dll"))
            .Should().Equal(NewBytes);
        replacement.Version.Should().Be("1.2.0");

        fx.Profiles.Get("default").EnabledPackageIds
            .Should().Equal("cam-aaaaaaaa", replacement.Id);
        vm.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task An_up_to_date_mod_is_not_offered_for_update()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SeedInstalledGridMod(fx, version: "1.2.0", bytes: NewBytes);

        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"));
        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        var item = vm.CatalogMods.Should().ContainSingle().Subject;
        item.IsInLibrary.Should().BeTrue();
        item.HasUpdate.Should().BeFalse();

        vm.SetCatalogSelection([item]);
        vm.AddCatalogModToLibraryCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task A_running_game_blocks_the_update_and_leaves_the_old_package_in_place()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");
        fx.Probe.GameRunning = true;

        var notices = new List<string>();
        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"), notify: notices.Add);
        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        var item = vm.CatalogMods.Should().ContainSingle().Subject;
        vm.SetCatalogSelection([item]);
        await vm.AddCatalogModToLibraryCommand.ExecuteAsync(null);

        fx.Library.List().Should().ContainSingle(p => p.Id == oldId);
        notices.Should().ContainSingle();
    }

    [Fact]
    public async Task Startup_check_reports_outdated_mods_without_installing_anything()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");

        var notices = new List<string>();
        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"), notify: notices.Add)
            ;
        vm.CheckModUpdatesOnStartup = true;

        await vm.RunStartupModUpdateCheckAsync();

        notices.Should().ContainSingle().Which.Should().Contain("Show Grid");
        fx.Library.List().Should().ContainSingle(p => p.Id == oldId);
    }

    [Fact]
    public async Task Startup_check_stays_quiet_when_disabled_or_current()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SeedInstalledGridMod(fx, version: "1.0.0");

        var notices = new List<string>();
        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"), notify: notices.Add);

        await vm.RunStartupModUpdateCheckAsync();
        notices.Should().BeEmpty();

        vm.CheckModUpdatesOnStartup = true;
        SeedInstalledGridMod(fx, version: "1.2.0", bytes: NewBytes);
        await vm.RunStartupModUpdateCheckAsync();
        notices.Should().BeEmpty();
    }

    [Fact]
    public void Startup_check_preference_survives_a_restart()
    {
        using var fx = MainViewModelFixture.CreateReady();

        var vm = fx.CreateVm();
        vm.CheckModUpdatesOnStartup.Should().BeFalse();
        vm.CheckModUpdatesOnStartup = true;

        fx.CreateVm().CheckModUpdatesOnStartup.Should().BeTrue();
    }

    /// <summary>Installs a package that looks like it came from the "show-grid" catalog entry.</summary>
    static string SeedInstalledGridMod(MainViewModelFixture fx, string version, byte[]? bytes = null)
    {
        var payload = bytes ?? OldBytes;
        var id = "showgrid-old00001";
        var dir = Path.Combine(fx.Paths.LibraryRoot, "mods", id);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "ShowGrid.dll"), payload);
        File.WriteAllText(
            Path.Combine(dir, "package.json"),
            $$"""
            {
              "id": "{{id}}",
              "catalogId": "show-grid",
              "displayName": "Show Grid",
              "type": "melon_mod",
              "version": "{{version}}",
              "files": [ { "relativePathInPackage": "ShowGrid.dll", "sha256": "{{Hex(payload)}}" } ]
            }
            """);
        fx.Store.Save(
            Path.Combine(fx.Paths.LibraryRoot, "index.json"),
            new { packageIds = new[] { "cam-aaaaaaaa", id } });
        return id;
    }

    static ModCatalogService CatalogServing(string version)
    {
        var catalogJson = $$"""
            {
              "updatedAt": "2026-09-06",
              "mods": [
                {
                  "id": "show-grid",
                  "name": "Show Grid",
                  "author": "巴巴",
                  "version": "{{version}}",
                  "updatedAt": "2026-09-06",
                  "summary": "格线",
                  "file": "mods/show-grid/ShowGrid.dll",
                  "sha256": "{{Hex(NewBytes)}}",
                  "type": "melon_mod"
                }
              ]
            }
            """;

        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.AbsolutePath.EndsWith("catalog.json", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, catalogJson)
                : ScriptedHttpHandler.Bytes(HttpStatusCode.OK, NewBytes));

        return new ModCatalogService(new HttpClient(handler));
    }

    static string Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
