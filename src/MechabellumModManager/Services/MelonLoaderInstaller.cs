using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.IO;

namespace MechabellumModManager.Services;

public sealed class MelonLoaderInstallResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? VersionTag { get; init; }
}

public sealed class MelonLoaderProgress
{
    public string Message { get; init; } = "";
    /// <summary>0-100 when known; null means indeterminate.</summary>
    public double? Percent { get; init; }
    public long? BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
}

public sealed class MelonLoaderInstaller
{
    public const string LatestApiUrl = "https://api.github.com/repos/LavaGang/MelonLoader/releases/latest";
    public const string LatestZipFallbackUrl =
        "https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip";
    public const string ReleasesPageUrl = "https://github.com/LavaGang/MelonLoader/releases";
    public const string DotNet6DesktopUrl =
        "https://dotnet.microsoft.com/download/dotnet/6.0";

    readonly HttpClient _http;
    readonly Func<bool> _isGameRunning;
    readonly Func<CancellationToken, Task<string>>? _resolveZipUrlAsync;

    public MelonLoaderInstaller(
        HttpClient? http = null,
        ProcessProbe? probe = null,
        Func<CancellationToken, Task<string>>? resolveZipUrlAsync = null,
        Func<bool>? isGameRunning = null)
    {
        _http = http ?? CreateDefaultHttpClient();
        var p = probe ?? new ProcessProbe();
        _isGameRunning = isGameRunning ?? p.IsGameRunning;
        _resolveZipUrlAsync = resolveZipUrlAsync;
    }

