using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class BranchSwitchServiceTests
{
    const string SampleAcf = """
"AppState"
{
	"appid"		"669330"
	"UserConfig"
	{
		"language"		"english"
		"BetaKey"		"oldbeta"
	}
}
""";

    [Fact]
    public void SwapJunction_official_to_beta_preserves_both_store_folders()
    {
        using var h = Harness.CreateReadyDualFolder();

        var result = h.Svc.TrySwapJunction(GameBranch.Beta);

        result.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeTrue();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.BetaStore));
        File.ReadAllText(Path.Combine(h.OfficialStore, "marker.txt")).Should().Be("official");
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("beta");
        File.ReadAllText(Path.Combine(h.SteamLink, "marker.txt")).Should().Be("beta");
        h.Svc.LoadConfig().ActiveBranch.Should().Be(GameBranch.Beta);
    }

    [Fact]
    public void SwapJunction_refuses_when_steam_running()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Probe.SteamRunning = true;
        var acfBefore = File.ReadAllText(h.AcfPath);

        var result = h.Svc.TrySwapJunction(GameBranch.Beta);

        result.Success.Should().BeFalse();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        File.ReadAllText(h.AcfPath).Should().Be(acfBefore);
        File.Exists(h.Paths.BranchSwitchJournalPath).Should().BeFalse();
    }

    [Fact]
    public void SwapJunction_refuses_when_game_running()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Probe.GameRunning = true;

        var result = h.Svc.TrySwapJunction(GameBranch.Beta);

        result.Success.Should().BeFalse();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        h.Svc.LoadConfig().ActiveBranch.Should().Be(GameBranch.Official);
    }

    [Fact]
    public void SwapJunction_does_not_delete_store_when_unlinking()
    {
        using var h = Harness.CreateReadyDualFolder();

        h.Svc.TrySwapJunction(GameBranch.Beta).Success.Should().BeTrue();
        h.Svc.TrySwapJunction(GameBranch.Official).Success.Should().BeTrue();

        Directory.Exists(h.OfficialStore).Should().BeTrue();
        Directory.Exists(h.BetaStore).Should().BeTrue();
        File.Exists(Path.Combine(h.OfficialStore, "Mechabellum.exe")).Should().BeTrue();
        File.Exists(Path.Combine(h.BetaStore, "Mechabellum.exe")).Should().BeTrue();
        File.ReadAllText(Path.Combine(h.OfficialStore, "marker.txt")).Should().Be("official");
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("beta");
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
    }

    [Fact]
    public void SwapJunction_does_not_write_beta_key()
    {
        using var h = Harness.CreateReadyDualFolder();
        var acfBefore = File.ReadAllText(h.AcfPath);

        h.Svc.TrySwapJunction(GameBranch.Beta).Success.Should().BeTrue();

        File.ReadAllText(h.AcfPath).Should().Be(acfBefore);
        h.Editor.ReadBetaKey(h.AcfPath).Should().Be("oldbeta");
    }

    [Fact]
    public void Interrupted_swap_writes_journal_and_repair_restores_link()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Junctions.RemainingCreateFailures = int.MaxValue;

        var swap = h.Svc.TrySwapJunction(GameBranch.Beta);

        swap.Success.Should().BeFalse();
        Directory.Exists(h.SteamLink).Should().BeFalse();
        File.Exists(h.Paths.BranchSwitchJournalPath).Should().BeTrue();
        File.ReadAllText(Path.Combine(h.OfficialStore, "marker.txt")).Should().Be("official");
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("beta");

        h.Junctions.RemainingCreateFailures = 0;
        var repair = h.Svc.TryRepairFromJournal();

        repair.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeTrue();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        File.Exists(h.Paths.BranchSwitchJournalPath).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.SteamLink, "marker.txt")).Should().Be("official");
    }

    [Fact]
    public void SilentSetBeta_writes_BetaBranchName_for_beta()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Cfg.BetaBranchName = "publicbeta";
        h.Svc.SaveConfig(h.Cfg);

        var result = h.Svc.TrySilentSetBeta(GameBranch.Beta);

        result.Success.Should().BeTrue();
        result.DegradeToManualBeta.Should().BeFalse();
        h.Editor.ReadBetaKey(h.AcfPath).Should().Be("publicbeta");
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        h.Svc.LoadConfig().ManifestBackupPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SilentSetBeta_clears_beta_key_for_official()
    {
        using var h = Harness.CreateReadyDualFolder();

        var result = h.Svc.TrySilentSetBeta(GameBranch.Official);

        result.Success.Should().BeTrue();
        h.Editor.ReadBetaKey(h.AcfPath).Should().BeNull();
        File.ReadAllText(h.AcfPath).Should().NotContain("BetaKey");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SilentSetBeta_refuses_when_game_or_steam_running(bool game, bool steam)
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Probe.GameRunning = game;
        h.Probe.SteamRunning = steam;
        var acfBefore = File.ReadAllText(h.AcfPath);

        var result = h.Svc.TrySilentSetBeta(GameBranch.Beta);

        result.Success.Should().BeFalse();
        File.ReadAllText(h.AcfPath).Should().Be(acfBefore);
    }

    [Fact]
    public void ArchiveCurrentAs_leaves_steam_link_path_empty_without_relinking()
    {
        using var h = Harness.CreateWizardStart();

        var result = h.Svc.ArchiveCurrentAs(GameBranch.Official);

        result.Success.Should().BeTrue();
        Directory.Exists(h.SteamLink).Should().BeFalse();
        h.Junctions.IsJunction(h.SteamLink).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.OfficialStore, "marker.txt")).Should().Be("current");
        File.Exists(Path.Combine(h.OfficialStore, "Mechabellum.exe")).Should().BeTrue();
        h.Svc.LoadConfig().WizardStep.Should().Be(BranchWizardStep.ArchivedA);
    }

    [Fact]
    public void ArchiveDownloadedAs_then_CreateLinkTo_follows_spec_order()
    {
        using var h = Harness.CreateWizardStart();
        h.Svc.ArchiveCurrentAs(GameBranch.Official).Success.Should().BeTrue();
        Directory.Exists(h.SteamLink).Should().BeFalse();

        SeedGameRoot(h.SteamLink, "downloaded");
        var archiveB = h.Svc.ArchiveDownloadedAs(GameBranch.Beta);
        archiveB.Success.Should().BeTrue();
        Directory.Exists(h.SteamLink).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("downloaded");
        h.Svc.LoadConfig().WizardStep.Should().Be(BranchWizardStep.ArchivedB);

        var link = h.Svc.CreateLinkTo(GameBranch.Official);
        link.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeTrue();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        File.ReadAllText(Path.Combine(h.SteamLink, "marker.txt")).Should().Be("current");
        File.ReadAllText(Path.Combine(h.OfficialStore, "marker.txt")).Should().Be("current");
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("downloaded");
        h.Svc.LoadConfig().ActiveBranch.Should().Be(GameBranch.Official);
        h.Svc.LoadConfig().WizardStep.Should().Be(BranchWizardStep.Linked);
    }

    [Fact]
    public void CreateLinkTo_existing_junction_to_target_is_success()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Cfg.Enabled = false;
        h.Cfg.WizardStep = BranchWizardStep.Linked;
        h.Svc.SaveConfig(h.Cfg);

        var result = h.Svc.CreateLinkTo(GameBranch.Official);

        result.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeTrue();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        var cfg = h.Svc.LoadConfig();
        cfg.WizardStep.Should().Be(BranchWizardStep.Linked);
        cfg.Enabled.Should().BeFalse();
        cfg.ActiveBranch.Should().Be(GameBranch.Official);
    }

    [Fact]
    public void MigrateLegacyManifestIfNeeded_copies_once()
    {
        using var h = Harness.CreateReadyDualFolder();
        var legacy = h.Paths.DeployManifestPath;
        File.WriteAllText(legacy, """{"gamePath":"legacy"}""");
        var dest = h.Paths.GetDeployManifestPath(GameBranch.Official, enabled: true);
        File.Exists(dest).Should().BeFalse();

        h.Svc.MigrateLegacyManifestIfNeeded(GameBranch.Official);

        File.ReadAllText(dest).Should().Be("""{"gamePath":"legacy"}""");
        File.ReadAllText(legacy).Should().Be("""{"gamePath":"legacy"}""");

        File.WriteAllText(legacy, """{"gamePath":"changed"}""");
        h.Svc.MigrateLegacyManifestIfNeeded(GameBranch.Official);
        File.ReadAllText(dest).Should().Be("""{"gamePath":"legacy"}""");
    }

    [Fact]
    public void LoadConfig_SaveConfig_roundtrip()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Cfg.BetaBranchName = "publicbeta";
        h.Cfg.OfficialProfileId = "p1";
        h.Svc.SaveConfig(h.Cfg);

        var loaded = h.Svc.LoadConfig();
        loaded.BetaBranchName.Should().Be("publicbeta");
        loaded.OfficialProfileId.Should().Be("p1");
        loaded.ActiveBranch.Should().Be(GameBranch.Official);
        loaded.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Repair_live_junction_on_target_syncs_ActiveBranch()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Junctions.DeleteJunction(h.SteamLink);
        h.Junctions.CreateJunction(h.SteamLink, h.BetaStore);
        h.Cfg.ActiveBranch = GameBranch.Official;
        h.Svc.SaveConfig(h.Cfg);
        h.Store.Save(h.Paths.BranchSwitchJournalPath, new BranchSwitchJournal
        {
            Phase = "unlinked",
            PreviousBranch = GameBranch.Official,
            TargetBranch = GameBranch.Beta,
            SteamLinkPath = Path.GetFullPath(h.SteamLink),
            PreviousStorePath = Path.GetFullPath(h.OfficialStore),
            TargetStorePath = Path.GetFullPath(h.BetaStore)
        });

        var repair = h.Svc.TryRepairFromJournal();

        repair.Success.Should().BeTrue();
        h.Svc.LoadConfig().ActiveBranch.Should().Be(GameBranch.Beta);
        File.Exists(h.Paths.BranchSwitchJournalPath).Should().BeFalse();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.BetaStore));
    }

    [Fact]
    public void Teardown_restores_current_store_to_steam_link_and_legacy_manifest()
    {
        using var h = Harness.CreateReadyDualFolder();
        var branchManifest = h.Paths.GetDeployManifestPath(GameBranch.Official, enabled: true);
        File.WriteAllText(branchManifest, """{"gamePath":"official-branch"}""");
        File.WriteAllText(h.Paths.DeployManifestPath, """{"gamePath":"stale"}""");

        var result = h.Svc.TryTeardown(deleteOtherStore: false);

        result.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeFalse();
        Directory.Exists(h.SteamLink).Should().BeTrue();
        File.ReadAllText(Path.Combine(h.SteamLink, "marker.txt")).Should().Be("official");
        File.Exists(Path.Combine(h.SteamLink, "Mechabellum.exe")).Should().BeTrue();
        Directory.Exists(h.OfficialStore).Should().BeFalse();
        Directory.Exists(h.BetaStore).Should().BeTrue();
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("beta");
        File.ReadAllText(h.Paths.DeployManifestPath).Should().Be("""{"gamePath":"official-branch"}""");
        var cfg = h.Svc.LoadConfig();
        cfg.Enabled.Should().BeFalse();
        cfg.WizardStep.Should().Be(BranchWizardStep.None);
    }

    [Fact]
    public void Teardown_refuses_when_steam_running()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Probe.SteamRunning = true;

        var result = h.Svc.TryTeardown(deleteOtherStore: false);

        result.Success.Should().BeFalse();
        h.Junctions.IsJunction(h.SteamLink).Should().BeTrue();
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
        h.Svc.LoadConfig().Enabled.Should().BeTrue();
    }

    [Fact]
    public void ArchiveCurrentAs_already_junction_onto_dest_unlinks_only()
    {
        using var h = Harness.CreateReadyDualFolder();

        var result = h.Svc.ArchiveCurrentAs(GameBranch.Official);

        result.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeFalse();
        Directory.Exists(h.SteamLink).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.OfficialStore, "marker.txt")).Should().Be("official");
        File.Exists(Path.Combine(h.OfficialStore, "Mechabellum.exe")).Should().BeTrue();
        h.Svc.LoadConfig().WizardStep.Should().Be(BranchWizardStep.ArchivedA);
    }

    [Fact]
    public void Repair_corrupt_journal_is_not_deleted()
    {
        using var h = Harness.CreateReadyDualFolder();
        const string garbage = "{not-json";
        File.WriteAllText(h.Paths.BranchSwitchJournalPath, garbage);

        var repair = h.Svc.TryRepairFromJournal();

        repair.Success.Should().BeFalse();
        File.Exists(h.Paths.BranchSwitchJournalPath).Should().BeTrue();
        File.ReadAllText(h.Paths.BranchSwitchJournalPath).Should().Be(garbage);
        h.Junctions.ResolveTarget(h.SteamLink).Should().Be(Path.GetFullPath(h.OfficialStore));
    }

    [Fact]
    public void Repair_does_not_fall_back_to_target_when_previous_is_broken()
    {
        using var h = Harness.CreateReadyDualFolder();
        h.Junctions.DeleteJunction(h.SteamLink);
        var journalPath = h.Paths.BranchSwitchJournalPath;
        h.Store.Save(journalPath, new BranchSwitchJournal
        {
            Phase = "unlinked",
            PreviousBranch = GameBranch.Official,
            TargetBranch = GameBranch.Beta,
            SteamLinkPath = Path.GetFullPath(h.SteamLink),
            PreviousStorePath = Path.Combine(h.Root, "missing-previous"),
            TargetStorePath = Path.GetFullPath(h.BetaStore)
        });

        var repair = h.Svc.TryRepairFromJournal();

        repair.Success.Should().BeFalse();
        File.Exists(journalPath).Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeFalse();
        Directory.Exists(h.SteamLink).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.BetaStore, "marker.txt")).Should().Be("beta");
    }

    static void SeedGameRoot(string dir, string marker)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "exe");
        File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "dll");
        File.WriteAllText(Path.Combine(dir, "marker.txt"), marker);
    }

    sealed class Harness : IDisposable
    {
        public string Root { get; }
        public string SteamLink { get; }
        public string OfficialStore { get; }
        public string BetaStore { get; }
        public string AcfPath { get; }
        public PathsService Paths { get; }
        public JsonStore Store { get; }
        public FakeProcessProbe Probe { get; }
        public ControllableJunctionService Junctions { get; }
        public SteamBetaKeyEditor Editor { get; }
        public BranchSwitchService Svc { get; }
        public BranchSwitchConfig Cfg { get; }

        Harness(
            string root,
            string steamLink,
            string officialStore,
            string betaStore,
            string acfPath,
            PathsService paths,
            JsonStore store,
            FakeProcessProbe probe,
            ControllableJunctionService junctions,
            SteamBetaKeyEditor editor,
            BranchSwitchService svc,
            BranchSwitchConfig cfg)
        {
            Root = root;
            SteamLink = steamLink;
            OfficialStore = officialStore;
            BetaStore = betaStore;
            AcfPath = acfPath;
            Paths = paths;
            Store = store;
            Probe = probe;
            Junctions = junctions;
            Editor = editor;
            Svc = svc;
            Cfg = cfg;
        }

        public static Harness CreateReadyDualFolder()
        {
            var h = CreateCore();
            SeedGameRoot(h.OfficialStore, "official");
            SeedGameRoot(h.BetaStore, "beta");
            h.Junctions.Inner.CreateJunction(h.SteamLink, h.OfficialStore);
            File.WriteAllText(h.AcfPath, SampleAcf);
            h.Cfg.Enabled = true;
            h.Cfg.WizardStep = BranchWizardStep.Ready;
            h.Cfg.ActiveBranch = GameBranch.Official;
            h.Cfg.BetaBranchName = "publicbeta";
            h.Svc.SaveConfig(h.Cfg);
            return h;
        }

        public static Harness CreateWizardStart()
        {
            var h = CreateCore();
            SeedGameRoot(h.SteamLink, "current");
            File.WriteAllText(h.AcfPath, SampleAcf);
            h.Cfg.Enabled = false;
            h.Cfg.WizardStep = BranchWizardStep.Declared;
            h.Cfg.BetaBranchName = "publicbeta";
            h.Svc.SaveConfig(h.Cfg);
            return h;
        }

        static Harness CreateCore()
        {
            var root = Path.Combine(Path.GetTempPath(), "mmm-bs-" + Guid.NewGuid().ToString("N"));
            var data = Path.Combine(root, "data");
            var steamapps = Path.Combine(root, "steamapps");
            var common = Path.Combine(steamapps, "common");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(common);

            var steamLink = Path.Combine(common, "Mechabellum");
            var official = Path.Combine(common, "Mechabellum_official");
            var beta = Path.Combine(common, "Mechabellum_beta");
            var acf = Path.Combine(steamapps, "appmanifest_669330.acf");

            var paths = new PathsService(data);
            var store = new JsonStore();
            var probe = new FakeProcessProbe();
            var junctions = new ControllableJunctionService();
            var editor = new SteamBetaKeyEditor(probe);
            var cfg = new BranchSwitchConfig
            {
                SteamLinkPath = steamLink,
                OfficialStorePath = official,
                BetaStorePath = beta
            };
            var svc = new BranchSwitchService(paths, store, probe, junctions, editor);
            return new Harness(root, steamLink, official, beta, acf, paths, store, probe, junctions, editor, svc, cfg);
        }

        public void Dispose()
        {
            try
            {
                if (Junctions.Inner.IsJunction(SteamLink))
                    Junctions.Inner.DeleteJunction(SteamLink);
            }
            catch
            {
                // Best-effort unlink before recursive delete.
            }

            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Temp leftover is non-fatal.
            }
        }
    }
}

sealed class FakeProcessProbe : IProcessProbe
{
    public bool GameRunning { get; set; }
    public bool SteamRunning { get; set; }
    public bool IsGameRunning() => GameRunning;
    public bool IsSteamRunning() => SteamRunning;
    public bool IsGameOrSteamRunning() => GameRunning || SteamRunning;
}

sealed class ControllableJunctionService : IJunctionService
{
    public JunctionService Inner { get; } = new();
    public int RemainingCreateFailures { get; set; }

    public bool IsJunction(string path) => Inner.IsJunction(path);
    public string? ResolveTarget(string path) => Inner.ResolveTarget(path);
    public void DeleteJunction(string linkPath) => Inner.DeleteJunction(linkPath);

    public void CreateJunction(string linkPath, string targetPath)
    {
        if (RemainingCreateFailures > 0)
        {
            RemainingCreateFailures--;
            throw new IOException("simulated crash before relink");
        }

        Inner.CreateJunction(linkPath, targetPath);
    }
}
