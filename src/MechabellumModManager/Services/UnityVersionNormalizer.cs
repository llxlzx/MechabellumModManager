using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public static class UnityVersionNormalizer
{
    static readonly Regex VersionRx = new(
        @"^\s*(?<maj>\d+)\.(?<min>\d+)\.(?<patch>\d+)([a-zA-Z]\d+)?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryNormalize(string? raw, out string? majorMinorPatch)
    {
        majorMinorPatch = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var m = VersionRx.Match(raw);
        if (!m.Success) return false;
        majorMinorPatch = $"{m.Groups["maj"].Value}.{m.Groups["min"].Value}.{m.Groups["patch"].Value}";
        return true;
    }

    public static string ExpectedZipFileName(string majorMinorPatch) =>
        $"UnityDependencies_{majorMinorPatch}.zip";
}
