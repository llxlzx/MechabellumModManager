using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

/// <summary>
/// The fixture library holds one package, "cam-aaaaaaaa" (Cam), whose recorded file hash is the
/// placeholder "aabbccdd", so these tests match it by id.
/// </summary>
public sealed class MainViewModelCatalogRemovalTests : IDisposable
{
    readonly TestRemovalSigner _signer = new();

    public void Dispose() => _signer.Dispose();

    static CatalogRemoval CamRemoval(string? reason = "影响对局公平") =>
        new() { Id = "cam-aaaaaaaa", Reason = reason };

    CatalogRoot Signed(params CatalogRemoval[] removed) => new() { RemovedSigned = _signer.Sign(removed) };

    static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static ModCatalogService ServingCatalog(string json)
    {
        var handler = new ScriptedHttpHandler(req =>
            req.RequestUri!.AbsolutePath.EndsWith("catalog.json", StringComparison.Ordinal)
                ? ScriptedHttpHandler.Json(HttpStatusCode.OK, json)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        return new ModCatalogService(new HttpClient(handler));
    }

    [Fact]
    public void A_signed_takedown_removes_the_library_row_and_tells_the_player()
    {
        using var fx = MainViewModelFixture.CreateReady();
        fx.Profiles.SetEnabled("default", "cam-aaaaaaaa", true);
        var notices = new List<string>();
        var vm = fx.CreateVm(notify: notices.Add, removalPublicKey: _signer.PublicKey);
        vm.Mods.Should().ContainSingle();

        vm.ApplyCatalogRoot(Signed(CamRemoval()));

        vm.Mods.Should().BeEmpty();
        fx.Library.List().Should().BeEmpty();
        fx.Profiles.Get("default").EnabledPackageIds.Should().BeEmpty();
        vm.LogText.Should().Contain("Cam").And.Contain("影响对局公平");
        notices.Should().ContainSingle().Which.Should().Contain("Cam");
    }

    [Fact]
    public void A_list_signed_by_someone_else_removes_nothing()
    {
        using var fx = MainViewModelFixture.CreateReady();
        using var attacker = new TestRemovalSigner();
        var vm = fx.CreateVm(removalPublicKey: _signer.PublicKey);

        vm.ApplyCatalogRoot(new CatalogRoot { RemovedSigned = attacker.Sign([CamRemoval()]) });

        fx.Library.List().Should().ContainSingle();
        vm.LogText.Should().Contain(RemovalListVerifier.ErrorSignature);
    }

    [Fact]
    public void Without_a_shipped_key_nothing_is_removed()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var vm = fx.CreateVm(removalPublicKey: "");

        vm.ApplyCatalogRoot(Signed(CamRemoval()));

        fx.Library.List().Should().ContainSingle();
    }

    [Fact]
    public void An_older_list_does_not_replace_a_newer_one()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var vm = fx.CreateVm(removalPublicKey: _signer.PublicKey);
        var now = DateTimeOffset.UtcNow;
        fx.Probe.GameRunning = true;
        vm.ApplyCatalogRoot(new CatalogRoot { RemovedSigned = _signer.Sign([CamRemoval()], now) });

        vm.ApplyCatalogRoot(new CatalogRoot { RemovedSigned = _signer.Sign([], now.AddDays(-1)) });
        fx.Probe.GameRunning = false;
        vm.ApplyCatalogRoot(new CatalogRoot());

