using System.IO.Compression;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class IncidentSeparationTests
{
    const string Missing = "这次材料没有";

    [Fact]
    public void Null_summary_has_no_window_material()
    {
        IncidentSeparation.Evaluate(null, null).Window.Should().Be(Missing);
    }

    [Fact]
    public void Empty_summary_has_no_window_material()
    {
        IncidentSeparation.Evaluate("", "ignored").Window.Should().Be(Missing);
    }

    [Fact]
    public void Hang_trigger_explains_window_and_not_battle_result()
    {
        var result = IncidentSeparation.Evaluate("scene: battle\ntrigger: hang\n", null);
        result.Window.Should().Be("trigger=hang。这只说明窗口有一段时间没有处理消息，不表示战斗结果和服务器不一致。");
    }

    [Fact]
    public void Unhandled_exception_trigger_is_not_a_window_hang()
    {
        var result = IncidentSeparation.Evaluate("trigger: unhandled-exception\n", null);
        result.Window.Should().Be("不是窗口卡死。材料里的触发原因是未处理异常。");
    }

    [Fact]
    public void Hang_wins_when_both_triggers_are_present()
    {
        var result = IncidentSeparation.Evaluate("trigger: unhandled-exception\ntrigger: hang\n", null);
        result.Window.Should().Contain("trigger=hang");
        result.Window.Should().NotContain("不是窗口卡死");
    }

    [Fact]
    public void Heartbeat_text_is_not_treated_as_a_hang()
    {
        var summary = "heartbeat written " + DateTimeOffset.UtcNow.AddSeconds(-30).ToString("o");
        IncidentSeparation.Evaluate(summary, null).Window.Should().Be(Missing);
    }

    [Fact]
    public void One_stamp_has_no_log_rate()
    {
        IncidentSeparation.Evaluate(null, "[14:00:00.000] hello").LogRate.Should().Be(Missing);
    }

    [Fact]
    public void Adjacent_one_second_gap_reports_median_without_claiming_a_logic_frame()
    {
        var result = IncidentSeparation.Evaluate(null, "[14:00:00.000] a\n[14:00:01.000] b\n");
        result.LogRate.Should().Be("相邻间隔中位数 1 秒，最大 1 秒。间隔不说明逻辑帧是否还在演算。");
    }

    [Fact]
    public void Sub_two_second_backward_jump_is_clock_noise()
    {
        var result = IncidentSeparation.Evaluate(null, "[14:00:01.000] a\n[14:00:00.500] b\n");
        result.LogRate.Should().Contain("中位数 0 秒");
        result.LogRate.Should().NotContain("0.5");
    }

    [Fact]
    public void Backward_jump_of_two_seconds_or_more_adds_a_day()
    {
        var result = IncidentSeparation.Evaluate(null, "[14:00:00.000] a\n[13:59:57.000] b\n");
        result.LogRate.Should().Contain("86397");
        result.LogRate.Should().NotContain("中位数 3");
        result.LogRate.Should().NotContain("最大 3");
    }

    [Fact]
    public void Exact_two_second_backward_jump_is_midnight_not_noise()
    {
        var result = IncidentSeparation.Evaluate(null, "[14:00:02.000] a\n[14:00:00.000] b\n");
        result.LogRate.Should().Contain("86398");
        result.LogRate.Should().NotContain("中位数 0 秒");
    }

    [Fact]
    public void Even_gap_count_median_averages_the_two_central_gaps()
    {
        var log = "[14:00:00.000] a\n[14:00:01.000] b\n[14:00:03.000] c\n";
        IncidentSeparation.Evaluate(null, log).LogRate.Should().Contain("中位数 1.5 秒");
    }

    [Fact]
    public void Stamps_stay_in_file_order_when_a_later_line_is_earlier_on_the_clock()
    {
        var sortedWouldBeThreeSeconds = "[14:00:00.000] first\n[13:59:57.000] second\n[14:00:00.000] third\n";
        var result = IncidentSeparation.Evaluate(null, sortedWouldBeThreeSeconds);
        result.LogRate.Should().Contain("86397");
        result.LogRate.Should().NotContain("最大 3 秒");
    }

    [Fact]
    public void Missing_method_and_reduce_life_names_the_method_not_a_mod()
    {
        var log = "MissingMethodException: FightActor.ReduceLife\n";
        var result = IncidentSeparation.Evaluate(null, log);
        result.LogicFrame.Should().Be("日志里同时出现 Exception 和 ReduceLife。这不指认是哪一个 Mod。");
        result.LogicFrame.Should().NotContain("封禁");
        result.LogicFrame.Should().NotContain("DamageRank");
        result.LogicFrame.Should().NotContain("BattleSuite");
    }

    [Fact]
    public void Exception_without_a_simulation_name_has_no_logic_frame_line()
    {
        IncidentSeparation.Evaluate(null, "MissingMethodException: SomeUi.OnClick\n").LogicFrame.Should().Be(Missing);
    }

    [Fact]
    public void Simulation_name_without_exception_has_no_logic_frame_line()
    {
        IncidentSeparation.Evaluate(null, "FightActor.ReduceLife returned\n").LogicFrame.Should().Be(Missing);
    }

    [Fact]
    public void Logic_frame_names_follow_the_search_list_not_file_order()
    {
        var log = "IsStateTimesUp failed\nException\nReduceLife\n";
        IncidentSeparation.Evaluate(null, log).LogicFrame.Should().Contain("ReduceLife、IsStateTimesUp");
    }

    [Fact]
    public void Null_melon_log_has_no_log_rate_or_logic_frame()
    {
        var result = IncidentSeparation.Evaluate("trigger: hang\n", null);
        result.LogRate.Should().Be(Missing);
        result.LogicFrame.Should().Be(Missing);
    }

    [Fact]
    public void Build_appends_three_missing_lines_between_blackbox_and_next_steps()
    {
        var text = DiagnosticsSummaryBuilder.Build(
            new DiagnosticsExportRequest
            {
                Paths = new PathsService(Path.GetTempPath()),
                AppVersion = "test"
            },
            Array.Empty<string>(),
            sessionTail: null,
            managerTail: null);

        var blackbox = text.IndexOf("## BlackBox", StringComparison.Ordinal);
        var section = text.IndexOf("## 卡死与逻辑帧", StringComparison.Ordinal);
        var next = text.IndexOf("## 建议下一步", StringComparison.Ordinal);
        blackbox.Should().BeGreaterThanOrEqualTo(0);
        section.Should().BeGreaterThan(blackbox);
        next.Should().BeGreaterThan(section);
        text.Should().Contain("- 窗口：这次材料没有");
        text.Should().Contain("- 日志：这次材料没有");
        text.Should().Contain("- 逻辑帧：这次材料没有");
    }

    [Fact]
    public void Build_passes_summary_and_melon_text_into_the_three_lines()
    {
        var text = DiagnosticsSummaryBuilder.Build(
            new DiagnosticsExportRequest
            {
                Paths = new PathsService(Path.GetTempPath()),
                AppVersion = "test"
            },
            Array.Empty<string>(),
            sessionTail: null,
            managerTail: null,
            blackboxSummaryText: "trigger: hang\n",
            melonLogText: "[14:00:00.000] a\n[14:00:01.000] b\nMissingMethodException FightActor.ReduceLife\n");

        text.Should().Contain("trigger=hang");
        text.Should().Contain("不表示战斗结果");
        text.Should().Contain("中位数 1 秒");
        text.Should().Contain("ReduceLife");
        text.Should().Contain("不指认");
    }

    [Fact]
    public void Export_reads_staging_copies_and_does_not_put_the_lines_into_diagnosis()
    {
        using var fx = new ExportFixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        Directory.CreateDirectory(Path.Combine(game, "MelonLoader"));
        File.WriteAllText(Path.Combine(box, "heartbeat.txt"), "age: 40\n");
        File.WriteAllText(Path.Combine(box, "summary.txt"), "trigger: hang\n");
        File.WriteAllText(
            Path.Combine(game, "MelonLoader", "Latest.log"),
            "[14:00:00.000] a\n[14:00:01.000] b\nMissingMethodException FightActor.ReduceLife\n");

        var zip = Path.Combine(fx.Root, "out.zip");
        var result = new DiagnosticsExportService().ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = game,
            SessionLogText = "session",
            AppVersion = "1.3.5",
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir),
            Diagnosis = new Diagnosis
            {
                Code = DiagnosisCodes.Healthy,
                Title = "未发现阻断问题",
                Action = "按管理器提示操作即可。"
            }
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var summary = ReadEntry(archive, "summary.md");
        var diagnosis = ReadEntry(archive, "diagnosis.json");
        summary.Should().Contain("## 卡死与逻辑帧");
        summary.Should().Contain("trigger=hang");
        summary.Should().Contain("中位数 1 秒");
        summary.Should().Contain("ReduceLife");
        var section = summary.IndexOf("## 卡死与逻辑帧", StringComparison.Ordinal);
        var next = summary.IndexOf("## 建议下一步", StringComparison.Ordinal);
        next.Should().BeGreaterThan(section);
        diagnosis.Should().Contain("\"code\": \"healthy\"");
        diagnosis.Should().NotContain("trigger=hang");
        diagnosis.Should().NotContain("不表示战斗结果");
        diagnosis.Should().NotContain("ReduceLife");
    }

    static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    sealed class ExportFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "inc-sep-" + Guid.NewGuid().ToString("N"));
        public PathsService Paths { get; }

        public ExportFixture()
        {
            Paths = new PathsService(Root);
            Paths.EnsureCreated();
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { /* ignore */ }
        }
    }
}
