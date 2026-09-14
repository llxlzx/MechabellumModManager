using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

/// <summary>
/// Reads MelonLoader Latest.log for fatal Il2CppAssemblyGenerator failures
/// (e.g. Cpp2IL download from GitHub timed out).
/// </summary>
public static class MelonGenerationFailureReader
{
    public readonly record struct LogSnapshot(long Length, DateTime LastWriteUtc);

    public static LogSnapshot CaptureSnapshot(string gamePath)
    {
        var logPath = LatestLogPath(gamePath);
        if (logPath is null || !File.Exists(logPath))
            return new LogSnapshot(0, DateTime.MinValue);

        try
        {
            var info = new FileInfo(logPath);
            return new LogSnapshot(info.Length, info.LastWriteTimeUtc);
        }
        catch
        {
            return new LogSnapshot(0, DateTime.MinValue);
        }
    }

    /// <summary>
    /// Describes a failure only if Latest.log grew or was rewritten after <paramref name="before"/>.
    /// Avoids treating a stale prior-run INTERNAL FAILURE as the current attempt's result.
    /// </summary>
    public static string? TryDescribeNewFailure(string gamePath, LogSnapshot before)
    {
        var logPath = LatestLogPath(gamePath);
        if (logPath is null || !File.Exists(logPath))
            return null;

        try
        {
            var info = new FileInfo(logPath);
            if (info.Length <= before.Length && info.LastWriteTimeUtc <= before.LastWriteUtc)
                return null;
        }
        catch
        {
            return null;
        }

        return TryDescribe(gamePath);
    }

    public static string? TryDescribe(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return null;

        var logPath = LatestLogPath(gamePath);
        if (logPath is null || !File.Exists(logPath))
            return null;

        string text;
        try
        {
            text = File.ReadAllText(logPath, Encoding.UTF8);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Contains("Failed to Download Cpp2IL", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("Cpp2IL", StringComparison.OrdinalIgnoreCase)
                && text.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                && text.Contains("INTERNAL FAILURE", StringComparison.OrdinalIgnoreCase)))
        {
            return "Cpp2IL 下载失败（无法连接 GitHub）。请用管理器重新安装 MelonLoader 以播种离线包，或配置代理后再试。";
        }

        if (text.Contains("[INTERNAL FAILURE]", StringComparison.OrdinalIgnoreCase))
        {
            var line = text.Split('\n')
                .Select(l => l.Trim())
                .LastOrDefault(l => l.Contains("[INTERNAL FAILURE]", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(line))
                return "MelonLoader 生成失败：" + line;
            return "MelonLoader 生成失败（INTERNAL FAILURE）。请查看 MelonLoader\\Latest.log。";
        }

        return null;
    }

    static string? LatestLogPath(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return null;
        return Path.Combine(gamePath, "MelonLoader", "Latest.log");
    }
}
