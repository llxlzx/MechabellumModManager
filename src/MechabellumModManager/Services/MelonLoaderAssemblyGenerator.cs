using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class MelonAssemblyGenerateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool Skipped { get; init; }
}

/// <summary>
/// Scheme B: start Mechabellum.exe once so MelonLoader generates Il2CppAssemblies, then wait/poll.
/// </summary>
public sealed class MelonLoaderAssemblyGenerator
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(180);
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

    static readonly Regex GameAssemblyHashRegex = new(
        @"GameAssemblyHash\s*=\s*""[^""]*""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    readonly GameDetector _detector;
    readonly HashSet<string> _attemptedStores = new(StringComparer.OrdinalIgnoreCase);
    readonly Func<string, Process?> _startProcess;
    readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public MelonLoaderAssemblyGenerator(
        GameDetector? detector = null,
        Func<string, Process?>? startProcess = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _detector = detector ?? new GameDetector();
        _startProcess = startProcess ?? StartGameProcess;
        _delay = delay ?? ((ts, ct) => Task.Delay(ts, ct));
    }

    /// <summary>
    /// When Il2CppAssemblies are missing but Config.cfg still has GameAssemblyHash,
    /// MelonLoader skips generation ("Assembly is up to date"). Clear the hash so the
    /// next game launch regenerates. Dual-store sync can create this broken state by
    /// copying Config.cfg while excluding Il2CppAssemblies.
    /// </summary>
    public static bool InvalidateStaleGenerationState(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;
        if (GameDetector.HasIl2CppAssemblies(gamePath))
            return false;

        var cfgPath = Path.Combine(
            gamePath,
            "MelonLoader",
            "Dependencies",
            "Il2CppAssemblyGenerator",
            "Config.cfg");
        if (!File.Exists(cfgPath))
            return false;

        string text;
        try
        {
            text = File.ReadAllText(cfgPath, Encoding.UTF8);
        }
        catch
        {
            return false;
        }

        if (!GameAssemblyHashRegex.IsMatch(text))
            return false;

        // Already empty → nothing to do.
        if (Regex.IsMatch(text, @"GameAssemblyHash\s*=\s*""""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            return false;

        var updated = GameAssemblyHashRegex.Replace(text, @"GameAssemblyHash = """"");
        if (string.Equals(updated, text, StringComparison.Ordinal))
            return false;

        try
        {
            File.WriteAllText(cfgPath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public MelonAssemblyGenerateResult EnsureAssemblies(
        string gamePath,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        return EnsureAssembliesAsync(gamePath, timeout, pollInterval, cancellationToken, progress)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<MelonAssemblyGenerateResult> EnsureAssembliesAsync(
        string gamePath,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            return Fail("游戏目录无效。");

        var status = _detector.Detect(gamePath);
        if (status.Kind == GameStatusKind.Ready)
            return new MelonAssemblyGenerateResult { Success = true, Skipped = true, Message = "程序集已存在，跳过生成。" };

        if (status.Kind is not GameStatusKind.LoaderPresentAssembliesMissing)
            return Fail($"当前状态无法生成程序集：{status.Message}");

        var full = Path.GetFullPath(gamePath);

        // Clear stale Melon cache BEFORE the session attempt gate, so a prior failed
        // attempt caused by "hash present / assemblies missing" can be retried once we fix it.
        if (InvalidateStaleGenerationState(full))
        {
            _attemptedStores.Remove(full);
            progress?.Invoke("已清除过期的程序集生成缓存（Config.cfg GameAssemblyHash），将强制 MelonLoader 重新生成。");
        }

        if (!_attemptedStores.Add(full))
            return Fail("本会话已尝试过自动生成但未成功。请手动启动一次游戏完成 MelonLoader 首次生成，或查看 MelonLoader\\Latest.log。");

        var exe = Path.Combine(full, GameLauncher.GameExeName);
        if (!File.Exists(exe))
            return Fail("未找到 Mechabellum.exe。");

        if (!File.Exists(Path.Combine(full, "version.dll")) && !File.Exists(Path.Combine(full, "winhttp.dll")))
            return Fail("缺少 MelonLoader 代理 DLL（version.dll / winhttp.dll）。");

        var wait = timeout ?? DefaultTimeout;
        var interval = pollInterval ?? DefaultPollInterval;
        progress?.Invoke("正在短暂启动游戏以触发 MelonLoader 生成程序集。");

        Process? proc = null;
        try
        {
            proc = _startProcess(exe);
            if (proc is null)
                progress?.Invoke("未附加到游戏进程，将继续轮询生成文件。");

            var marker = Path.Combine(full, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll");
            long? lastSize = null;
            var stableHits = 0;
            var deadline = DateTime.UtcNow + wait;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(marker))
                {
                    var size = new FileInfo(marker).Length;
                    if (lastSize is not null && lastSize.Value == size && size > 0)
                    {
                        stableHits++;
                        if (stableHits >= 2)
                        {
                            TryKill(proc);
                            if (_detector.Detect(full).Kind == GameStatusKind.Ready)
                                return new MelonAssemblyGenerateResult
                                {
                                    Success = true,
                                    Message = "MelonLoader 程序集已生成。"
                                };
                        }
                    }
                    else
                    {
                        stableHits = 0;
                        lastSize = size;
                    }
                }

                progress?.Invoke("等待 MelonLoader 生成 Il2Cpp 程序集…");
                await _delay(interval, cancellationToken).ConfigureAwait(false);
            }

            TryKill(proc);
            return Fail(
                $"等待程序集生成超时（{wait.TotalSeconds:0} 秒）。请手动启动一次游戏完成首次生成，并查看日志：MelonLoader\\Latest.log");
        }
        catch (OperationCanceledException)
        {
            TryKill(proc);
            return Fail("已取消程序集生成。");
        }
        catch (Exception ex)
        {
            TryKill(proc);
            return Fail($"生成程序集失败：{ex.Message}");
        }
    }

    static Process? StartGameProcess(string exePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
            UseShellExecute = false
        };
        return Process.Start(psi);
    }

    static void TryKill(Process? proc)
    {
        if (proc is null) return;
        try
        {
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
        finally
        {
            try { proc.Dispose(); } catch { /* ignore */ }
        }
    }

    static MelonAssemblyGenerateResult Fail(string message) =>
        new() { Success = false, Message = message };
}
