using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class OrphanDualLayoutRepairTests
{
    [Fact]
    public void Inspect_detects_junction_orphan_even_when_config_disabled_and_paths_cleared()
    {
        using var h = OrphanHarness.CreateJunctionOrphan();

        var info = h.Svc.InspectOrphanDualLayout(h.SteamLink);

        info.IsOrphan.Should().BeTrue();
        info.LinkIsJunction.Should().BeTrue();
        info.LeftoverStorePaths.Should().Contain(p => PathsEqual(p, h.BetaStore));
    }

    [Fact]
    public void Inspect_false_for_clean_single_Mechabellum()
    {
        using var h = OrphanHarness.CreateCleanSingle();

        h.Svc.InspectOrphanDualLayout(h.SteamLink).IsOrphan.Should().BeFalse();
    }

    [Fact]
    public void Inspect_detects_real_link_plus_leftover_store()
    {
        using var h = OrphanHarness.CreateRealLinkWithLeftoverBeta();

        var info = h.Svc.InspectOrphanDualLayout(h.SteamLink);
        info.IsOrphan.Should().BeTrue();
        info.LinkIsJunction.Should().BeFalse();
        info.LeftoverStorePaths.Should().Contain(p => PathsEqual(p, h.BetaStore));
    }

    [Fact]
    public void Repair_materializes_junction_and_keeps_other_store()
    {
        using var h = OrphanHarness.CreateJunctionOrphan();

        var result = h.Svc.TryRepairOrphanDualLayout(deleteOtherStore: false, preferredLinkPath: h.SteamLink);

        result.Success.Should().BeTrue();
        h.Junctions.IsJunction(h.SteamLink).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.SteamLink, "marker.txt")).Should().Be("official");
        Directory.Exists(h.OfficialStore).Should().BeFalse();
        Directory.Exists(h.BetaStore).Should().BeTrue();
        var cfg = h.Svc.LoadConfig();
        cfg.Enabled.Should().BeFalse();
        cfg.OfficialStorePath.Should().BeEmpty();
        cfg.BetaStorePath.Should().BeEmpty();
        cfg.SteamLinkPath.Should().Be(Path.GetFullPath(h.SteamLink));
    }

    [Fact]
    public void Repair_deletes_other_store_when_requested()
    {
        using var h = OrphanHarness.CreateJunctionOrphan();

        var result = h.Svc.TryRepairOrphanDualLayout(deleteOtherStore: true, preferredLinkPath: h.SteamLink);

        result.Success.Should().BeTrue();
        Directory.Exists(h.BetaStore).Should().BeFalse();
        File.Exists(Path.Combine(h.SteamLink, "Mechabellum.exe")).Should().BeTrue();
    }

    [Fact]
    public void Repair_leftover_only_deletes_stores_when_requested()
    {
        using var h = OrphanHarness.CreateRealLinkWithLeftoverBeta();

        var result = h.Svc.TryRepairOrphanDualLayout(deleteOtherStore: true, preferredLinkPath: h.SteamLink);

        result.Success.Should().BeTrue();
        Directory.Exists(h.BetaStore).Should().BeFalse();
        File.ReadAllText(Path.Combine(h.SteamLink, "marker.txt")).Should().Be("link");
    }

    [Fact]
    public void Repair_refuses_when_steam_running()
    {
        using var h = OrphanHarness.CreateJunctionOrphan();
        h.Probe.SteamRunning = true;

        var result = h.Svc.TryRepairOrphanDualLayout(deleteOtherStore: false, preferredLinkPath: h.SteamLink);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Steam");
        h.Junctions.IsJunction(h.SteamLink).Should().BeTrue();
    }

    static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    sealed class OrphanHarness : IDisposable
    {
        public string Root { get; }
        public string SteamLink { get; }
        public string OfficialStore { get; }
        public string BetaStore { get; }
        public FakeProcessProbe Probe { get; }
        public ControllableJunctionService Junctions { get; }
        public BranchSwitchService Svc { get; }

        OrphanHarness(
            string root,
            string steamLink,
            string official,
            string beta,
            FakeProcessProbe probe,
            ControllableJunctionService junctions,
            BranchSwitchService svc)
        {
            Root = root;
            SteamLink = steamLink;
            OfficialStore = official;
            BetaStore = beta;
            Probe = probe;
            Junctions = junctions;
            Svc = svc;
        }

        public static OrphanHarness CreateJunctionOrphan()
        {
            var h = CreateCore();
            Seed(h.OfficialStore, "official");
            Seed(h.BetaStore, "beta");
            h.Junctions.Inner.CreateJunction(h.SteamLink, h.OfficialStore);
            h.Svc.SaveConfig(new BranchSwitchConfig
            {
                Enabled = false,
                WizardStep = BranchWizardStep.None,
                SteamLinkPath = h.SteamLink,
                OfficialStorePath = "",
                BetaStorePath = "",
                ActiveBranch = GameBranch.Official
            });
            return h;
        }

        public static OrphanHarness CreateCleanSingle()
        {
            var h = CreateCore();
            Seed(h.SteamLink, "solo");
            h.Svc.SaveConfig(new BranchSwitchConfig
            {
                Enabled = false,
                WizardStep = BranchWizardStep.None,
                SteamLinkPath = h.SteamLink
            });
            return h;
        }

        public static OrphanHarness CreateRealLinkWithLeftoverBeta()
        {
            var h = CreateCore();
            Seed(h.SteamLink, "link");
            Seed(h.BetaStore, "beta");
            h.Svc.SaveConfig(new BranchSwitchConfig
            {
                Enabled = false,
                WizardStep = BranchWizardStep.None,
                SteamLinkPath = h.SteamLink,
                OfficialStorePath = "",
                BetaStorePath = ""
            });
            return h;
        }

        static OrphanHarness CreateCore()
        {
            var root = Path.Combine(Path.GetTempPath(), "mmm-orphan-" + Guid.NewGuid().ToString("N"));
            var data = Path.Combine(root, "data");
            var common = Path.Combine(root, "steamapps", "common");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(common);
            var link = Path.Combine(common, "Mechabellum");
            var official = Path.Combine(common, "Mechabellum_official");
            var beta = Path.Combine(common, "Mechabellum_beta");
            var paths = new PathsService(data);
            var store = new JsonStore();
            var probe = new FakeProcessProbe();
            var junctions = new ControllableJunctionService();
            var svc = new BranchSwitchService(paths, store, probe, junctions, new SteamBetaKeyEditor(probe));
            return new OrphanHarness(root, link, official, beta, probe, junctions, svc);
        }

        static void Seed(string dir, string marker)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "exe");
            File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "dll");
            File.WriteAllText(Path.Combine(dir, "marker.txt"), marker);
        }

        public void Dispose()
        {
            try
            {
                if (Junctions.Inner.IsJunction(SteamLink))
                    Junctions.Inner.DeleteJunction(SteamLink);
            }
            catch { /* ignore */ }

            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch { /* ignore */ }
        }
    }
}
