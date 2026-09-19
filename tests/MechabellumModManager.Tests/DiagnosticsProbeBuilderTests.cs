using System.IO;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DiagnosticsProbeBuilderTests
{
    [Fact]
    public void Junction_desync_when_link_empty_but_target_store_looks_valid()
    {
        DiagnosticsProbeBuilder.IsJunctionDesync(
            isJunction: true,
            target: @"H:\Steam\steamapps\common\Mechabellum_official",
            official: @"H:\Steam\steamapps\common\Mechabellum_official",
            beta: @"H:\Steam\steamapps\common\Mechabellum_beta",
            linkOk: false,
            officialOk: true,
            betaOk: true).Should().BeTrue();
    }

    [Fact]
    public void Junction_desync_false_when_link_and_store_match()
    {
        DiagnosticsProbeBuilder.IsJunctionDesync(
            isJunction: true,
            target: @"H:\Steam\steamapps\common\Mechabellum_official",
            official: @"H:\Steam\steamapps\common\Mechabellum_official",
            beta: @"H:\Steam\steamapps\common\Mechabellum_beta",
            linkOk: true,
            officialOk: true,
            betaOk: true).Should().BeFalse();
    }

    [Fact]
    public void Melon_log_open_for_append_is_still_readable()
    {
        var root = CreateGameWithLog("Preferences Loaded\n");
        var log = Path.Combine(root, "MelonLoader", "Latest.log");
        try
        {
            using var hold = new FileStream(log, FileMode.Open, FileAccess.Write, FileShare.Read);
            var melon = DiagnosticsProbeBuilder.InspectMelonLog(log, DateTimeOffset.Now);
            melon.ContainsKey("unreadable").Should().BeFalse();
            melon["hasPreferencesLoaded"].Should().Be(true);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Exclusively_locked_melon_log_is_a_finding()
    {
        var root = CreateGameWithLog("Preferences Loaded\n");
        var log = Path.Combine(root, "MelonLoader", "Latest.log");
        var data = Path.Combine(Path.GetTempPath(), "mmm-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var hold = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var bundle = DiagnosticsProbeBuilder.Build(new DiagnosticsExportRequest
            {
                Paths = new PathsService(data),
                GamePath = root
            });
            var findings = bundle["findings"] as List<string>;
            findings.Should().Contain("melon_log_unreadable");
        }
        finally
        {
            Directory.Delete(root, true);
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
    }

    static string CreateGameWithLog(string text)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-probe-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        File.WriteAllText(Path.Combine(root, "MelonLoader", "Latest.log"), text);
        return root;
    }
}
