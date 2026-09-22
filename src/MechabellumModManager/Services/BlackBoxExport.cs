using System.IO;
using System.Text;

namespace MechabellumModManager.Services;

public readonly record struct BlackBoxCollectResult(string Status, bool DumpIncluded);

public static class BlackBoxExport
{
    public const long MaxDumpBytes = 64L * 1024 * 1024;
    const string DumpWarning =
        "blackbox.dmp is not redacted and may contain local paths and memory fragments.";

    public static string DumpWarningText => DumpWarning;

    public static BlackBoxCollectResult Collect(string staging, string? gamePath, List<string> missing, bool redact)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            missing.Add("blackbox/");
            return new BlackBoxCollectResult("absent", false);
        }

        var dir = Path.Combine(gamePath, "UserData", "BlackBox");
        if (!Directory.Exists(dir))
        {
            missing.Add("blackbox/");
            return new BlackBoxCollectResult("absent", false);
        }

        var hasHeartbeat = CopyText(staging, dir, "heartbeat.txt", "blackbox/heartbeat.txt", missing, redact);
        var hasSummary = CopyText(staging, dir, "summary.txt", "blackbox/summary.txt", missing, redact);
        var dumpIncluded = CopyDump(staging, dir, missing);

        if (!hasSummary && !missing.Contains("blackbox/summary.txt"))
            missing.Add("blackbox/summary.txt");

        var status = (hasSummary, dumpIncluded) switch
        {
            (true, true) => "summary-and-dump",
            (false, true) => "dump-only",
            (true, false) => "summary",
            (false, false) when hasHeartbeat => "heartbeat-only",
            _ => "empty"
        };
        return new BlackBoxCollectResult(status, dumpIncluded);
    }

    static bool CopyText(string staging, string dir, string name, string relative, List<string> missing, bool redact)
    {
        var source = Path.Combine(dir, name);
        if (!File.Exists(source))
            return false;
        try
        {
            var text = DiagnosticsExportService.ReadTextCapped(source);
            if (redact)
                text = DiagnosticsExportService.Redact(text);
            var dest = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllText(dest, text, Encoding.UTF8);
            return true;
        }
        catch
        {
            missing.Add(relative);
            return false;
        }
    }

    static bool CopyDump(string staging, string dir, List<string> missing)
    {
        var source = Path.Combine(dir, "blackbox.dmp");
        if (!File.Exists(source))
            return false;
        try
        {
            if (new FileInfo(source).Length > MaxDumpBytes)
            {
                missing.Add("blackbox/blackbox.dmp (over 64MB)");
                return false;
            }
            var destDir = Path.Combine(staging, "blackbox");
            Directory.CreateDirectory(destDir);
            File.Copy(source, Path.Combine(destDir, "blackbox.dmp"), overwrite: true);
            return true;
        }
        catch
        {
            missing.Add("blackbox/blackbox.dmp");
            return false;
        }
    }
}
