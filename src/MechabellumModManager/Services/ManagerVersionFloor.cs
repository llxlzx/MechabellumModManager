using System.Globalization;

namespace MechabellumModManager.Services;

/// <summary>
/// Compares a catalog <c>minManagerVersion</c> with the running manager.
/// Catalog values must be exactly three integer segments. The local version may carry a
/// leading v, a +build suffix, or a fourth numeric segment; only the first three count.
/// </summary>
public static class ManagerVersionFloor
{
    public readonly record struct Decision(bool Allowed, bool Unreadable, string? Required, string? Local);

    public static Decision Evaluate(string? minManagerVersion, string? localVersion)
    {
        var localText = CanonicalLocal(localVersion) ?? localVersion?.Trim();
        if (string.IsNullOrWhiteSpace(minManagerVersion))
            return new Decision(true, false, null, localText);

        var raw = minManagerVersion.Trim();
        if (!TryCatalog(raw, out var required))
            return new Decision(false, true, null, localText);

        if (!TryLocal(localVersion, out var local))
            return new Decision(false, false, Format(required), localText);

        var allowed = Compare(local, required) >= 0;
        return new Decision(allowed, false, Format(required), Format(local));
    }

    /// <summary>Strict catalog form: <c>1.3.14</c>. Rejects prefixes, suffixes, and leading zeros.</summary>
    public static bool TryCatalog(string? raw, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var parts = raw.Trim().Split('.');
        if (parts.Length != 3)
            return false;
        if (!TrySegment(parts[0], out var major) ||
            !TrySegment(parts[1], out var minor) ||
            !TrySegment(parts[2], out var patch))
            return false;
        version = (major, minor, patch);
        return true;
    }

    /// <summary>Three-part string suitable for writing into catalog.json. Null when the local version cannot be read.</summary>
    public static string? CanonicalLocal(string? raw)
    {
        if (!TryLocal(raw, out var version))
            return null;
        return Format(version);
    }

    public static int Compare((int Major, int Minor, int Patch) left, (int Major, int Minor, int Patch) right)
    {
        var major = left.Major.CompareTo(right.Major);
        if (major != 0)
            return major;
        var minor = left.Minor.CompareTo(right.Minor);
        if (minor != 0)
            return minor;
        return left.Patch.CompareTo(right.Patch);
    }

    static bool TryLocal(string? raw, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var value = raw.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];
        var plus = value.IndexOf('+');
        if (plus >= 0)
            value = value[..plus];
        var parts = value.Split('.');
        if (parts.Length is not (3 or 4))
            return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
            return false;
        if (parts.Length == 4 &&
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return false;
        if (major is < 0 or > 65535 || minor is < 0 or > 65535 || patch is < 0 or > 65535)
            return false;
        version = (major, minor, patch);
        return true;
    }

    static bool TrySegment(string text, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 5)
            return false;
        if (text.Length > 1 && text[0] == '0')
            return false;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            return false;
        return value is >= 0 and <= 65535;
    }

    static string Format((int Major, int Minor, int Patch) version) =>
        string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Patch}");
}
