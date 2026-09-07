using System.Text;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

/// <summary>
/// Parses manager log lines into a compact timeline for diagnostics packs.
/// </summary>
public static class DiagnosticsTimelineBuilder
{
    static readonly Regex LineRe = new(
        @"^\[(?<t>\d{2}:\d{2}:\d{2})\]\s*(?<msg>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static readonly (string Needle, string Kind)[] Kinds =
    [
        ("双目录向导已完成", "wizard_await_steam"),
        ("正在等待 Steam 完成分支切换", "wizard_await_steam"),
        ("已解除正式服/测试服双目录配置", "teardown_done"),
        ("解除双服", "teardown"),
        ("正在生成 MelonLoader 程序集", "melon_gen_start"),
        ("等待 MelonLoader 生成", "melon_gen_wait"),
        ("程序集已生成", "melon_gen_ok"),
        ("等待程序集生成超时", "melon_gen_timeout"),
        ("部署成功", "deploy_ok"),
        ("已保存", "acf_snapshot"),
        ("结算后未保存 ACF", "acf_snapshot_skip"),
        ("未找到有效的 Mechabellum", "game_missing"),
        ("已导出诊断包", "diag_export"),
        ("切服失败", "branch_switch_fail"),
        ("Loader 未在本次启动注入", "loader_not_injected"),
        ("游戏与 MelonLoader 已就绪", "game_ready")
    ];

    public static string BuildJsonl(params string?[] logTexts) =>
        MergeEventsAndLogs(eventsJsonl: null, logTexts);

    public static string MergeEventsAndLogs(string? eventsJsonl, params string?[] logTexts)
    {
        var sb = new StringBuilder();
        AppendStructuredEvents(sb, eventsJsonl);
        foreach (var text in logTexts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Length == 0)
                    continue;
                var m = LineRe.Match(line);
                if (!m.Success)
                    continue;
                var msg = m.Groups["msg"].Value;
                string? kind = null;
                foreach (var (needle, k) in Kinds)
                {
                    if (msg.Contains(needle, StringComparison.Ordinal))
                    {
                        kind = k;
                        break;
                    }
                }

                if (kind is null)
                    continue;

                var safe = msg.Replace('\\', '/').Replace("\"", "'");
                if (safe.Length > 160)
                    safe = safe[..160] + "…";
                sb.Append("{\"t\":\"").Append(m.Groups["t"].Value)
                    .Append("\",\"kind\":\"").Append(kind)
                    .Append("\",\"msg\":\"").Append(safe).Append("\"}\n");
            }
        }

        return sb.ToString();
    }

    static void AppendStructuredEvents(StringBuilder sb, string? eventsJsonl)
    {
        if (string.IsNullOrWhiteSpace(eventsJsonl))
            return;

        foreach (var raw in eventsJsonl.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line[0] != '{')
                continue;
            sb.Append(line);
            if (line[^1] != '\n')
                sb.Append('\n');
        }
    }
}
