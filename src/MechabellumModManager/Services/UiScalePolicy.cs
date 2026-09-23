using System.Globalization;

namespace MechabellumModManager.Services;

/// <summary>
/// Extra layout scale on top of Windows DPI. Auto divides the screen's short side by the
/// Windows scale, compares that logical size with a 1080p baseline, and snaps to a step.
/// 80% is manual only, so auto never shrinks a small screen further. The top step is 250%.
/// </summary>
public static class UiScalePolicy
{
    public const string Auto = "auto";
    public const double ReferenceShortSide = 1080;

    public static readonly string[] ExplicitCodes =
        ["0.8", "1", "1.1", "1.25", "1.5", "1.75", "2", "2.25", "2.5"];

    static readonly double[] AutoSteps = [1, 1.1, 1.25, 1.5, 1.75, 2, 2.25, 2.5];

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

        var dpi = systemDpiScale < 0.5 ? 1 : systemDpiScale;
        var shortSide = screenWidthPixels > 0
            ? Math.Min(screenWidthPixels, screenHeightPixels)
            : screenHeightPixels;
        if (shortSide <= 0)
            return 1;

        var ratio = shortSide / dpi / ReferenceShortSide;
        return SnapAuto(ratio);
    }

    public static string Format(double scale) =>
        scale.ToString("0.##", CultureInfo.InvariantCulture);

    public static string PercentText(double scale) =>
        Math.Round(scale * 100).ToString("0", CultureInfo.InvariantCulture);

    static double SnapAuto(double ratio)
    {
        var best = AutoSteps[0];
        var bestDistance = Math.Abs(ratio - best);
        for (var i = 1; i < AutoSteps.Length; i++)
        {
            var distance = Math.Abs(ratio - AutoSteps[i]);
            if (distance < bestDistance)
            {
                best = AutoSteps[i];
                bestDistance = distance;
            }
        }

        return best;
    }

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
