using System.Text;

namespace BlackBox.Core;

public readonly record struct SummaryFields(
    string Trigger,
    string Utc,
    int Pid,
    long UptimeSeconds,
    long SilentSeconds,
    string GamePath,
    string UnityVersion,
    string Scene,
    string WorkingSetMb,
    string Plugins,
    string Mods,
    string Dump,
    string DumpError);

public static class IncidentWriter
{
    static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static string FormatHeartbeat(DateTimeOffset utc, int pid, long uptimeSeconds, string unityVersion, string scene) =>
        "utc: " + utc.ToUniversalTime().ToString("o") + "\n" +
        "pid: " + pid + "\n" +
        "uptimeSeconds: " + uptimeSeconds + "\n" +
        "unityVersion: " + unityVersion + "\n" +
        "scene: " + scene + "\n";

    public static string FormatSummary(SummaryFields fields) =>
        "trigger: " + fields.Trigger + "\n" +
        "utc: " + fields.Utc + "\n" +
        "pid: " + fields.Pid + "\n" +
        "uptimeSeconds: " + fields.UptimeSeconds + "\n" +
        "silentSeconds: " + fields.SilentSeconds + "\n" +
        "gamePath: " + fields.GamePath + "\n" +
        "unityVersion: " + fields.UnityVersion + "\n" +
        "scene: " + fields.Scene + "\n" +
        "workingSetMb: " + fields.WorkingSetMb + "\n" +
        "plugins: " + fields.Plugins + "\n" +
        "mods: " + fields.Mods + "\n" +
        "dump: " + fields.Dump + "\n" +
        "dumpError: " + fields.DumpError + "\n";

    public static void WriteHeartbeat(string directory, string text) =>
        WriteAtomic(Path.Combine(directory, "heartbeat.txt"), text);

    public static void WriteSummary(string directory, string text) =>
        WriteAtomic(Path.Combine(directory, "summary.txt"), text);

    public static void WriteAtomic(string path, string text)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var tmp = path + ".writing";
        File.WriteAllText(tmp, text, Utf8);
        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }
}
