using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Pages the manager opens in a browser. GitHub pages often do not load in mainland China, so the
/// China edition opens copies that sync-mirror publishes to the domestic mirror. A player who chose
/// "always GitHub" or turned the mirror off gets GitHub.
/// </summary>
public static class EditionLinks
{
    public const string SubmitPagePath = "MechabellumMods/docs/submit.html";
    public const string DownloadPagePath = "MechabellumModManager/download.html";

    public static string ContributeGuide(AppEdition edition, string? mirrorBaseUrl, DownloadRouteKind route) =>
        MirrorPage(edition, mirrorBaseUrl, route, SubmitPagePath) ?? GitHubCommunityLinks.ContributeGuideUrl;

    public static string ReleasesPage(AppEdition edition, string? mirrorBaseUrl, DownloadRouteKind route) =>
        MirrorPage(edition, mirrorBaseUrl, route, DownloadPagePath) ?? UpdateChecker.DefaultBrowseUrl;

    static string? MirrorPage(AppEdition edition, string? mirrorBaseUrl, DownloadRouteKind route, string path)
    {
        if (edition != AppEdition.Cn || route == DownloadRouteKind.GitHubOnly)
            return null;
        if (!ExternalUrlPolicy.IsHttpsUrl(mirrorBaseUrl))
            return null;
        return RemoteFetch.TryMirrorUri(mirrorBaseUrl, path)?.AbsoluteUri;
    }
}
