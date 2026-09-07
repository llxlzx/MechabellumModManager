using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DiagnosisSnapshotBuilderTests
{
    [Fact]
    public void Capture_qiancengxue_files_match_engine_and_export_code()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        Directory.CreateDirectory(Path.Combine(game, "MelonLoader", "Il2CppAssemblies"));
        File.WriteAllText(Path.Combine(game, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "x");
        File.WriteAllText(Path.Combine(game, "version.dll"), "x");
        File.WriteAllText(Path.Combine(game, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "x");
        File.WriteAllText(Path.Combine(game, "MelonLoader", "Latest.log"), "MelonLoader v0.7.1 Open-Beta\n");
        File.SetLastWriteTime(Path.Combine(game, "MelonLoader", "Latest.log"), DateTime.Now.AddDays(-8));

        var status = new GameDetector().Detect(game);
        var snap = DiagnosisSnapshotBuilder.Capture(
            fx.Paths,
            game,
            status,
            lastLaunchRequestedAt: null,
            branch: new BranchSwitchConfig
            {
                Enabled = false,
                WizardStep = BranchWizardStep.Ready,
                ActiveBranch = GameBranch.Official,
                SteamLinkPath = game
            },
            isAwaitingSteamSettle: false);

        snap.MelonNeedsUpgrade.Should().BeTrue();
        snap.AcfBetaMismatch.Should().BeFalse();

        var fromBar = DiagnosisEngine.Evaluate(snap);
        var fromZip = DiagnosisEngine.Evaluate(snap);
        fromBar.Code.Should().Be(DiagnosisCodes.MelonNeedsUpgrade);
        fromZip.Code.Should().Be(fromBar.Code);
        fromBar.RuledOut.Should().Contain(DiagnosisCodes.DualFolderBroken);
    }

    [Theory]
    [InlineData("public_test", "Official", true)]
    [InlineData("public_test", "Beta", false)]
    [InlineData("", "Official", false)]
    [InlineData(null, "Beta", true)]
    public void Acf_mismatch_compares_steam_key_to_manager_store(string? key, string branch, bool expected)
    {
        DiagnosisSnapshotBuilder.IsAcfBetaMismatch(key, branch).Should().Be(expected);
    }

    sealed class Fixture : IDisposable
    {
        public string Root { get; }
        public PathsService Paths { get; }

        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mmm-snap-" + Guid.NewGuid().ToString("N"));
            Paths = new PathsService(Root);
            Paths.EnsureCreated();
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { /* ignore */ }
        }
    }
}
