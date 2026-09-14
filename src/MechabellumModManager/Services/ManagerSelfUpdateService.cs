using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace MechabellumModManager.Services;

public sealed class ManagerSelfUpdateService
{
    readonly HttpClient _http;
    readonly Func<string> _appBaseProvider;
    readonly Action? _requestShutdown;

    public ManagerSelfUpdateService(
        HttpClient? http = null,
        Func<string>? appBaseProvider = null,
        Action? requestShutdown = null)
    {
        _http = http ?? UpdateChecker.CreateDefaultClient();
        _http.Timeout = TimeSpan.FromMinutes(30);
        _appBaseProvider = appBaseProvider ?? (() => AppContext.BaseDirectory);
        _requestShutdown = requestShutdown;
    }

    public async Task ApplyAsync(
        UpdateManifest manifest,
        ManagerDeploymentKind kind,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!ManagerSelfUpdatePlanner.CanApplyOneClick(kind, manifest))
            throw new InvalidOperationException("manifest missing https URL or sha256 for this install type");

        var appBase = Path.GetFullPath(_appBaseProvider());
        var workDir = Path.Combine(Path.GetTempPath(), "MechabellumModManager-update");
        Directory.CreateDirectory(workDir);

        if (kind == ManagerDeploymentKind.Installed)
        {
            var dest = Path.Combine(workDir, "Setup.exe");
            await DownloadVerifiedAsync(
                    manifest.SetupUrl!.Trim(),
                    dest,
                    ManagerSelfUpdatePlanner.NormalizeSha256(manifest.SetupSha256!),
                    progress,
                    ct)
                .ConfigureAwait(false);

            var args =
                $"/SILENT /CLOSEAPPLICATIONS /NORESTART /DIR=\"{appBase.TrimEnd('\\')}\"";
            var psi = new ProcessStartInfo
            {
                FileName = dest,
                Arguments = args,
                UseShellExecute = true
            };
            Process.Start(psi);
            _requestShutdown?.Invoke();
            return;
        }

        var zipPath = Path.Combine(workDir, "portable.zip");
        await DownloadVerifiedAsync(
                manifest.PortableUrl!.Trim(),
                zipPath,
                ManagerSelfUpdatePlanner.NormalizeSha256(manifest.PortableSha256!),
                progress,
                ct)
            .ConfigureAwait(false);

        var staging = Path.Combine(appBase, PortableUpdateApplier.StagingFolderName);
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);

        ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);
        FlattenIfSingleRootFolder(staging);

        var helper = PortableUpdateApplier.WriteReplaceHelper(
            workDir,
            Environment.ProcessId,
            staging,
            appBase);
        Process.Start(new ProcessStartInfo
        {
            FileName = helper,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        _requestShutdown?.Invoke();
    }

    /// <summary>
    /// Portable zips sometimes wrap files in one top-level folder; flatten so exe sits at staging root.
    /// </summary>
    internal static void FlattenIfSingleRootFolder(string staging)
    {
        var entries = Directory.GetFileSystemEntries(staging);
        if (entries.Length != 1)
            return;
        if (!Directory.Exists(entries[0]))
            return;

        var inner = entries[0];
        foreach (var child in Directory.GetFileSystemEntries(inner))
        {
            var name = Path.GetFileName(child);
            var dest = Path.Combine(staging, name);
            if (Directory.Exists(child))
                Directory.Move(child, dest);
            else
                File.Move(child, dest, overwrite: true);
        }

        Directory.Delete(inner, recursive: true);
    }

    public async Task DownloadVerifiedAsync(
        string url,
        string destPath,
        string expectedSha256,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        if (!ExternalUrlPolicy.IsHttpsUrl(url))
            throw new InvalidOperationException("only https download URLs are allowed");

        var part = destPath + ".part";
        TryDelete(part);
        TryDelete(destPath);

        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? 0L;

        await using (var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                progress?.Report(new DownloadProgress(done, total));
            }
        }

        var actual = await HashFileAsync(part, ct).ConfigureAwait(false);
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(part);
            throw new InvalidOperationException(
                $"sha256 mismatch: expected {expectedSha256}, got {actual}");
        }

        File.Move(part, destPath, overwrite: true);
    }

    static async Task<string> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* best effort */
        }
    }
}
