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
    public async Task Downloading_a_catalog_mod_drives_the_progress_bar_and_puts_it_away_afterwards()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"));
        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        var item = vm.CatalogMods.Should().ContainSingle().Subject;
        vm.SetCatalogSelection([item]);

        var percents = new List<double>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CatalogDownloadPercent))
                lock (percents) percents.Add(vm.CatalogDownloadPercent);
        };

        await vm.AddCatalogModToLibraryCommand.ExecuteAsync(null);

        vm.IsCatalogDownloading.Should().BeFalse("the bar must not linger once the download ends");

        // Progress<T> hands the callback off to the scheduler, so the tail can still be in flight.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            lock (percents)
            {
                if (percents.Contains(100))
                    break;
            }
            await Task.Delay(20);
        }

        lock (percents)
        {
            percents.Should().Contain(100,
                "the catalog declares a size, so the bar has to reach a real 100% rather than stay indeterminate");
        }
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
    public async Task Library_row_update_command_swaps_the_package_like_the_catalog_panel()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");
        fx.Profiles.SetEnabled("default", "cam-aaaaaaaa", true);
        fx.Profiles.SetEnabled("default", oldId, true);

        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"));
        await vm.RunStartupModUpdateCheckAsync();

        vm.OutdatedCount.Should().Be(1);
        var row = vm.Mods.Should().ContainSingle(m => m.Package.Id == oldId).Subject;
        row.HasUpdate.Should().BeTrue();

        await vm.UpdateModCommand.ExecuteAsync(row);

        var packages = fx.Library.List();
        packages.Should().NotContain(p => p.Id == oldId);
        var replacement = packages.Should().ContainSingle(p => p.CatalogId == "show-grid").Subject;
        File.ReadAllBytes(Path.Combine(replacement.PackageDirectory, "ShowGrid.dll"))
            .Should().Equal(NewBytes);
        replacement.Version.Should().Be("1.2.0");
        fx.Profiles.Get("default").EnabledPackageIds
            .Should().Equal("cam-aaaaaaaa", replacement.Id);
        // Library swap must repopulate Mods on the CollectionView thread — empty Mods + stuck
        // OutdatedCount is the swallowed NotSupportedException failure mode.
        vm.Mods.Should().ContainSingle(m => m.Package.Id == replacement.Id);
        vm.OutdatedCount.Should().Be(0);
    }

    [Fact]
    public async Task Library_row_update_is_blocked_while_the_game_is_running()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");
        fx.Probe.GameRunning = true;

        var notices = new List<string>();
        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"), notify: notices.Add);
        await vm.RunStartupModUpdateCheckAsync();

        var row = vm.Mods.Should().ContainSingle(m => m.Package.Id == oldId).Subject;
        await vm.UpdateModCommand.ExecuteAsync(row);

        fx.Library.List().Should().ContainSingle(p => p.Id == oldId);
        notices.Should().Contain(n => n.Contains("退出游戏", StringComparison.Ordinal));
        vm.OutdatedCount.Should().Be(1);
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

        // The check is only useful if the installed list lights up without opening the catalog panel.
        vm.CatalogMods.Should().ContainSingle(c => c.Id == "show-grid");
        var row = vm.Mods.Should().ContainSingle(m => m.Package.Id == oldId).Subject;
        row.HasUpdate.Should().BeTrue();
        row.LatestVersion.Should().Be("1.2.0");
        row.UpdateStatusText.Should().Be("1.0.0 → 1.2.0");
    }

    [Fact]
    public async Task Startup_check_stays_quiet_when_disabled_or_current()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SeedInstalledGridMod(fx, version: "1.0.0");

        var notices = new List<string>();
        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"), notify: notices.Add);

        vm.CheckModUpdatesOnStartup = false;
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
        vm.CheckModUpdatesOnStartup.Should().BeTrue("a player who is not told keeps running a version the author already fixed");
        vm.CheckModUpdatesOnStartup = false;

        fx.CreateVm().CheckModUpdatesOnStartup.Should().BeFalse();
    }

    /// <summary>
    /// The reported bug: a mod pushed to the repo only showed as updatable after a manual refresh in
    /// the catalog panel. Switching to the library has to be enough.
    /// </summary>
    [Fact]
    public async Task Switching_to_the_library_page_detects_the_update_without_opening_the_catalog()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");

        var vm = fx.CreateVm(catalog: CatalogServing(version: "1.2.0"));
        vm.CheckModUpdatesOnStartup = false;

        vm.ShowSettingsPageCommand.Execute(null);
        vm.ShowLibraryPageCommand.Execute(null);

        var row = await WaitForUpdateFlag(vm, oldId);
        row.HasUpdate.Should().BeTrue();
        row.UpdateStatusText.Should().Be("1.0.0 → 1.2.0");
        vm.OutdatedCount.Should().Be(1);
    }

    [Fact]
    public async Task A_second_switch_inside_the_throttle_window_does_not_refetch()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");

        var catalog = CatalogServing(version: "1.2.0", out var handler);
        var vm = fx.CreateVm(catalog: catalog);
        vm.CheckModUpdatesOnStartup = false;

        vm.ShowSettingsPageCommand.Execute(null);
        vm.ShowLibraryPageCommand.Execute(null);
        await WaitForUpdateFlag(vm, oldId);
        var afterFirst = handler.Requests.Count;

        vm.ShowSettingsPageCommand.Execute(null);
        vm.ShowLibraryPageCommand.Execute(null);
        await Task.Delay(50);

        handler.Requests.Count.Should().Be(afterFirst, "the catalog in hand is still fresh");

        vm.SilentCatalogRefreshInterval = TimeSpan.Zero;
        vm.ShowSettingsPageCommand.Execute(null);
        vm.ShowLibraryPageCommand.Execute(null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (handler.Requests.Count == afterFirst && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        handler.Requests.Count.Should().BeGreaterThan(afterFirst, "an expired window has to refetch");
    }

    /// <summary>
    /// A failed launch fetch used to leave the status column blank for the whole session, which reads
    /// as "nothing to update" rather than "we never got to look".
    /// </summary>
    [Fact]
    public async Task Startup_check_falls_back_to_the_cached_catalog_when_the_network_fails()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");
        CatalogCache.Write(fx.Paths.DataRoot, CatalogJson(version: "1.2.0"));

        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.ServiceUnavailable, "down"));
        var catalog = new ModCatalogService(new HttpClient(handler), dataRoot: fx.Paths.DataRoot);

        var vm = fx.CreateVm(catalog: catalog);
        vm.CheckModUpdatesOnStartup = true;

        await vm.RunStartupModUpdateCheckAsync();

        var row = vm.Mods.Should().ContainSingle(m => m.Package.Id == oldId).Subject;
        row.HasUpdate.Should().BeTrue();
        row.LatestVersion.Should().Be("1.2.0");
    }

    /// <summary>
    /// A mirror that stopped syncing answers 200 with an old catalog, and silence about that is the
    /// hardest version of this bug to diagnose.
    /// </summary>
    [Fact]
    public async Task A_stale_mirror_is_named_in_the_log_and_the_newer_copy_wins()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var oldId = SeedInstalledGridMod(fx, version: "1.0.0");

        var handler = new ScriptedHttpHandler(req =>
        {
            if (!req.RequestUri!.AbsolutePath.EndsWith("catalog.json", StringComparison.Ordinal))
                return ScriptedHttpHandler.Bytes(HttpStatusCode.OK, NewBytes);
            return req.RequestUri.Host.Contains("mirror.example", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("1.0.0", "2026-09-01"))
                : ScriptedHttpHandler.Json(HttpStatusCode.OK, CatalogJson("1.2.0", "2026-09-10"));
        });
        var catalog = new ModCatalogService(new HttpClient(handler));

        var vm = fx.CreateVm(catalog: catalog);
        // The VM owns the mirror setting, so the service's own value is overwritten at construction.
        vm.MirrorBaseUrl = "https://mirror.example/m";
        await vm.RefreshCatalogCommand.ExecuteAsync(null);

        var row = vm.Mods.Should().ContainSingle(m => m.Package.Id == oldId).Subject;
        row.HasUpdate.Should().BeTrue();
        row.LatestVersion.Should().Be("1.2.0");
        vm.LogText.Should().Contain(string.Format(
            LocalizationService.T("LogCatalogSourceStale"),
            RemoteFetch.MirrorSource,
            RemoteFetch.GithubSource));
    }

    static async Task<ModItemViewModel> WaitForUpdateFlag(MainViewModel vm, string packageId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var candidate = vm.Mods.FirstOrDefault(m => m.Package.Id == packageId);
            if (candidate is { HasUpdate: true })
                return candidate;
            await Task.Delay(20);
        }

        return vm.Mods.Should().ContainSingle(m => m.Package.Id == packageId).Subject;
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

    static string CatalogJson(string version, string updatedAt = "2026-09-06") =>
        $$"""
        {
          "updatedAt": "{{updatedAt}}",
          "mods": [
            {
              "id": "show-grid",
              "name": "Show Grid",
              "author": "巴巴",
              "version": "{{version}}",
              "updatedAt": "{{updatedAt}}",
              "summary": "格线",
              "file": "mods/show-grid/ShowGrid.dll",
              "sha256": "{{Hex(NewBytes)}}",
              "size": {{NewBytes.Length}},
              "type": "melon_mod"
            }
          ]
        }
        """;

    static ModCatalogService CatalogServing(string version) => CatalogServing(version, out _);

    static ModCatalogService CatalogServing(string version, out ScriptedHttpHandler handler)
    {
        var catalogJson = CatalogJson(version);
        handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.AbsolutePath.EndsWith("catalog.json", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, catalogJson)
                : ScriptedHttpHandler.Bytes(HttpStatusCode.OK, NewBytes));

        return new ModCatalogService(new HttpClient(handler));
    }

    static string Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
