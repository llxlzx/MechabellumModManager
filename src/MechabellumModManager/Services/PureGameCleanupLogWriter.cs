using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Cleanup log that survives DeleteAppDataRoot (Roaming wipe).
/// Durable copy lives under LocalAppData; roaming is best-effort.
/// </summary>
public sealed class PureGameCleanupLogWriter
{
    readonly string _durablePath;
    readonly string? _roamingPath;

    public PureGameCleanupLogWriter(string durablePath, string? roamingPath = null)
    {
        _durablePath = durablePath;
        _roamingPath = roamingPath;
    }

    public static string DefaultDurablePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            PathsService.AppFolderName,
            "pure-game-cleanup.log");

    public static string DefaultRoamingPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PathsService.AppFolderName,
            "pure-game-cleanup.log");

    public void Append(string message)
    {
        var line = FormatLine(message);
        TryAppend(_durablePath, line);
        if (!string.IsNullOrWhiteSpace(_roamingPath))
            TryAppend(_roamingPath, line);
    }

    public void WriteAll(IEnumerable<string> lines)
    {
        var body = string.Concat(lines.Select(l => FormatLine(l)));
        TryWrite(_durablePath, body);
        if (!string.IsNullOrWhiteSpace(_roamingPath))
            TryWrite(_roamingPath, body);
    }

    static string FormatLine(string message) =>
        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

    static void TryAppend(string path, string text)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(path, text);
        }
        catch
        {
            // ignore
        }
    }

    static void TryWrite(string path, string text)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, text);
        }
        catch
        {
            // ignore
        }
    }
}
