using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Community catalog GitHub coordinates + email (mailto) helpers for submit / update / report / feedback.
/// Authors do not need Fork/PR; maintainers review mail and publish to MechabellumMods.
/// </summary>
public static class GitHubCommunityLinks
{
    public const string Inbox = "llxmod@foxmail.com";

    public static string Owner => ModCatalogService.Owner;
    public static string Repo => ModCatalogService.Repo;

    public static string RepositoryUrl => $"https://github.com/{Owner}/{Repo}";

    /// <summary>
    /// README email contribute / report section (anchor created on MechabellumMods).
    /// </summary>
    public static string ContributeGuideUrl =>
        $"{RepositoryUrl}#投稿与举报--submit--report";

    /// <summary>Static bilingual mailto helper page on the mods repo.</summary>
    public static string SubmitPageUrl =>
        $"{RepositoryUrl}/blob/main/docs/submit.html";

    public static string BuildMailto(string subject, string body) =>
        $"mailto:{Inbox}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

    public static string BuildSubmitMailto(string? modName = null)
    {
        var name = DisplayName(modName);
        var subject = $"[Mod投稿/Submit] {name}";
        return BuildMailto(subject, BuildSubmitBody(name));
    }

    public static string BuildUpdateMailto(string? modName = null)
    {
        var name = DisplayName(modName);
        var subject = $"[Mod更新/Update] {name}";
        return BuildMailto(subject, BuildUpdateBody(name));
    }

    public static string BuildReportMailto(
        string modId,
        string? modName,
        string? source,
        ReportCategory category,
        string? notes,
        string? appVersion)
    {
        var name = DisplayName(modName, fallback: modId);
        var subject = $"[Mod举报/Report] {name}";
        return BuildMailto(subject, BuildReportBody(modId, name, source, category, notes, appVersion));
    }

    public static string BuildFeedbackMailto(string? shortTitle = null)
    {
        var title = string.IsNullOrWhiteSpace(shortTitle) ? "ShortTitle" : shortTitle.Trim();
        var subject = $"[管理器建议/Feedback] {title}";
        return BuildMailto(subject, BuildFeedbackBody(title));
    }

    public static string BuildSubmitBody(string? modName = null)
    {
        var name = DisplayName(modName);
        return
            "【类型 / Type】投稿 / Submit（新 Mod）\n" +
            $"【Mod 名称 / Name】{name}\n" +
            "【作者 / Author】\n" +
            "【版本 / Version】\n" +
            "【一句话简介 / Summary】\n" +
            "【游戏端 / Game】正式服 / 测试服 / 两者（Official / Test / Both）\n" +
            "【联系方式 / Contact】（可选）\n" +
            "【备注 / Notes】\n";
    }

    public static string BuildUpdateBody(string? modName = null)
    {
        var name = DisplayName(modName);
        return
            "【类型 / Type】更新 / Update\n" +
            $"【Mod 名称 / Name】{name}\n" +
            "【作者 / Author】\n" +
            "【原版本 → 新版本 / Version】\n" +
            "【更新说明 / Changelog】\n" +
            "【联系方式 / Contact】（可选）\n";
    }

    public static string BuildReportBody(
        string modId,
        string? modName,
        string? source,
        ReportCategory category,
        string? notes,
        string? appVersion)
    {
        var name = DisplayName(modName, fallback: modId);
        var safeNotes = TruncateNotes(notes);
        return
            "【类型 / Type】举报 / Report\n" +
            $"【Mod 名称 / Name】{name}\n" +
            $"【Mod Id】{modId}\n" +
            $"【来源 / Source】{FormatSource(source)}\n" +
            $"【类别 / Category】{FormatCategory(category)}\n" +
            $"【说明 / Details】{safeNotes}\n" +
            $"【管理器版本 / App】{appVersion ?? ""}\n";
    }

    public static string BuildFeedbackBody(string? title = null)
    {
        var t = string.IsNullOrWhiteSpace(title) ? "" : title.Trim();
        return
            "【类型 / Type】管理器建议 / Feedback\n" +
            $"【标题 / Title】{t}\n" +
            "【详细说明 / Details】\n" +
            "【管理器版本 / App】（可选）\n" +
            "【联系方式 / Contact】（可选）\n";
    }

    static string DisplayName(string? modName, string fallback = "ModName") =>
        string.IsNullOrWhiteSpace(modName) ? fallback : modName.Trim();

    static string FormatSource(string? source)
    {
        if (string.Equals(source, "library", StringComparison.OrdinalIgnoreCase))
            return "本地库（Library）";
        if (string.Equals(source, "catalog", StringComparison.OrdinalIgnoreCase))
            return "社区目录（Catalog）";
        return string.IsNullOrWhiteSpace(source) ? "" : source.Trim();
    }

    static string FormatCategory(ReportCategory category) => category switch
    {
        ReportCategory.Cheat => "作弊相关 / Cheat",
        ReportCategory.Virus => "病毒或恶意 / Malware",
        ReportCategory.Unrelated => "与游戏无关 / Unrelated",
        _ => "其他 / Other"
    };

    static string TruncateNotes(string? notes)
    {
        if (string.IsNullOrEmpty(notes))
            return "";
        if (notes.Length <= ReportRequest.MaxNotesLength)
            return notes;
        return notes[..ReportRequest.MaxNotesLength] + "…(truncated)";
    }
}
