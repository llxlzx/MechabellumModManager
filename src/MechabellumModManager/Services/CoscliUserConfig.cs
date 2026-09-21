using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

/// <summary>
/// Writes the SecretId and SecretKey into the coscli config file that program reads by default.
/// </summary>
public static class CoscliUserConfig
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cos.yaml");

    public static bool HasSecret(string? path = null)
    {
        path ??= DefaultPath;
        return !string.IsNullOrWhiteSpace(ReadValue(path, "secretkey"));
    }

    public static string? ReadSecretId(string? path = null) =>
        ReadValue(path ?? DefaultPath, "secretid");

    public static void Save(string path, string secretId, string secretKey, string bucket, string region)
    {
        secretId = secretId.Trim();
        secretKey = secretKey.Trim();
        if (secretId.Length == 0 || secretKey.Length == 0)
            throw new ArgumentException("secret");

        var bucketBlock =
            $"- name: {YamlQuote(bucket)}\n" +
            $"  alias: {YamlQuote(bucket)}\n" +
            $"  region: {YamlQuote(region)}\n" +
            $"  endpoint: {YamlQuote($"cos.{region}.myqcloud.com")}\n" +
            "  ofs: false\n";

        string text;
        if (!File.Exists(path))
        {
            text =
                "secretid: " + YamlQuote(secretId) + "\n" +
                "secretkey: " + YamlQuote(secretKey) + "\n" +
                "sessiontoken: \"\"\n" +
                "buckets:\n" +
                bucketBlock;
        }
        else
        {
            var lines = File.ReadAllLines(path).ToList();
            ReplaceOrInsert(lines, "secretid", secretId);
            ReplaceOrInsert(lines, "secretkey", secretKey);
            text = string.Join('\n', lines);
            if (!text.EndsWith('\n'))
                text += "\n";
            if (!text.Contains(bucket, StringComparison.Ordinal))
            {
                if (!lines.Any(l => l.TrimStart().StartsWith("buckets:", StringComparison.Ordinal)))
                    text += "buckets:\n";
                text += bucketBlock;
            }
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    static void ReplaceOrInsert(List<string> lines, string key, string value)
    {
        var prefix = key + ":";
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var indent = lines[i].Length - lines[i].TrimStart().Length;
                lines[i] = new string(' ', indent) + key + ": " + YamlQuote(value);
                return;
            }
        }

        lines.Insert(0, key + ": " + YamlQuote(value));
    }

    static string? ReadValue(string path, string key)
    {
        if (!File.Exists(path))
            return null;
        var prefix = key + ":";
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = line[prefix.Length..].Trim();
            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                value = value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
            return value.Length == 0 ? null : value;
        }

        return null;
    }

    static string YamlQuote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
