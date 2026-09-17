using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace MechabellumModManager.Services;

public sealed record ManagerSetupDownloadProgress(
    string Message,
    double? Percent,
    long BytesReceived,
    long? TotalBytes);

/// <summary>
/// Downloads the manager Setup installer to a temp folder. Does not launch or install.
/// </summary>
public sealed class ManagerSetupDownloader
{
    static readonly Regex UnsafeVersionChars = new(@"[^A-Za-z0-9._-]+", RegexOptions.Compiled);

    readonly HttpClient _http;
    readonly string _tempRoot;

    public ManagerSetupDownloader(HttpClient? http = null, string? tempRoot = null)
    {
        _http = http ?? CreateDefaultClient();
        _tempRoot = string.IsNullOrWhiteSpace(tempRoot)
            ? Path.Combine(Path.GetTempPath(), "mmm-update")
            : tempRoot;
    }

    public static HttpClient CreateDefaultClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MechabellumModManager");
        return http;
    }

    public static string SanitizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "unknown";
        var cleaned = UnsafeVersionChars.Replace(version.Trim(), "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }

    public async Task<string> DownloadAsync(
        string setupUrl,
        string version,
        IProgress<ManagerSetupDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!ExternalUrlPolicy.IsDownloadableSetupUrl(setupUrl))
            throw new InvalidOperationException("Setup URL is not a downloadable https .exe.");

        var dir = Path.Combine(_tempRoot, SanitizeVersion(version));
        Directory.CreateDirectory(dir);
        var finalPath = Path.Combine(dir, "MechabellumModManager_Setup.exe");
        var partPath = finalPath + ".part";

        try
        {
            if (File.Exists(partPath))
                File.Delete(partPath);
            if (File.Exists(finalPath))
                File.Delete(finalPath);

            using var resp = await _http
                .GetAsync(setupUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var finalUri = resp.RequestMessage?.RequestUri;
            if (finalUri is not null &&
                !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Setup download redirected to a non-https URL.");
            }

            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength;
            await using var input = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(
                             partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long received = 0;
                var lastReportedPercent = -1;
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                           .ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;

                    double? percent = null;
                    if (total is > 0)
                        percent = Math.Min(100, received * 100.0 / total.Value);

                    var percentInt = percent is null ? -1 : (int)percent.Value;
                    var shouldReport = total is > 0
                        ? percentInt != lastReportedPercent
                        : received == read || received % (512 * 1024) < read;

                    if (shouldReport)
                    {
                        lastReportedPercent = percentInt;
                        var msg = total is > 0
                            ? $"正在下载安装包… {FormatBytes(received)} / {FormatBytes(total.Value)} ({percentInt}%)"
                            : $"正在下载安装包… 已下载 {FormatBytes(received)}";
                        progress?.Report(new ManagerSetupDownloadProgress(msg, percent, received, total));
                    }
                }
            }

            File.Move(partPath, finalPath, overwrite: true);
            progress?.Report(new ManagerSetupDownloadProgress(
                "下载完成。", 100, total ?? new FileInfo(finalPath).Length, total ?? new FileInfo(finalPath).Length));
            return finalPath;
        }
        catch
        {
            TryDelete(partPath);
            TryDelete(finalPath);
            throw;
        }
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
            // best-effort cleanup
        }
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }
}
