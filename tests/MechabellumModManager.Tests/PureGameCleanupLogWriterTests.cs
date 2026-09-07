using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class PureGameCleanupLogWriterTests
{
    [Fact]
    public void Durable_log_keeps_early_lines_after_roaming_appdata_deleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-cleanlog-" + Guid.NewGuid().ToString("N"));
        var roamingDir = Path.Combine(root, "roaming", PathsService.AppFolderName);
        var durablePath = Path.Combine(root, "local", PathsService.AppFolderName, "pure-game-cleanup.log");
        var roamingPath = Path.Combine(roamingDir, "pure-game-cleanup.log");
        Directory.CreateDirectory(roamingDir);
        try
        {
            var writer = new PureGameCleanupLogWriter(durablePath, roamingPath);
            writer.Append("skipped teardown: Steam running");
            writer.Append("deleted MelonLoader");

            Directory.Delete(roamingDir, recursive: true);
            Directory.Exists(roamingDir).Should().BeFalse();

            writer.Append("deleted AppData");
            writer.WriteAll(["skipped teardown: Steam running", "deleted MelonLoader", "deleted AppData", "exit=0"]);

            File.Exists(durablePath).Should().BeTrue();
            var durable = File.ReadAllText(durablePath);
            durable.Should().Contain("skipped teardown: Steam running");
            durable.Should().Contain("deleted AppData");
            durable.Should().Contain("exit=0");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
}
