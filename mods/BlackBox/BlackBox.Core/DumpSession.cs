namespace BlackBox.Core;

public readonly record struct DumpRunResult(bool ExeMissing, int? ExitCode, bool TimedOut);

public interface IDumpRunner
{
    DumpRunResult Run(string exePath, string arguments, int timeoutMs);
}

public readonly record struct DumpOutcome(string Status, string Error);

public static class DumpArguments
{
    public static string Format(int pid, ulong startedFileTime, string outputTmpPath) =>
        "--pid " + pid + " --started " + startedFileTime + " --out \"" + outputTmpPath + "\"";
}

public static class DumpSession
{
    public static DumpOutcome Complete(
        string helperPath,
        string arguments,
        string tmpPath,
        string finalPath,
        IDumpRunner runner,
        int timeoutMs = 30000)
    {
        if (!File.Exists(helperPath))
            return new DumpOutcome("helper-missing", "helper missing");

        var run = runner.Run(helperPath, arguments, timeoutMs);
        if (run.TimedOut)
            return new DumpOutcome("timed-out", "timed out");
        if (run.ExitCode != 0)
        {
            DeleteIfEmpty(tmpPath);
            return new DumpOutcome("failed", "exit " + run.ExitCode);
        }

        if (!File.Exists(tmpPath) || new FileInfo(tmpPath).Length == 0)
        {
            DeleteIfEmpty(tmpPath);
            return new DumpOutcome("failed", "empty dump");
        }

        if (File.Exists(finalPath))
            File.Replace(tmpPath, finalPath, destinationBackupFileName: null);
        else
            File.Move(tmpPath, finalPath);
        return new DumpOutcome("written", "");
    }

    static void DeleteIfEmpty(string path)
    {
        if (File.Exists(path) && new FileInfo(path).Length == 0)
            File.Delete(path);
    }
}
