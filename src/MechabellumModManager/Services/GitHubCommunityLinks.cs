using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Builds browser URLs so authors/reporters use the MechabellumMods GitHub repo directly
/// (no Cloudflare relay).
/// </summary>
public static class GitHubCommunityLinks
{
    public const string ReportIssueTemplate = "mod_report.md";
    public const string ReportLabel = "report";

    public static string Owner => ModCatalogService.Owner;
    public static string Repo => ModCatalogService.Repo;

    public static string RepositoryUrl => $"https://github.com/{Owner}/{Repo}";

    /// <summary>
    /// Repo root README documents Fork + Pull Request. Avoid fragile heading anchors.
    /// </summary>
    public static string ContributeGuideUrl => RepositoryUrl;

    public static string BuildReportIssueUrl(
        string modId,
        string? modName,
        string? source,
        ReportCategory category,
        string? notes,
        string? appVersion)
    {
        var cat = category.ToString().ToLowerInvariant();
        var display = string.IsNullOrWhiteSpace(modName) ? modId : modName.Trim();
        var title = $"[Report][{cat}] {display}";
        var safeNotes = TruncateNotes(notes);
        var body =
            "## Report\n" +
            $"- Mod ID: `{modId}`\n" +
            $"- Mod name: {display}\n" +
            $"- Source: {source ?? ""}\n" +
            $"- Category: {cat}\n" +
            $"- App version: {appVersion ?? ""}\n" +
            $"- Notes: {safeNotes}\n\n" +
            "(Opened from Mechabellum Mod Manager — please sign in and Submit on GitHub.)\n";

        // title+body pre-fill for the manager; labels + template for repo triage / chooser.
        return $"https://github.com/{Owner}/{Repo}/issues/new" +
               $"?template={Uri.EscapeDataString(ReportIssueTemplate)}" +
               $"&title={Uri.EscapeDataString(title)}" +
               $"&body={Uri.EscapeDataString(body)}" +
               $"&labels={Uri.EscapeDataString(ReportLabel)}";
    }

    static string TruncateNotes(string? notes)
    {
        if (string.IsNullOrEmpty(notes))
            return "";
        if (notes.Length <= ReportRequest.MaxNotesLength)
            return notes;
        return notes[..ReportRequest.MaxNotesLength] + "…(truncated)";
    }
}
