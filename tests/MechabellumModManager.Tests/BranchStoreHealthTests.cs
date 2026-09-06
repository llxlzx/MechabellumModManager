using FluentAssertions;
using MechabellumModManager.Services;

public class BranchStoreHealthTests
{
    [Fact]
    public void Hollow_store_cannot_promote()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "x");
            var r = BranchStoreHealth.Evaluate(dir, SettledAcf());
            r.IsGameRoot.Should().BeFalse();
            r.CanPromoteReady.Should().BeFalse();
            r.PrimaryReason.Should().Be("store_not_game_root");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Root_ok_but_hardfault_acf_cannot_promote()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "x");
            File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "x");
            var acf =
                """
                "AppState"
                {
                	"StateFlags"		"1190"
                	"buildid"		"1"
                	"TargetBuildID"		"1"
                	"BytesToDownload"		"0"
                	"BytesDownloaded"		"0"
                }
                """;
            var r = BranchStoreHealth.Evaluate(dir, acf);
            r.IsGameRoot.Should().BeTrue();
            r.AcfHardFault.Should().BeTrue();
            r.AcfAllowsSnapshot.Should().BeFalse();
            r.CanPromoteReady.Should().BeFalse();
            r.PrimaryReason.Should().Be("acf_hard_fault");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Complete_root_and_settled_acf_can_promote()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "x");
            File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "x");
            var r = BranchStoreHealth.Evaluate(dir, SettledAcf());
            r.CanPromoteReady.Should().BeTrue();
            r.PrimaryReason.Should().BeNull();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Wizard_download_ready_requires_root_settled_and_steam_exited()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Mechabellum.exe"), "x");
            File.WriteAllText(Path.Combine(dir, "GameAssembly.dll"), "x");
            BranchStoreHealth.IsWizardDownloadReady(dir, SettledAcf(), steamOrGameRunning: true)
                .Should().BeFalse();
            BranchStoreHealth.IsWizardDownloadReady(dir, SettledAcf(), steamOrGameRunning: false)
                .Should().BeTrue();
            BranchStoreHealth.IsWizardDownloadReady(dir, null, steamOrGameRunning: false)
                .Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    static string SettledAcf() =>
        """
        "AppState"
        {
        	"StateFlags"		"4"
        	"buildid"		"1"
        	"TargetBuildID"		"1"
        	"BytesToDownload"		"0"
        	"BytesDownloaded"		"0"
        }
        """;
}
