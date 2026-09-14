using System.Globalization;

namespace MechabellumModManager.Services;

/// <summary>
/// Built-in mainland-China COS root used as the factory default only when the OS region is China
/// (<c>CN</c>). An explicit empty <c>AppConfig.MirrorBaseUrl</c> means the player opted out (GitHub
/// only) and must not be refilled. Overseas regions default to GitHub so they do not draw on the
/// domestic mirror's egress.
/// </summary>
public static class DomesticMirrorDefaults
{
    public const string BaseUrl =
        "https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com";

    /// <summary>
    /// True when <paramref name="region"/> is mainland China. Defaults to
    /// <see cref="RegionInfo.CurrentRegion"/> (Windows region, not UI language).
    /// </summary>
    public static bool IsMainlandChinaRegion(RegionInfo? region = null)
    {
        region ??= RegionInfo.CurrentRegion;
        return string.Equals(region.TwoLetterISORegionName, "CN", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Factory fill for a never-configured mirror: COS in mainland China, empty (GitHub-only) elsewhere.
    /// Persist the empty string so the next launch does not treat it as null again.
    /// </summary>
    public static string ResolveFactoryMirrorBaseUrl(RegionInfo? region = null) =>
        IsMainlandChinaRegion(region) ? BaseUrl : "";

    public static bool IsBuiltInCosUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        return string.Equals(
            url.Trim().TrimEnd('/'),
            BaseUrl.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
