using System.Text;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Builds human-readable diagnostics summary so testers only need to send the zip.
/// </summary>
public static class DiagnosticsSummaryBuilder
{
    static readonly Dictionary<string, string> FindingZh = new(StringComparer.OrdinalIgnoreCase)
    {
        ["game_missing"] = "当前游戏路径缺少 Mechabellum.exe / GameAssembly.dll",
        ["melon_log_missing"] = "未找到 MelonLoader Latest.log",
        ["melon_log_stale_or_incomplete"] = "Melon 日志过旧或不完整",
        ["melon_proxy_dll_missing"] = "缺少 version.dll / winhttp.dll",
        ["link_incomplete_but_store_valid"] = "Steam 路径不完整，但独立仓目录看起来有游戏",
        ["active_store_incomplete"] = "当前激活仓不完整",
        ["acf_not_settled"] = "Steam 清单未结算完成",
        ["acf_update_unhealthy"] = "Steam 更新状态异常（可能更新失败/卡住）",
        ["awaiting_steam_settle"] = "管理器正在等待 Steam 结算",
        ["orphan_dual_layout"] = "检测到孤儿双服布局",
        ["leftover_dual_store_folders"] = "仍有残留双服目录",
        ["junction_missing_while_branch_enabled"] = "双服已启用但缺少目录联接",
        ["game_path_is_branch_store"] = "游戏路径落在 _official/_beta 仓目录"
    };

    public static string Build(
        DiagnosticsExportRequest request,
        IReadOnlyList<string> findings,
        string? sessionTail,
        string? managerTail,
        string? authoritativeGameStatusKind = null,
        Diagnosis? diagnosis = null)
    {
        var sb = new StringBuilder();
        if (diagnosis is not null)
            sb.AppendLine($"# {diagnosis.Title}");
        else
            sb.AppendLine("# Mechabellum Mod Manager — 诊断摘要");
        sb.AppendLine();
        if (diagnosis is not null)
        {
            sb.AppendLine($"- 判定：`{diagnosis.Code}`");
            if (diagnosis.Evidence.Count > 0)
                sb.AppendLine("- 证据：" + string.Join("；", diagnosis.Evidence));
            if (diagnosis.RuledOut.Count > 0)
                sb.AppendLine("- 已排除：" + string.Join("、", diagnosis.RuledOut));
            if (diagnosis.Also.Count > 0)
                sb.AppendLine("- 附注：" + string.Join("、", diagnosis.Also));
            if (!string.IsNullOrWhiteSpace(diagnosis.Action))
                sb.AppendLine($"- 建议：{diagnosis.Action}");
            sb.AppendLine();
        }
        sb.AppendLine($"- 应用版本：{request.AppVersion}");
        sb.AppendLine($"- 导出时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"- 向导步骤：{request.BranchWizardStep ?? "(无)"}");
        sb.AppendLine($"- 当前分支：{request.ActiveGameBranch ?? "(无)"}");
        sb.AppendLine($"- 双服启用：{request.BranchSwitchEnabled}");
        sb.AppendLine($"- 等待 Steam 结算：{request.IsAwaitingSteamSettle}");
        var statusKind = !string.IsNullOrWhiteSpace(authoritativeGameStatusKind)
            ? authoritativeGameStatusKind
            : request.GameStatusKind;
        sb.AppendLine($"- 游戏状态：{statusKind ?? "(无)"}");
        if (!string.IsNullOrWhiteSpace(request.GameStatusMessage))
            sb.AppendLine($"- 状态说明：{request.GameStatusMessage}");
        sb.AppendLine();

        sb.AppendLine("## Findings");
        if (findings.Count == 0)
            sb.AppendLine("- （无）");
        else
        {
            foreach (var f in findings)
            {
                var zh = FindingZh.TryGetValue(f, out var t) ? t : f;
                sb.AppendLine($"- `{f}` — {zh}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 建议下一步");
        foreach (var tip in BuildTips(findings, request))
            sb.AppendLine($"- {tip}");

        sb.AppendLine();
        sb.AppendLine("## 日志尾部（session）");
        sb.AppendLine("```");
        sb.AppendLine(TrimTail(sessionTail, 40));
        sb.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(managerTail))
        {
            sb.AppendLine();
            sb.AppendLine("## 日志尾部（manager 当日）");
            sb.AppendLine("```");
            sb.AppendLine(TrimTail(managerTail, 40));
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("把本 zip 整包发给维护者即可；优先阅读本 summary.md。");
        return sb.ToString();
    }

    public static IReadOnlyList<string> BuildTips(IReadOnlyList<string> findings, DiagnosticsExportRequest request)
    {
        var tips = new List<string>();
        var set = new HashSet<string>(findings, StringComparer.OrdinalIgnoreCase);

        if (set.Contains("acf_update_unhealthy") || set.Contains("acf_not_settled") || set.Contains("awaiting_steam_settle"))
        {
            tips.Add("先在 Steam 把游戏更新到可「开始游戏」状态（勿停在更新失败）；再完全退出 Steam，回到管理器点结算/继续。");
        }

        if (set.Contains("link_incomplete_but_store_valid") || set.Contains("active_store_incomplete") || set.Contains("game_missing"))
        {
            tips.Add("Steam 路径当前不完整：勿反复点继续；等 Steam 下载/校验完成，或验证游戏文件。");
        }

        if (set.Contains("orphan_dual_layout") || set.Contains("leftover_dual_store_folders"))
        {
            tips.Add("存在双服残留：可在设置页使用「急救恢复单目录」。");
        }

        if (string.Equals(request.ActiveGameBranch, "Beta", StringComparison.OrdinalIgnoreCase)
            && request.BranchSwitchEnabled)
        {
            tips.Add("当前为测试服双目录：急救前建议先切到正式服，避免 Steam 内容文件损毁。");
        }

        if (tips.Count == 0)
            tips.Add("按管理器提示操作；若仍失败，保持本 zip 原文发送即可。");

        return tips.Take(3).ToList();
    }

    static string TrimTail(string? text, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(空)";
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length <= maxLines)
            return string.Join("\n", lines);
        return string.Join("\n", lines.Skip(lines.Length - maxLines));
    }

    public static IReadOnlyList<string> ExtractFindings(Dictionary<string, object?> env)
    {
        if (!env.TryGetValue("findings", out var raw) || raw is null)
            return Array.Empty<string>();

        if (raw is List<string> list)
            return list;
        if (raw is IEnumerable<object> objs)
            return objs.Select(o => o?.ToString() ?? "").Where(s => s.Length > 0).ToList();
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var acc = new List<string>();
            foreach (var el in je.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                    acc.Add(el.GetString() ?? "");
            }
            return acc;
        }

        return Array.Empty<string>();
    }
}
