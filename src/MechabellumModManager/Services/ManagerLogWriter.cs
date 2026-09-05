using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Append-only daily manager logs under <see cref="PathsService.LogsDir"/>.
/// Thread-safe; uses FileShare.ReadWrite so diagnostics export can read while writing.
/// </summary>
public sealed class ManagerLogWriter
{
    readonly object _gate = new();
    readonly string _logsDir;
    readonly int _retainDays;
    readonly int _maxFiles;

    public ManagerLogWriter(string logsDir, int retainDays = 14, int maxFiles = 7)
    {
        _logsDir = logsDir ?? throw new ArgumentNullException(nameof(logsDir));
        _retainDays = Math.Max(1, retainDays);
        _maxFiles = Math.Max(1, maxFiles);
    }

    public string TodayLogPath =>
        Path.Combine(_logsDir, $"manager-{DateTime.Now:yyyy-MM-dd}.log");

    public void Append(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_logsDir);
                using var fs = new FileStream(
                    TodayLogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                using var sw = new StreamWriter(fs);
                sw.WriteLine(line);
            }
        }
        catch
        {
            // Never break the UI/business path for log I/O.
        }
    }

    public void PurgeOldFiles()
    {
        try
        {
            if (!Directory.Exists(_logsDir))
                return;

            lock (_gate)
            {
                var files = Directory.GetFiles(_logsDir, "manager-*.log")
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();

                var cutoff = DateTime.UtcNow.Date.AddDays(-_retainDays);
                for (var i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    var keep = i < _maxFiles && f.LastWriteTimeUtc.Date >= cutoff;
                    if (keep)
                        continue;
                    try { f.Delete(); } catch { /* ignore */ }
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    public IReadOnlyList<string> ListRecentLogFiles()
    {
        try
        {
            if (!Directory.Exists(_logsDir))
                return Array.Empty<string>();

            return Directory.GetFiles(_logsDir, "manager-*.log")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(_maxFiles)
                .Select(f => f.FullName)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
