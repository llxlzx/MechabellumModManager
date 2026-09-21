using System.Globalization;

namespace MechabellumModManager.Services;

/// <summary>
/// Extra layout scale on top of Windows DPI. Auto stays at 100% when the system is already
/// scaled up. When Windows is left near 100%, the short side of the screen picks a step:
/// 1080p stays 100%, 1440p and short ultrawides use 125%, 1800p and 4K use 150%, 5K uses 175%,
/// and 8K-class screens use 200%.
/// </summary>
public static class UiScalePolicy
{
    public const string Auto = "auto";

    public static readonly string[] ExplicitCodes = ["1", "1.25", "1.5", "1.75", "2"];

    public static string Normalize(string? configured)
    {
        if (TryParseExplicit(configured, out var scale))
            return Format(scale);
        return Auto;
    }

    /// <summary>
    /// <paramref name="systemDpiScale"/> is 1 at 96 DPI. Width and height are physical pixels of the
    /// screen the window is on. Width may be 0 when only the height is known.
    /// </summary>
    public static double Resolve(
        string? configured,
        double systemDpiScale,
        int screenHeightPixels,
        int screenWidthPixels = 0)
    {
        if (TryParseExplicit(configured, out var explicitScale))
            return explicitScale;

        if (systemDpiScale >= 1.25)
            return 1;

        var shortSide = screenWidthPixels > 0
            ? Math.Min(screenWidthPixels, screenHeightPixels)
            : screenHeightPixels;

        if (shortSide >= 3200)
            return 2;
        if (shortSide >= 2880)
            return 1.75;
        if (shortSide >= 1800)
            return 1.5;
        if (shortSide >= 1440)
            return 1.25;

        return 1;
    }

    public static string Format(double scale) =>
        scale.ToString("0.##", CultureInfo.InvariantCulture);

    static bool TryParseExplicit(string? configured, out double scale)
    {
        scale = 0;
        if (string.IsNullOrWhiteSpace(configured))
            return false;
        if (string.Equals(configured.Trim(), Auto, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!double.TryParse(configured.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;

        foreach (var code in ExplicitCodes)
        {
            var allowed = double.Parse(code, CultureInfo.InvariantCulture);
            if (Math.Abs(parsed - allowed) < 0.001)
            {
                scale = allowed;
                return true;
            }
        }

        return false;
    }
}
