using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public sealed class UnityVersionResolver
{
    const int MaxScanBytes = 4 * 1024 * 1024;

    static readonly Regex VersionRx = new(
        @"(20\d{2}\.\d+\.\d+[a-zA-Z]\d+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public bool TryResolve(string gamePath, out string? majorMinorPatch)
    {
        majorMinorPatch = null;
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            return false;

        var globalGameManagers = FindGlobalGameManagers(gamePath);
        if (globalGameManagers is null)
            return false;

        return TryReadVersion(globalGameManagers, out majorMinorPatch);
    }

    static string? FindGlobalGameManagers(string gamePath)
    {
        var preferred = Path.Combine(gamePath, "Mechabellum_Data", "globalgamemanagers");
        if (File.Exists(preferred))
            return preferred;

        foreach (var dataDir in Directory.EnumerateDirectories(gamePath, "*_Data"))
        {
            var candidate = Path.Combine(dataDir, "globalgamemanagers");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    static bool TryReadVersion(string filePath, out string? majorMinorPatch)
    {
        majorMinorPatch = null;
        using var stream = File.OpenRead(filePath);
        var length = (int)Math.Min(stream.Length, MaxScanBytes);
        var buffer = new byte[length];
        if (stream.Read(buffer, 0, length) <= 0)
            return false;

        var text = Encoding.UTF8.GetString(buffer);
        foreach (Match match in VersionRx.Matches(text))
        {
            if (UnityVersionNormalizer.TryNormalize(match.Value, out var normalized))
            {
                majorMinorPatch = normalized;
                return true;
            }
        }

        return false;
    }
}