        fx.Library.List().Should().BeEmpty("the newer list, which still takes Cam down, stays in force");
    }

    [Fact]
    public void A_takedown_waits_while_the_game_runs_and_logs_once()
    {
        using var fx = MainViewModelFixture.CreateReady();
        fx.Probe.GameRunning = true;
        var notices = new List<string>();
        var vm = fx.CreateVm(notify: notices.Add, removalPublicKey: _signer.PublicKey);

        vm.ApplyCatalogRoot(Signed(CamRemoval()));
        vm.ApplyCatalogRoot(Signed(CamRemoval()));

        fx.Library.List().Should().ContainSingle();
        notices.Should().BeEmpty();
        vm.LogText.Split('\n').Count(l => l.Contains("Cam")).Should().Be(1);
    }

    [Fact]
    public void Nothing_is_removed_while_the_recovery_gate_owns_the_folders()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var vm = fx.CreateVm(removalPublicKey: _signer.PublicKey);
        vm.IsRecoveryGateActive = true;

        vm.ApplyCatalogRoot(Signed(CamRemoval()));

        fx.Library.List().Should().ContainSingle();

        vm.IsRecoveryGateActive = false;
        vm.ApplyCatalogRoot(new CatalogRoot());

        fx.Library.List().Should().BeEmpty("the accepted list is retried once the gate clears");
    }

    [Fact]
    public void Apply_after_a_deferred_takedown_removes_instead_of_deploying()
    {
        using var fx = MainViewModelFixture.CreateReady();
        fx.Profiles.SetEnabled("default", "cam-aaaaaaaa", true);
        fx.Probe.GameRunning = true;
        var vm = fx.CreateVm(removalPublicKey: _signer.PublicKey);
        vm.ApplyCatalogRoot(Signed(CamRemoval()));
        fx.Library.List().Should().ContainSingle();

        fx.Probe.GameRunning = false;
        vm.ApplyProfile();

        fx.Library.List().Should().BeEmpty();
        File.Exists(Path.Combine(fx.GameRoot, "Mods", "Cam.dll")).Should().BeFalse();
    }

    [Fact]
    public void An_id_both_listed_and_removed_is_hidden_from_the_catalog()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var vm = fx.CreateVm(removalPublicKey: _signer.PublicKey);

        vm.ApplyCatalogRoot(new CatalogRoot
        {
            Mods =
            [
                new CatalogMod { Id = "opponent-economy", Name = "Econ", File = "mods/e/Econ.dll" },
                new CatalogMod { Id = "show-grid", Name = "Grid", File = "mods/g/ShowGrid.dll" }
            ],
            RemovedSigned = _signer.Sign([new CatalogRemoval { Id = "opponent-economy" }])
        });

        vm.CatalogMods.Select(m => m.Id).Should().Equal("show-grid");
        vm.LogText.Should().Contain("opponent-economy");
    }

    [Fact]
    public async Task Takedown_runs_on_startup_even_when_update_checks_are_off()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var blob = _signer.Sign([CamRemoval("下架")]);
        var notices = new List<string>();
        var vm = fx.CreateVm(
            catalog: ServingCatalog($$"""
                { "mods": [], "removedSigned": { "payload": "{{blob.Payload}}", "signature": "{{blob.Signature}}" } }
                """),
            notify: notices.Add,
            removalPublicKey: _signer.PublicKey);
        vm.CheckModUpdatesOnStartup = false;

        await vm.RunStartupModUpdateCheckAsync();

        fx.Library.List().Should().BeEmpty();
        notices.Should().ContainSingle().Which.Should().Contain("Cam");
    }

    [Fact]
    public void Importing_a_taken_down_dll_removes_it_before_enabling()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var stub = Path.Combine(fx.DataRoot, "Banned.dll");
        var bytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x07 };
        File.WriteAllBytes(stub, bytes);
        var confirms = 0;
        var vm = fx.CreateVm(
            confirmHighRisk: _ =>
            {
                confirms++;
                return true;
            },
            pickPackageType: () => ModPackageType.MelonMod,
            openDll: () => stub,
            removalPublicKey: _signer.PublicKey);
        vm.ApplyCatalogRoot(Signed(new CatalogRemoval { Id = "banned", Sha256 = [Sha(bytes)] }));

        vm.ImportDllCommand.Execute(null);

        fx.Library.List().Should().ContainSingle(p => p.Id == "cam-aaaaaaaa");
        fx.Profiles.Get("default").EnabledPackageIds.Should().BeEmpty();
        confirms.Should().Be(0);
        vm.LogText.Should().Contain("Banned");
    }

    /// <summary>
    /// After an upgrade the catalog cache is gone. The accepted list still loads from its own file,
    /// but a hash-only entry waits until a catalog says which hashes listed mods still ship.
    /// </summary>
    [Fact]
    public void The_accepted_list_survives_a_restart_and_hash_matches_wait_for_the_listed_mods()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var stub = Path.Combine(fx.DataRoot, "Banned.dll");
        var bytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x08 };
        File.WriteAllBytes(stub, bytes);
        fx.CreateVm(removalPublicKey: _signer.PublicKey)
            .ApplyCatalogRoot(Signed(new CatalogRemoval { Id = "banned", Sha256 = [Sha(bytes)] }));
        File.Delete(CatalogCache.GetPath(fx.Paths.DataRoot));

        var restarted = fx.CreateVm(
            confirmHighRisk: _ => true,
            pickPackageType: () => ModPackageType.MelonMod,
            openDll: () => stub,
            removalPublicKey: _signer.PublicKey);
        restarted.ImportDllCommand.Execute(null);
        fx.Library.List().Should().HaveCount(2, "listed mods are unknown, so the hash is not enforced yet");

        restarted.ApplyCatalogRoot(new CatalogRoot());

        fx.Library.List().Should().ContainSingle(p => p.Id == "cam-aaaaaaaa",
            "the list came from removal-list.json, the catalog only supplied the listed mods");
    }

    [Fact]
    public void A_hash_a_listed_mod_still_ships_is_not_enforced()
    {
        using var fx = MainViewModelFixture.CreateReady();
        var bytes = Encoding.UTF8.GetBytes("renamed mod, same dll");
        var stub = Path.Combine(fx.DataRoot, "Econ.dll");
        File.WriteAllBytes(stub, bytes);
        var vm = fx.CreateVm(
            confirmHighRisk: _ => true,
            pickPackageType: () => ModPackageType.MelonMod,
            openDll: () => stub,
            removalPublicKey: _signer.PublicKey);
        vm.ApplyCatalogRoot(new CatalogRoot
        {
            Mods = [new CatalogMod { Id = "econ-v2", Name = "Econ", File = "mods/e/Econ.dll", Sha256 = Sha(bytes) }],
            RemovedSigned = _signer.Sign([new CatalogRemoval { Id = "econ-v1", Sha256 = [Sha(bytes)] }])
        });

        vm.ImportDllCommand.Execute(null);

        fx.Library.List().Should().HaveCount(2);
        vm.LogText.Should().Contain("econ-v2");
    }

    /// <summary>
    /// Both branch records name the Steam junction, which reaches only the mounted (official)
    /// store. The beta copy must still be found and deleted in the beta store itself.
    /// </summary>
    [Fact]
    public void Dual_folder_takedown_cleans_the_unmounted_store()
    {
        using var fx = MainViewModelFixture.CreateReadyDualFolder();
        fx.WriteBranchConfig(new BranchSwitchConfig
        {
            Enabled = true,
            WizardStep = BranchWizardStep.Ready,
            ActiveBranch = GameBranch.Official,
            SteamLinkPath = fx.SteamLink,
            OfficialStorePath = fx.OfficialStore,
            BetaStorePath = fx.BetaStore
        });
        var bytes = Encoding.UTF8.GetBytes("cam build");
        Directory.CreateDirectory(Path.Combine(fx.BetaStore, "Mods"));
        File.WriteAllBytes(Path.Combine(fx.BetaStore, "Mods", "Cam.dll"), bytes);
        var betaManifest = fx.Paths.GetDeployManifestPath(GameBranch.Beta, enabled: true);
        fx.Store.Save(betaManifest, new DeployManifest
        {
            GamePath = fx.SteamLink,
            Files = [new ManifestFileEntry { RelativePath = "Mods/Cam.dll", PackageId = "cam-aaaaaaaa", Sha256 = Sha(bytes) }]
        });
        var vm = fx.CreateVm(removalPublicKey: _signer.PublicKey);

        vm.ApplyCatalogRoot(Signed(CamRemoval()));

        File.Exists(Path.Combine(fx.BetaStore, "Mods", "Cam.dll")).Should().BeFalse();
        fx.Store.LoadOrDefault(betaManifest, () => new DeployManifest()).Files.Should().BeEmpty();
        fx.Library.List().Should().BeEmpty();
    }
}
