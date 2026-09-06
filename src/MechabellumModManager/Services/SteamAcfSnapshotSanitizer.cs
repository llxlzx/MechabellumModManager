using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class SteamAcfSnapshotSanitizeResult
{
    public IReadOnlyList<string> DeletedFiles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Removes manager ACF snapshots whose BetaKey semantics do not match the branch they claim to represent,
/// or whose official/beta pair share the same buildid (poisoned "fake official" snapshots).
/// Does not touch live Steam appmanifest files.
/// </summary>
public static class SteamAcfSnapshotSanitizer
{
    static readonly Regex BetaKeyValue = new(
        @"\""BetaKey\""\s+\""([^\""]*)\""",
        RegexOptions.CultureInvariant);

    public static string? ReadBuildId(string acfText) =>
        SteamBetaKeyEditor.ReadQuotedValue(acfText, "buildid");

    /// <summary>
    /// True when both texts have a non-empty buildid and they are equal.
    /// </summary>
    public static bool HasBuildIdCollision(string? officialAcfText, string? betaAcfText)
    {
        var a = ReadBuildId(officialAcfText ?? "");
        var b = ReadBuildId(betaAcfText ?? "");
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    public static bool IsValidSnapshot(string acfText, GameBranch branch, string betaBranchName)
    {
        if (string.IsNullOrWhiteSpace(acfText))
            return false;

        var userKey = ReadBetaKeyFromBlock(acfText, "UserConfig");
        var hasMounted = TryGetQuotedBlock(acfText, "MountedConfig", out _, out _);
        var mountedKey = hasMounted ? ReadBetaKeyFromBlock(acfText, "MountedConfig") : null;

        if (branch == GameBranch.Official)
        {
            return string.IsNullOrWhiteSpace(userKey)
                   && (!hasMounted || string.IsNullOrWhiteSpace(mountedKey));
        }

        var expected = (betaBranchName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        if (!string.Equals(userKey ?? "", expected, StringComparison.Ordinal))
            return false;

        return !hasMounted || string.Equals(mountedKey ?? "", expected, StringComparison.Ordinal);
    }

    public static SteamAcfSnapshotSanitizeResult SanitizeDirectory(
        string snapshotsDir,
        string? betaBranchName = null)
    {
        var deleted = new List<string>();
        if (string.IsNullOrWhiteSpace(snapshotsDir) || !Directory.Exists(snapshotsDir))
            return new SteamAcfSnapshotSanitizeResult { DeletedFiles = deleted };

        var expectedBeta = string.IsNullOrWhiteSpace(betaBranchName)
            ? BranchSwitchConfig.DefaultSteamBetaBranchName
            : betaBranchName.Trim();

        var officialPath = Path.Combine(snapshotsDir, "official.acf");
        var betaPath = Path.Combine(snapshotsDir, "beta.acf");

        TryDeleteIfInvalid(officialPath, GameBranch.Official, expectedBeta, deleted);
        TryDeleteIfInvalid(betaPath, GameBranch.Beta, expectedBeta, deleted);

        // Same buildid pair is poison even if BetaKey semantics look valid.
        if (File.Exists(officialPath) && File.Exists(betaPath))
        {
            string? officialText = null;
            string? betaText = null;
            try { officialText = File.ReadAllText(officialPath); } catch { /* ignore */ }
            try { betaText = File.ReadAllText(betaPath); } catch { /* ignore */ }
            if (HasBuildIdCollision(officialText, betaText))
            {
                TryDelete(officialPath, deleted);
                TryDelete(betaPath, deleted);
            }
        }

        return new SteamAcfSnapshotSanitizeResult { DeletedFiles = deleted };
    }

    /// <summary>
    /// Sanitize default AppData snapshots dir; beta name from branch-switch.json when present.
    /// </summary>
    public static SteamAcfSnapshotSanitizeResult SanitizeDefault(PathsService? paths = null)
    {
        paths ??= new PathsService();
        var betaName = TryReadBetaBranchName(paths.BranchSwitchConfigPath)
                       ?? BranchSwitchConfig.DefaultSteamBetaBranchName;
        return SanitizeDirectory(paths.SteamAcfSnapshotsDir, betaName);
    }

    static void TryDeleteIfInvalid(
        string path,
        GameBranch branch,
        string betaBranchName,
        List<string> deleted)
    {
        if (!File.Exists(path))
            return;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            TryDelete(path, deleted);
            return;
        }

        if (IsValidSnapshot(text, branch, betaBranchName))
            return;

        TryDelete(path, deleted);
    }

    static void TryDelete(string path, List<string> deleted)
    {
        try
        {
            File.Delete(path);
            deleted.Add(path);
        }
        catch
        {
            // non-fatal for Setup
        }
    }

    static string? TryReadBetaBranchName(string branchSwitchPath)
    {
        try
        {
            if (!File.Exists(branchSwitchPath))
                return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(branchSwitchPath));
            if (doc.RootElement.TryGetProperty("betaBranchName", out var el)
                && el.ValueKind == JsonValueKind.String)
            {
                var v = el.GetString();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    static string? ReadBetaKeyFromBlock(string text, string blockName)
    {
        if (!TryGetQuotedBlock(text, blockName, out var open, out var close))
            return null;

        var inner = text.Substring(open + 1, close - open - 1);
        var match = BetaKeyValue.Match(inner);
        return match.Success ? match.Groups[1].Value : null;
    }

    static bool TryGetQuotedBlock(string text, string key, out int openBrace, out int closeBrace)
    {
        openBrace = closeBrace = -1;
        var needle = $"\"{key}\"";
        var start = 0;
        while (true)
        {
            var idx = text.IndexOf(needle, start, StringComparison.Ordinal);
            if (idx < 0)
                return false;

            var i = idx + needle.Length;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            if (i < text.Length && text[i] == '{')
            {
                openBrace = i;
                var depth = 0;
                for (var j = i; j < text.Length; j++)
                {
                    if (text[j] == '{')
                        depth++;
                    else if (text[j] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            closeBrace = j;
                            return true;
                        }
                    }
                }

                return false;
            }

            start = idx + needle.Length;
        }
    }
}
