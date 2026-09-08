using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Headless redist fill for thin Setup:
/// MechabellumModManager.exe --ensure-redist --redist-dir "..." [--mirror-base-url "..."] [--ids a,b]
/// </summary>
public static class EnsureRedistCli
{
    public const string Arg = "--ensure-redist";

    public static bool TryParseArgs(
        string[] args,
        out string? redistDir,
        out string? mirrorBaseUrl,
        out string[]? ids,
        out bool noMirror)
    {
        redistDir = null;
        mirrorBaseUrl = null;
        ids = null;
        noMirror = false;
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
        }

        return !string.IsNullOrWhiteSpace(redistDir);
    }

    /// <returns>0 ok, 1 fail, 4 bad args</returns>
    public static int Run(string redistDir, string? mirrorBaseUrl, string[]? ids, bool noMirror)
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

            var svc = new RedistEnsureService();
            var result = svc.EnsureAsync(redistDir.Trim(), mirror, ids).GetAwaiter().GetResult();
            Log(result.Message);
            foreach (var (id, source) in result.SourceById)
                Log($"  {id}: {source}");
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            return 1;
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
