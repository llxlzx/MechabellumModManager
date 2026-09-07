using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

/// <summary>
/// Bundled MelonLoader redist is 0.7.3; older installs (e.g. 0.7.1) should be upgraded.
/// </summary>
public static class MelonLoaderVersionGate
{
    public static readonly Version BundledMinimum = new(0, 7, 3);

    static readonly Regex LatestLogVersion = new(
        @"MelonLoader\s+v(?<ver>\d+\.\d+(?:\.\d+){0,2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// True when a parseable installed FileVersion is strictly below the bundled minimum
    /// (including 0.0.0.0). Unreadable/empty strings return false — use
    /// <see cref="ShouldForceUpgradeStore"/> when Latest.log or a missing FileVersion
    /// should force an upgrade.
    /// </summary>
    public static bool ShouldUpgradeInstalled(string? fileVersion)
    {
        if (!TryParseVersion(fileVersion, out var installed))
            return false;

        return installed < BundledMinimum;
    }

    /// <summary>
    /// Upgrade when FileVersion is empty/0.0.0.0/below bundled, or Latest.log reports
    /// a Loader below the bundled minimum. A readable FileVersion at or above bundled
    /// wins over a stale Latest.log so a just-upgraded store is not re-extracted.
    /// </summary>
    public static bool ShouldForceUpgradeStore(string? gamePath, string? fileVersion)
    {
        if (TryParseVersion(fileVersion, out var installed))
            return installed < BundledMinimum;

        if (string.IsNullOrWhiteSpace(gamePath))
            return IsMissingOrZero(fileVersion);

        var logVersion = TryReadVersionFromLatestLog(gamePath);
        if (TryParseVersion(logVersion, out var fromLog))
            return fromLog < BundledMinimum;

        // Empty FileVersion without a log banner: only force when a Loader dll is present
        // (0.7.1 with blank VERSIONINFO). Fixture/partial dirs without a dll are not upgrades.
        return HasMelonLoaderDll(gamePath);
    }

    public static bool ShouldUpgradeFromGamePath(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;
        return ShouldForceUpgradeStore(gamePath, TryReadInstalledVersion(gamePath));
    }

    public static bool HasMelonLoaderDll(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;
        var melonRoot = Path.Combine(gamePath, "MelonLoader");
        foreach (var candidate in MelonDllCandidates(melonRoot))
        {
            if (File.Exists(candidate))
                return true;
        }

        return false;
    }

    public static string? TryReadInstalledVersion(string gamePath)
    {
        var melonRoot = Path.Combine(gamePath, "MelonLoader");
        foreach (var candidate in MelonDllCandidates(melonRoot))
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                return FileVersionInfo.GetVersionInfo(candidate).FileVersion;
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    public static string? TryReadVersionFromLatestLog(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return null;

        var logPath = Path.Combine(gamePath, "MelonLoader", "Latest.log");
        if (!File.Exists(logPath))
            return null;

        try
        {
            var text = File.ReadAllText(logPath);
            var match = LatestLogVersion.Match(text);
            return match.Success ? match.Groups["ver"].Value : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// FileVersion when readable and non-zero; otherwise the Latest.log banner version.
    /// </summary>
    public static string? TryReadDisplayVersion(string gamePath)
    {
        var fileVersion = TryReadInstalledVersion(gamePath);
        if (TryParseVersion(fileVersion, out var parsed) && !IsZeroVersion(parsed))
            return NormalizeDisplay(fileVersion);

        var fromLog = TryReadVersionFromLatestLog(gamePath);
        return string.IsNullOrWhiteSpace(fromLog) ? fileVersion : fromLog;
    }

    static IEnumerable<string> MelonDllCandidates(string melonRoot)
    {
        yield return Path.Combine(melonRoot, "net6", "MelonLoader.dll");
        yield return Path.Combine(melonRoot, "net472", "MelonLoader.dll");
        yield return Path.Combine(melonRoot, "net35", "MelonLoader.dll");
        yield return Path.Combine(melonRoot, "MelonLoader.dll");
    }

    public static string FormatBundledMinimum() =>
        $"{BundledMinimum.Major}.{BundledMinimum.Minor}.{BundledMinimum.Build}";

    static bool IsMissingOrZero(string? fileVersion)
    {
        if (string.IsNullOrWhiteSpace(fileVersion))
            return true;
        return TryParseVersion(fileVersion, out var v) && IsZeroVersion(v);
    }

    static bool IsZeroVersion(Version v) =>
        v.Major == 0 && v.Minor == 0 && (v.Build <= 0) && (v.Revision <= 0);

    static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        var plus = normalized.IndexOf('+');
        if (plus >= 0)
            normalized = normalized[..plus];

        if (!Version.TryParse(normalized, out var parsed))
            return false;
        version = parsed;
        return true;
    }

    static string? NormalizeDisplay(string? fileVersion)
    {
        if (string.IsNullOrWhiteSpace(fileVersion))
            return fileVersion;
        var trimmed = fileVersion.Trim();
        var plus = trimmed.IndexOf('+');
        return plus >= 0 ? trimmed[..plus] : trimmed;
    }
}
