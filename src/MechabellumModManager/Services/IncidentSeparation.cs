using System.Globalization;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public readonly record struct IncidentSeparationResult(string Window, string LogRate, string LogicFrame);

/// <summary>
/// Three read-only lines for the diagnostics zip. They do not grade mods or change diagnosis codes.
/// </summary>
public static class IncidentSeparation
{
    const string Missing = "这次材料没有";

    static readonly Regex Stamp = new(@"\[(\d{2}:\d{2}:\d{2}\.\d{3})\]", RegexOptions.CultureInvariant);

    static readonly string[] SimulationNames =
    [
        "ReduceLife",
        "DamagePerformer",
        "AddSupply",
        "ReduceSupply",
        "SetSupply",
        "OnFightEnd",
        "OnFightOver",
        "GetMaxStateTime",
        "GetDeployTime",
        "IsStateTimesUp"
    ];

    public static IncidentSeparationResult Evaluate(string? summaryText, string? melonLogText) =>
        new(WindowLine(summaryText), LogRateLine(melonLogText), LogicFrameLine(melonLogText));

    static string WindowLine(string? summaryText)
    {
        if (string.IsNullOrEmpty(summaryText))
            return Missing;

        var hang = false;
        var unhandled = false;
        foreach (var raw in summaryText.Split('\n'))
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;
            if (line.Contains("trigger: hang", StringComparison.Ordinal))
                hang = true;
            if (line.Contains("trigger: unhandled-exception", StringComparison.Ordinal))
                unhandled = true;
        }

        if (hang)
            return "trigger=hang。这只说明窗口有一段时间没有处理消息，不表示战斗结果和服务器不一致。";
        if (unhandled)
            return "不是窗口卡死。材料里的触发原因是未处理异常。";
        return Missing;
    }

    static string LogRateLine(string? melonLogText)
    {
        if (melonLogText is null)
            return Missing;

        var stamps = new List<decimal>();
        foreach (Match match in Stamp.Matches(melonLogText))
        {
            if (TryParseClock(match.Groups[1].Value, out var seconds))
                stamps.Add(seconds);
        }

        if (stamps.Count < 2)
            return Missing;

        // Adjacent gaps stay in file order. Sorting the stamps would hide a real backward jump.
        var gaps = new List<decimal>(stamps.Count - 1);
        for (var i = 0; i < stamps.Count - 1; i++)
        {
            var gap = stamps[i + 1] - stamps[i];
            if (gap < 0)
            {
                if (-gap < 2m)
                    gap = 0m;
                else
                    gap += 86400m;
            }

            gaps.Add(gap);
        }

        var ordered = gaps.OrderBy(g => g).ToArray();
        var mid = ordered.Length / 2;
        var median = ordered.Length % 2 == 1
            ? ordered[mid]
            : (ordered[mid - 1] + ordered[mid]) / 2m;
        var max = gaps.Max();
        return $"相邻间隔中位数 {FormatSeconds(median)} 秒，最大 {FormatSeconds(max)} 秒。间隔不说明逻辑帧是否还在演算。";
    }

    static string LogicFrameLine(string? melonLogText)
    {
        if (melonLogText is null)
            return Missing;
        if (melonLogText.IndexOf("Exception", StringComparison.Ordinal) < 0)
            return Missing;

        var hits = new List<string>();
        foreach (var name in SimulationNames)
        {
            if (melonLogText.IndexOf(name, StringComparison.Ordinal) >= 0)
                hits.Add(name);
        }

        if (hits.Count == 0)
            return Missing;
        return $"日志里同时出现 Exception 和 {string.Join("、", hits)}。这不指认是哪一个 Mod。";
    }

    static bool TryParseClock(string text, out decimal seconds)
    {
        seconds = 0m;
        var first = text.IndexOf(':');
        var second = text.IndexOf(':', first + 1);
        var dot = text.IndexOf('.', second + 1);
        if (first < 0 || second < 0 || dot < 0)
            return false;
        if (!int.TryParse(text[..first], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(text[(first + 1)..second], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(text[(second + 1)..dot], NumberStyles.None, CultureInfo.InvariantCulture, out var secs)
            || !int.TryParse(text[(dot + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var millis))
            return false;
        seconds = hours * 3600m + minutes * 60m + secs + millis / 1000m;
        return true;
    }

    static string FormatSeconds(decimal value)
    {
        var rounded = decimal.Round(value, 3, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
