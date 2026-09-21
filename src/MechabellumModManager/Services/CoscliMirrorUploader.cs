using System.Diagnostics;
using System.IO;

namespace MechabellumModManager.Services;

public interface IMirrorObjectUploader
{
    Task UploadAsync(string localPath, string remoteKey, CancellationToken ct);
}

/// <summary>
/// Writes objects with the machine's coscli config. The bucket secret stays in that config,
/// not in the manager.
/// </summary>
public sealed class CoscliMirrorUploader : IMirrorObjectUploader
{
    readonly string _coscliPath;
    readonly string _bucket;

    public CoscliMirrorUploader(string coscliPath, string bucket)
    {
        _coscliPath = coscliPath;
        _bucket = bucket;
    }

    public static string? TryFind()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "coscli.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MMMirror", "bin", "coscli.exe");
        return File.Exists(local) ? local : null;
    }

    public async Task UploadAsync(string localPath, string remoteKey, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _coscliPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("cp");
        psi.ArgumentList.Add(localPath);
        psi.ArgumentList.Add($"cos://{_bucket}/{remoteKey.TrimStart('/')}");

        using var process = Process.Start(psi)
            ?? throw new MirrorPublishException(DirectUploadErrorCode.NotConfigured, "coscli");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        await stdoutTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new MirrorPublishException(DirectUploadErrorCode.Network, stderr.Trim());
    }
}
