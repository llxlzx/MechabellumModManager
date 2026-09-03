using System.Diagnostics;
using System.Globalization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public enum MailComposeKind
{
    Submit,
    Update,
    Report,
    Feedback
}

public readonly record struct MailComposePayload(string Subject, string Body, string MailtoUrl);

/// <summary>
/// Community catalog GitHub coordinates + email compose helpers for submit / update / report / feedback.
/// Authors do not need Fork/PR; maintainers review mail and publish to MechabellumMods.
/// Primary path: region-aware webmail + clipboard (mailto is unreliable when no default mail app).
/// </summary>
public static class GitHubCommunityLinks
{
    public const string Inbox = "llxmod@foxmail.com";

    /// <summary>QQ / Foxmail webmail home (CN-friendly).</summary>
    public static string DomesticWebMailUrl => "https://wx.mail.qq.com/";

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

    /// <summary>
    /// Prefer QQ webmail when the effective UI language is Chinese
    /// (includes Follow System when OS UI culture is zh*).
    /// </summary>
    public static bool PreferDomesticWebMail()
    {
        var culture = CultureInfo.CurrentUICulture;
        return culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
    }

    public static string TruncateForUrl(string? text, int maxBodyChars = 1200)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (text.Length <= maxBodyChars)
            return text;
        return text[..maxBodyChars] + "\n…(truncated)";
    }

    public static string BuildGmailComposeUrl(string subject, string body)
    {
        var b = TruncateForUrl(body, maxBodyChars: 1200);
        return "https://mail.google.com/mail/?view=cm&fs=1&tf=1"
            + "&to=" + Uri.EscapeDataString(Inbox)
            + "&su=" + Uri.EscapeDataString(subject ?? "")
            + "&body=" + Uri.EscapeDataString(b);
    }

    public static string BuildClipboardPackage(string subject, string body) =>
        $"To: {Inbox}\r\nSubject: {subject}\r\n\r\n{body}";

    /// <summary>
    /// 1) Copy clipboard package (always, via <paramref name="setClipboard"/>)
    /// 2) Domestic: open QQ webmail home; International: open Gmail compose (pre-filled)
    /// Does not rely on mailto as primary.
    /// </summary>
    public static (bool ok, string openedUrl, bool domestic) TryOpenCompose(
        string subject,
        string body,
        Action<string>? setClipboard)
    {
        var domestic = PreferDomesticWebMail();
        var package = BuildClipboardPackage(subject, body);
        try
        {
            setClipboard?.Invoke(package);
        }
        catch
        {
            // Clipboard may be locked; still attempt to open webmail.
        }

        var url = domestic
            ? DomesticWebMailUrl
            : BuildGmailComposeUrl(subject, body);

        var ok = TryShellOpen(url);
        return (ok, url, domestic);
    }

    /// <summary>
    /// Open region webmail for the inbox address and copy the address to clipboard.
    /// </summary>
    public static (bool ok, string openedUrl, bool domestic) TryOpenInboxWebMail(
        Action<string>? setClipboard = null)
    {
        try
        {
            setClipboard?.Invoke(Inbox);
        }
        catch
        {
            // ignore
        }

        var domestic = PreferDomesticWebMail();
        var url = domestic
            ? DomesticWebMailUrl
            : BuildGmailComposeUrl("", "");
        return (TryShellOpen(url), url, domestic);
    }

    public static string BuildMailto(string subject, string body) =>
        $"mailto:{Inbox}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

    public static MailComposePayload BuildSubmitCompose(string? modName = null)
    {
        var name = DisplayName(modName);
        var subject = $"[Mod投稿/Submit] {name}";
        var body = BuildSubmitBody(name);
        return new MailComposePayload(subject, body, BuildMailto(subject, body));
    }

    public static MailComposePayload BuildUpdateCompose(string? modName = null)
    {
        var name = DisplayName(modName);
        var subject = $"[Mod更新/Update] {name}";
        var body = BuildUpdateBody(name);
        return new MailComposePayload(subject, body, BuildMailto(subject, body));
    }

    public static MailComposePayload BuildReportCompose(
        string modId,
        string? modName,
        string? source,
        ReportCategory category,
        string? notes,
        string? appVersion)
    {
        var name = DisplayName(modName, fallback: modId);
        var subject = $"[Mod举报/Report] {name}";
        var body = BuildReportBody(modId, name, source, category, notes, appVersion);
        return new MailComposePayload(subject, body, BuildMailto(subject, body));
    }

    public static MailComposePayload BuildFeedbackCompose(string? shortTitle = null)
    {
        var title = string.IsNullOrWhiteSpace(shortTitle) ? "ShortTitle" : shortTitle.Trim();
        var subject = $"[管理器建议/Feedback] {title}";
        var body = BuildFeedbackBody(title);
        return new MailComposePayload(subject, body, BuildMailto(subject, body));
    }

    public static string BuildSubmitMailto(string? modName = null) =>
        BuildSubmitCompose(modName).MailtoUrl;

    public static string BuildUpdateMailto(string? modName = null) =>
        BuildUpdateCompose(modName).MailtoUrl;

    public static string BuildReportMailto(
        string modId,
        string? modName,
        string? source,
        ReportCategory category,
        string? notes,
        string? appVersion) =>
        BuildReportCompose(modId, modName, source, category, notes, appVersion).MailtoUrl;

    public static string BuildFeedbackMailto(string? shortTitle = null) =>
        BuildFeedbackCompose(shortTitle).MailtoUrl;

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

    static bool TryShellOpen(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
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
