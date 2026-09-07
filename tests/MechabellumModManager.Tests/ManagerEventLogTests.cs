using FluentAssertions;
using MechabellumModManager.Services;

public class ManagerEventLogTests
{
    [Fact]
    public void Write_appends_jsonl_with_code_and_data()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-events-" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new ManagerEventLog(dir);
            log.Write(ManagerEventLog.LaunchRequested, new Dictionary<string, string?>
            {
                ["mode"] = "SteamThenExe"
            });

            File.Exists(log.FilePath).Should().BeTrue();
            var line = File.ReadAllText(log.FilePath).Trim();
            line.Should().Contain("\"code\":\"launch_requested\"");
            line.Should().Contain("SteamThenExe");

            var recent = log.ReadRecent();
            recent.Should().ContainSingle();
            recent[0].Code.Should().Be(ManagerEventLog.LaunchRequested);
            recent[0].Data.Should().ContainKey("mode");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Write_does_not_throw_when_directory_cannot_be_created()
    {
        var blocked = Path.Combine(Path.GetTempPath(), "mmm-events-file-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocked, "not-a-dir");
        try
        {
            var log = new ManagerEventLog(blocked);
            var act = () => log.Write(ManagerEventLog.LaunchRequested);
            act.Should().NotThrow();
        }
        finally
        {
            try { File.Delete(blocked); } catch { /* ignore */ }
        }
    }
}
