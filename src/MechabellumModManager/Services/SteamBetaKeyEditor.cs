using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public sealed class SteamBetaEditResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? BackupPath { get; init; }
}

public sealed class SteamBetaKeyEditor
{
    public const string AppId = "669330";
    public const string ManifestFileName = "appmanifest_669330.acf";

    static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    static readonly Regex AppIdLine = new(@"\""appid\""\s+\""(\d+)\""", RegexOptions.CultureInvariant);
    static readonly Regex BetaKeyLine = new(@"^[ \t]*\""BetaKey\""\s+\""[^\""]*\""", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    static readonly Regex BetaKeyValue = new(@"\""BetaKey\""\s+\""([^\""]*)\""", RegexOptions.CultureInvariant);
    static readonly Regex SiblingKeyLine = new(@"^[ \t]*\""[^\""]+\""(\s+)\""[^\""]*\""", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    static readonly Regex BetaKeySeparator = new(@"\""BetaKey\""(\s+)\""", RegexOptions.CultureInvariant);

    readonly ISteamRunningProbe _probe;

    public SteamBetaKeyEditor(ISteamRunningProbe probe)
    {
        _probe = probe;
    }

    public static string FindAppManifestPath(string steamLinkGamePath)
        => Path.GetFullPath(Path.Combine(steamLinkGamePath, "..", "..", ManifestFileName));

    public SteamBetaEditResult BackupAndSetBetaKey(string acfPath, string? betaKey, string backupDir)
    {
        if (_probe.IsSteamRunning())
            return Fail("Steam is running.");

        if (!IsAllowedManifestPath(acfPath))
            return Fail("Only appmanifest_669330.acf can be edited.");

        if (!File.Exists(acfPath))
            return Fail("Manifest not found.");

        var text = File.ReadAllText(acfPath, Utf8NoBom);
        EnsureAppIdIsMechabellum(text);

        if (!TryGetQuotedBlock(text, "UserConfig", out _, out _))
            return Fail("UserConfig block not found.");

        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(
            backupDir,
            ManifestFileName + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        File.Copy(acfPath, backupPath, overwrite: false);

        var updated = SetUserConfigBetaKey(text, string.IsNullOrWhiteSpace(betaKey) ? null : betaKey);
        File.WriteAllText(acfPath, updated, Utf8NoBom);
        return new SteamBetaEditResult { Success = true, BackupPath = backupPath };
    }

    public string? ReadBetaKey(string acfPath)
    {
        if (!IsAllowedManifestPath(acfPath) || !File.Exists(acfPath))
            return null;

        var text = File.ReadAllText(acfPath, Utf8NoBom);
        if (!TryGetQuotedBlock(text, "UserConfig", out var open, out var close))
            return null;

        var inner = text.Substring(open + 1, close - open - 1);
        var match = BetaKeyValue.Match(inner);
        return match.Success ? match.Groups[1].Value : null;
    }

    static bool IsAllowedManifestPath(string acfPath)
        => !string.IsNullOrWhiteSpace(acfPath)
           && string.Equals(Path.GetFileName(acfPath), ManifestFileName, StringComparison.OrdinalIgnoreCase);

    static void EnsureAppIdIsMechabellum(string text)
    {
        var match = AppIdLine.Match(text);
        if (match.Success && match.Groups[1].Value != AppId)
            throw new InvalidOperationException($"Refusing to edit appid {match.Groups[1].Value}; only {AppId} is allowed.");
    }

    static string SetUserConfigBetaKey(string text, string? betaKey)
    {
        if (!TryGetQuotedBlock(text, "UserConfig", out var open, out var close))
            throw new InvalidOperationException("UserConfig block not found.");

        var inner = text.Substring(open + 1, close - open - 1);
        var newInner = betaKey is null ? RemoveBetaKeyLine(inner) : UpsertBetaKeyLine(inner, betaKey);
        return text.Substring(0, open + 1) + newInner + text.Substring(close);
    }

    static string RemoveBetaKeyLine(string inner)
    {
        var match = BetaKeyLine.Match(inner);
        if (!match.Success)
            return inner;

        var start = match.Index;
        while (start > 0 && inner[start - 1] != '\n')
            start--;
        var end = match.Index + match.Length;
        while (end < inner.Length && inner[end] != '\n')
            end++;
        if (end < inner.Length && inner[end] == '\n')
            end++;

        return inner.Remove(start, end - start);
    }

    static string UpsertBetaKeyLine(string inner, string betaKey)
    {
        var match = BetaKeyLine.Match(inner);
        if (match.Success)
        {
            var replaced = BetaKeyValue.Replace(match.Value, $"\"BetaKey\"{SeparatorOf(match.Value)}\"{betaKey}\"", 1);
            return inner.Remove(match.Index, match.Length).Insert(match.Index, replaced);
        }

        var line = FormatNewBetaKeyLine(inner, betaKey);
        var lastKey = SiblingKeyLine.Match(inner);
        Match? last = null;
        while (lastKey.Success)
        {
            last = lastKey;
            lastKey = lastKey.NextMatch();
        }

        var nl = inner.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (last is null)
            return inner.TrimEnd('\r', '\n', ' ', '\t') + nl + line + nl;

        var insertAt = last.Index + last.Length;
        return inner.Insert(insertAt, nl + line);
    }

    static string FormatNewBetaKeyLine(string inner, string betaKey)
    {
        var sibling = SiblingKeyLine.Match(inner);
        var indent = "\t\t";
        var sep = "\t\t";
        if (sibling.Success)
        {
            indent = LeadingIndent(sibling.Value);
            sep = sibling.Groups[1].Value;
        }

        return $"{indent}\"BetaKey\"{sep}\"{betaKey}\"";
    }

    static string SeparatorOf(string betaKeyLine)
    {
        var match = BetaKeySeparator.Match(betaKeyLine);
        return match.Success ? match.Groups[1].Value : "\t\t";
    }

    static string LeadingIndent(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;
        return line[..i];
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

    static SteamBetaEditResult Fail(string message) => new() { Success = false, Message = message };
}