    public static HttpClient CreateDefaultHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MechabellumModManager", "1.0"));
        return http;
    }

    public async Task<MelonLoaderInstallResult> InstallAsync(
        string gamePath,
        IProgress<MelonLoaderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gamePath) ||
            !File.Exists(Path.Combine(gamePath, "Mechabellum.exe")) ||
            !File.Exists(Path.Combine(gamePath, "GameAssembly.dll")))
        {
            return Fail("游戏路径无效，无法安装 MelonLoader。");
        }

        if (_isGameRunning())
            return Fail("检测到 Mechabellum 正在运行，请先关闭游戏后再安装。");

        var stagingRoot = Path.Combine(Path.GetTempPath(), "mmm-ml-" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(stagingRoot, "MelonLoader.x64.zip");
        var extractDir = Path.Combine(stagingRoot, "extract");
        Directory.CreateDirectory(extractDir);

        var written = new List<string>();
        try
        {
            Report(progress, "正在解析 MelonLoader 最新正式版…", percent: null);
            string zipUrl;
            string? tag = null;
            try
            {
                (zipUrl, tag) = await ResolveLatestZipAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Fail($"无法获取 MelonLoader 最新版本：{ex.Message}\n请手动打开：{ReleasesPageUrl}");
            }

            Report(progress,
                tag is null ? "正在下载 MelonLoader…" : $"正在下载 MelonLoader {tag}…",
                percent: 0);
            Directory.CreateDirectory(stagingRoot);
            try
            {
                await DownloadFileAsync(zipUrl, zipPath, progress, tag, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Fail($"下载失败：{ex.Message}\n请检查网络或手动下载：{ReleasesPageUrl}");
            }

            Report(progress, "正在解压…", percent: 100);
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            Report(progress, "正在写入游戏目录…", percent: 100);
            try
            {
                CopyExtractedPayload(extractDir, gamePath, written);
            }
            catch (Exception ex)
            {
                Rollback(written);
                return Fail($"写入游戏目录失败：{ex.Message}\n可尝试以管理员身份运行，或检查文件夹权限。");
            }

            var status = new GameDetector().Detect(gamePath);
            if (status.Kind is not (Models.GameStatusKind.Ready or Models.GameStatusKind.LoaderPresentAssembliesMissing))
            {
                return new MelonLoaderInstallResult
                {
                    Success = false,
                    VersionTag = tag,
                    Message =
                        $"文件已写入，但检测仍为「{Describe(status.Kind)}」：{status.Message}\n" +
                        $"若游戏无法启动 MelonLoader，请安装 .NET 6 Desktop Runtime：{DotNet6DesktopUrl}"
                };
            }

            // Seed before optimize so exact-match offline can become true when redist is present.
            var seed = new UnityDependenciesSeeder().Seed(gamePath);
            var optimize = seed.Success && seed.Version != null
                ? new MelonLoaderConfigOptimizer().ApplyRecommendedSettings(gamePath, seed.Version)
                : new MelonLoaderConfigOptimizer().ApplyRecommendedSettings(gamePath);
            var firstLaunchHint =
                "\n注意：首次启动游戏时 MelonLoader 会生成 IL2CPP 程序集（下载工具 + 分析 GameAssembly），" +
                "可能卡住 1～2 分钟，属正常现象；完成后再次启动会快很多。";

            var seedNote = string.IsNullOrWhiteSpace(seed.Message)
                ? ""
                : seed.Success
                    ? "\n" + seed.Message
                    : "\n警告：UnityDependencies 播种失败 — " + seed.Message;

            return new MelonLoaderInstallResult
            {
                Success = true,
                VersionTag = tag,
                Message = (tag is null
                    ? "MelonLoader 安装成功，状态：就绪。"
                    : $"MelonLoader {tag} 安装成功，状态：就绪。")
                    + seedNote
                    + (optimize.Changed ? "\n" + optimize.Message : "")
                    + firstLaunchHint
            };
        }
        finally
        {
            TryDeleteDir(stagingRoot);
        }
    }

    async Task<(string Url, string? Tag)> ResolveLatestZipAsync(CancellationToken ct)
    {
        if (_resolveZipUrlAsync is not null)
        {
            var url = await _resolveZipUrlAsync(ct).ConfigureAwait(false);
            return (url, null);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!string.Equals(name, "MelonLoader.x64.zip", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(url))
                        return (url!, tag);
                }
            }
        }
        catch
        {
            // Fall back to the well-known latest download URL.
        }

        return (LatestZipFallbackUrl, null);
    }

    async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<MelonLoaderProgress>? progress,
        string? tag,
        CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var output = File.Create(destination);

        var buffer = new byte[81920];
        long received = 0;
        var lastReportedPercent = -1;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;

            double? percent = null;
            if (total is > 0)
                percent = Math.Min(100, received * 100.0 / total.Value);

            var percentInt = percent is null ? -1 : (int)percent.Value;
            // Throttle UI updates: every 1% or every 512KB when size unknown.
            var shouldReport = total is > 0
                ? percentInt != lastReportedPercent
                : received == read || received % (512 * 1024) < read;

            if (shouldReport)
            {
                lastReportedPercent = percentInt;
                var label = tag is null ? "MelonLoader" : $"MelonLoader {tag}";
                var msg = total is > 0
                    ? $"正在下载 {label}… {FormatBytes(received)} / {FormatBytes(total.Value)} ({percentInt}%)"
                    : $"正在下载 {label}… 已下载 {FormatBytes(received)}";
                Report(progress, msg, percent, received, total);
            }
        }

        Report(progress,
            tag is null ? "下载完成。" : $"MelonLoader {tag} 下载完成。",
            percent: 100,
            bytesReceived: received,
            totalBytes: total ?? received);
    }

    static void Report(
        IProgress<MelonLoaderProgress>? progress,
        string message,
        double? percent,
        long? bytesReceived = null,
        long? totalBytes = null)
    {
        progress?.Report(new MelonLoaderProgress
        {
            Message = message,
            Percent = percent,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.00} MB";
    }

    /// <summary>Used by <see cref="MelonLoaderDualStoreSync"/> to install from a local zip.</summary>
    public static void CopyExtractedPayloadForSync(string extractDir, string gamePath, List<string> written)
        => CopyExtractedPayload(extractDir, gamePath, written);

    static void CopyExtractedPayload(string extractDir, string gamePath, List<string> written)
    {
        // Prefer contents of a top-level MelonLoader folder layout from the zip root.
        foreach (var path in Directory.EnumerateFileSystemEntries(extractDir))
        {
            var name = Path.GetFileName(path);
            var dest = Path.Combine(gamePath, name);
            if (Directory.Exists(path))
            {
                CopyDirectory(path, dest, written);
            }
            else if (File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(path, dest, overwrite: true);
                written.Add(dest);
            }
        }

        if (!Directory.Exists(Path.Combine(gamePath, "MelonLoader")) &&
            !File.Exists(Path.Combine(gamePath, "version.dll")) &&
            !File.Exists(Path.Combine(gamePath, "winhttp.dll")))
        {
            throw new InvalidOperationException("压缩包中未找到 MelonLoader 或代理 DLL（version.dll / winhttp.dll）。");
        }
    }

    static void CopyDirectory(string sourceDir, string destDir, List<string> written)
    {
        Directory.CreateDirectory(destDir);
        written.Add(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
            written.Add(dest);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)), written);
    }

    static void Rollback(List<string> written)
    {
        foreach (var path in written.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                    Directory.Delete(path);
            }
            catch
            {
                // Best-effort rollback.
            }
        }
    }

    static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore
        }
    }

    static MelonLoaderInstallResult Fail(string message) =>
        new() { Success = false, Message = message };

    static string Describe(Models.GameStatusKind kind) => kind switch
    {
        Models.GameStatusKind.Ready => "就绪",
        Models.GameStatusKind.LoaderPresentAssembliesMissing => "待生成程序集",
        Models.GameStatusKind.GameOkLoaderMissing => "缺少 Loader",
        Models.GameStatusKind.LoaderPartial => "Loader 不完整",
        Models.GameStatusKind.GameMissing => "未找到游戏",
        _ => kind.ToString()
    };
}
