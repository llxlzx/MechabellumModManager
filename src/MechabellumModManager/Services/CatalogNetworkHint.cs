namespace MechabellumModManager.Services;

public static class CatalogNetworkHint
{
    public static string Format(string error, string? mirrorBaseUrl, bool usedCache)
    {
        var head = usedCache
            ? string.Format(LocalizationService.T("CatalogFetchFailedUsedCache"), error ?? "")
            : string.Format(LocalizationService.T("CatalogFetchFailed"), error ?? "");
        var hint = string.IsNullOrWhiteSpace(mirrorBaseUrl)
            ? LocalizationService.T("CatalogFetchHintNoMirror")
            : LocalizationService.T("CatalogFetchHintMirrorFailed");
        return $"{head} {hint}";
    }
}
