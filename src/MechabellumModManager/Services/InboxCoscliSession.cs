using System.Diagnostics;
using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

/// <summary>
/// Writes a one-shot coscli config and uploads with <c>-c</c> that file.
/// Never reads or merges an existing yaml, including the user-profile config.
/// </summary>
public static class InboxCoscliSession
{
    public static void Write(string path, string secretId, string secretKey, string sessionToken, string bucket, string region)
    {
        var text =
            "secretid: " + Quote(secretId) + "\n" +
            "secretkey: " + Quote(secretKey) + "\n" +
            "sessiontoken: " + Quote(sessionToken) + "\n" +
            "buckets:\n" +
            "- name: " + Quote(bucket) + "\n" +
            "  alias: " + Quote(bucket) + "\n" +
            "  region: " + Quote(region) + "\n" +
            "  endpoint: " + Quote("cos." + region + ".myqcloud.com") + "\n" +
            "  ofs: false\n";

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static async Task RunAsync(
        string coscliPath,
        string configPath,
        string localPath,
        string bucket,
        string objectKey,
        string sha256,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = coscliPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var key = (objectKey ?? "").TrimStart('/');
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(configPath);
        psi.ArgumentList.Add("cp");
        psi.ArgumentList.Add(localPath);
        psi.ArgumentList.Add("cos://" + bucket + "/" + key);
        psi.ArgumentList.Add("--meta");
        psi.ArgumentList.Add("x-cos-meta-sha256:" + sha256);

        try
        {
            using var process = Process.Start(psi)
                ?? throw new MirrorPublishException(
                    DirectUploadErrorCode.NotConfigured,
                    nameof(DirectUploadErrorCode.NotConfigured));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            await stdoutTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new MirrorPublishException(DirectUploadErrorCode.Network, StripSessionTokenLines(stderr));
        }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                    File.Delete(configPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    static string StripSessionTokenLines(string? stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return "";
        var kept = new List<string>();
        foreach (var line in stderr.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Contains("sessiontoken", StringComparison.OrdinalIgnoreCase))
                continue;
            kept.Add(line);
        }

        return string.Join('\n', kept).Trim();
    }

    static string Quote(string? value) =>
        "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
