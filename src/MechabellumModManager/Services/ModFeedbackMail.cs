namespace MechabellumModManager.Services;

public readonly record struct ModFeedbackMailPayload(
    string To,
    string Subject,
    string Body,
    string GmailUrl,
    string MailtoUrl,
    string QqClipboard);

public static class ModFeedbackMail
{
    public const string QqWebMail = "https://wx.mail.qq.com/";

    public static ModFeedbackMailPayload Build(string author, string visibleName, string? version)
    {
        var versionText = string.IsNullOrWhiteSpace(version) ? "" : version.Trim();
        var subject = string.IsNullOrEmpty(versionText)
            ? $"[Mod 反馈 / Feedback] {visibleName}"
            : $"[Mod 反馈 / Feedback] {visibleName} {versionText}";
        var body = $"【Mod】{visibleName}\n【版本 / Version】{versionText}\n【问题 / Issue】\n";

        return new ModFeedbackMailPayload(
            author,
            subject,
            body,
            BuildGmailUrl(author, subject, body),
            BuildMailtoUrl(author, subject, body),
            BuildQqClipboard(author, subject, body));
    }

    static string BuildGmailUrl(string author, string subject, string body)
    {
        var b = GitHubCommunityLinks.TruncateForUrl(body, maxBodyChars: 1200);
        return "https://mail.google.com/mail/?view=cm&fs=1&tf=1"
               + "&to=" + Uri.EscapeDataString(author)
               + "&su=" + Uri.EscapeDataString(subject ?? "")
               + "&body=" + Uri.EscapeDataString(b);
    }

    static string BuildMailtoUrl(string author, string subject, string body) =>
        $"mailto:{author}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

    static string BuildQqClipboard(string author, string subject, string body) =>
        $"To: {author}\r\nSubject: {subject}\r\n\r\n{body}";
}
