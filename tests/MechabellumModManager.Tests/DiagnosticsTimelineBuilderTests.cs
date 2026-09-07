using FluentAssertions;
using MechabellumModManager.Services;

public class DiagnosticsTimelineBuilderTests
{
    [Fact]
    public void Timeline_includes_structured_event_without_chinese_needle()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-tl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var events = new ManagerEventLog(dir);
            events.Write(ManagerEventLog.LaunchRequested, new Dictionary<string, string?>
            {
                ["mode"] = "SteamThenExe"
            });
            var jsonl = File.ReadAllText(events.FilePath);

            var timeline = DiagnosticsTimelineBuilder.MergeEventsAndLogs(
                jsonl,
                "[12:00:00] hello without any known chinese needle");

            timeline.Should().Contain("launch_requested");
            timeline.Should().Contain("SteamThenExe");
            timeline.Should().NotContain("hello without");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Timeline_still_matches_chinese_needles_as_fallback()
    {
        var timeline = DiagnosticsTimelineBuilder.MergeEventsAndLogs(
            eventsJsonl: null,
            "[12:00:00] 游戏与 MelonLoader 已就绪");

        timeline.Should().Contain("game_ready");
    }

    [Fact]
    public void Timeline_maps_loader_not_injected_chinese_needle()
    {
        var timeline = DiagnosticsTimelineBuilder.MergeEventsAndLogs(
            eventsJsonl: null,
            "[12:00:00] Loader 未在本次启动注入");

        timeline.Should().Contain("loader_not_injected");
    }
}
