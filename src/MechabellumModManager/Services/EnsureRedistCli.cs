using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Headless redist fill for thin Setup:
/// MechabellumModManager.exe --ensure-redist --redist-dir "..." [--mirror-base-url "..."] [--ids a,b] [--progress-file "..."]
/// </summary>
public static class EnsureRedistCli
{
    public const string Arg = "--ensure-redist";

    public static bool TryParseArgs(
        string[] args,
        out string? redistDir,
        out string? mirrorBaseUrl,
        out string[]? ids,
        out bool noMirror,
        out string? progressFile)
    {
        redistDir = null;
        mirrorBaseUrl = null;
        ids = null;
        noMirror = false;
        progressFile = null;
        if (args is null || args.Length == 0)
            return false;
        if (!args.Any(a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase)))
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--redist-dir", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
                redistDir = args[++i];
            else if (string.Equals(args[i], "--mirror-base-url", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
                mirrorBaseUrl = args[++i];
            else if (string.Equals(args[i], "--ids", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
            {
                ids = args[++i]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            else if (string.Equals(args[i], "--no-mirror", StringComparison.OrdinalIgnoreCase))
                noMirror = true;
            else if (string.Equals(args[i], "--progress-file", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
                progressFile = args[++i];
        }

        return !string.IsNullOrWhiteSpace(redistDir);
    }

    /// <returns>0 ok, 1 fail, 4 bad args</returns>
    public static int Run(
        string redistDir,
        string? mirrorBaseUrl,
        string[]? ids,
        bool noMirror,
        string? progressFile = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(redistDir))
            {
                Log("missing --redist-dir");
                return 4;
            }

            var mirror = noMirror
                ? ""
                : string.IsNullOrWhiteSpace(mirrorBaseUrl)
                    ? DomesticMirrorDefaults.BaseUrl
                    : mirrorBaseUrl.Trim();

            Log($"ensure redist → {redistDir}");
            Log($"mirror={(mirror.Length == 0 ? "(none)" : mirror)}");

            IProgress<RedistProgress>? progress = null;
            if (!string.IsNullOrWhiteSpace(progressFile))
            {
                var path = progressFile.Trim();
                // Do not use Progress<T> — it marshals to SynchronizationContext and can stall
                // headless --ensure-redist under WPF App.OnStartup.
                progress = new SyncFileProgress(path);
            }

            var svc = new RedistEnsureService();
            var result = svc.EnsureAsync(redistDir.Trim(), mirror, ids, progress: progress)
                .GetAwaiter()
                .GetResult();
            Log(result.Message);
            foreach (var (id, source) in result.SourceById)
                Log($"  {id}: {source}");
            var code = result.Success ? 0 : 1;
            WriteExitCodeFile(progressFile, code);
            return code;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            WriteExitCodeFile(progressFile, 1);
            return 1;
        }
    }

    /// <summary>
    /// Sidecar for thin Setup. Exec ewNoWait's ResultCode is STILL_ACTIVE (259), not a PID,
    /// so the installer must not OpenProcess it. It waits on this file instead.
    /// </summary>
    internal static void WriteExitCodeFile(string? progressFile, int code)
    {
        if (string.IsNullOrWhiteSpace(progressFile))
            return;
        try
        {
            var path = progressFile.Trim() + ".exit";
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, code.ToString());
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            /* Setup times out if this never appears */
        }
    }

    sealed class SyncFileProgress : IProgress<RedistProgress>
    {
        readonly string _path;

        public SyncFileProgress(string path) => _path = path;

        public void Report(RedistProgress value) => WriteProgressFile(_path, value);
    }

    static void WriteProgressFile(string path, RedistProgress p)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Format: percent|artifactId|detail
            var line = $"{Math.Clamp(p.Percent, 0, 100)}|{p.ArtifactId}|{p.Detail}";
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, line);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            /* best effort — Setup may still show static status */
        }
    }

    static void Log(string message)
    {
        try
        {
            Console.Error.WriteLine("[ensure-redist] " + message);
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MechabellumModManager",
                "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "ensure-redist.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            /* best effort */
        }
    }
}
