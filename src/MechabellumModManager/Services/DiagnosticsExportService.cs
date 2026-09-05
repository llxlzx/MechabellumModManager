using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class DiagnosticsExportRequest
{
    public required PathsService Paths { get; init; }
    public string GamePath { get; init; } = "";
    public string SessionLogText { get; init; } = "";
    public string AppVersion { get; init; } = "";
    public string? GameStatusKind { get; init; }
    public string? GameStatusMessage { get; init; }
    public bool BranchSwitchEnabled { get; init; }
    public string? BranchWizardStep { get; init; }
    public string? ActiveGameBranch { get; init; }
    public string? LaunchMode { get; init; }
    public bool? GameRunning { get; init; }
    public bool? SteamRunning { get; init; }
    public bool IsAwaitingSteamSettle { get; init; }
    public DiagnosticsRedactionMode Redaction { get; init; }
    public ManagerLogWriter? LogWriter { get; init; }
}

public sealed class DiagnosticsExportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? ZipPath { get; init; }
    public IReadOnlyList<string> Missing { get; init; } = Array.Empty<string>();
}

public sealed class DiagnosticsExportService
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DiagnosticsExportResult ExportToFile(string zipPath, DiagnosticsExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            return Fail("Zip path is empty.");

        var missing = new List<string>();
        var staging = Path.Combine(Path.GetTempPath(), "mmm-diag-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(Path.Combine(staging, "logs"));
            Directory.CreateDirectory(Path.Combine(staging, "melon"));

            var redact = request.Redaction == DiagnosticsRedactionMode.Strong;

            WriteText(staging, "logs/session.log", request.SessionLogText ?? "", redact);

            var logWriter = request.LogWriter ?? new ManagerLogWriter(request.Paths.LogsDir);
            foreach (var logFile in logWriter.ListRecentLogFiles())
            {
                TryCopyText(staging, "logs/" + Path.GetFileName(logFile), logFile, missing, redact);
            }

            TryCopyText(staging, "config.json", request.Paths.ConfigPath, missing, redact);
            TryCopyText(staging, "branch-switch.json", request.Paths.BranchSwitchConfigPath, missing, redact);
            TryCopyText(staging, "branch-switch-journal.json", request.Paths.BranchSwitchJournalPath, missing, redact);

            foreach (var name in new[]
                     {
                         "deploy-manifest.json",
                         "deploy-manifest.prev.json",
                         "deploy-manifest.official.json",
                         "deploy-manifest.official.prev.json",
                         "deploy-manifest.beta.json",
                         "deploy-manifest.beta.prev.json"
                     })
            {
                TryCopyText(staging, name, Path.Combine(request.Paths.DataRoot, name), missing, redact);
            }

            var dataCrash = Path.Combine(request.Paths.DataRoot, "crash.log");
            var appDataCrash = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                PathsService.AppFolderName,
                "crash.log");
            TryCopyText(staging, "logs/crash.log", dataCrash, missing, redact, recordMissing: false);
            if (!File.Exists(dataCrash) && File.Exists(appDataCrash))
                TryCopyText(staging, "logs/crash.appdata.log", appDataCrash, missing, redact);
            else if (!File.Exists(dataCrash) && !File.Exists(appDataCrash))
                missing.Add("crash.log");

            var melonLog = string.IsNullOrWhiteSpace(request.GamePath)
                ? null
                : Path.Combine(request.GamePath, "MelonLoader", "Latest.log");
            if (!string.IsNullOrWhiteSpace(melonLog) && File.Exists(melonLog))
                TryCopyText(staging, "melon/Latest.log", melonLog, missing, redact);
            else
            {
                missing.Add("melon/Latest.log");
                WriteText(staging, "melon/note.txt",
                    "MelonLoader Latest.log was not found under the current game path.",
                    redact: false);
            }

            var dataRootValue = request.Paths.DataRoot;
            var gamePathValue = request.GamePath;
            if (redact)
            {
                dataRootValue = Redact(dataRootValue);
                gamePathValue = Redact(gamePathValue ?? "");
            }

            var env = new Dictionary<string, object?>
            {
                ["appVersion"] = request.AppVersion,
                ["os"] = Environment.OSVersion.ToString(),
                ["dotnet"] = Environment.Version.ToString(),
                ["timeZone"] = TimeZoneInfo.Local.Id,
                ["exportedAt"] = DateTimeOffset.Now.ToString("o"),
                ["dataRoot"] = dataRootValue,
                ["gamePath"] = gamePathValue,
                ["gameStatusKind"] = request.GameStatusKind,
                ["gameStatusMessage"] = request.GameStatusMessage is null
                    ? null
                    : (redact ? Redact(request.GameStatusMessage) : request.GameStatusMessage),
                ["branchSwitchEnabled"] = request.BranchSwitchEnabled,
                ["branchWizardStep"] = request.BranchWizardStep,
                ["activeGameBranch"] = request.ActiveGameBranch,
                ["redaction"] = redact ? "strong" : "none",
                ["missing"] = missing
            };

            try
            {
                var probeBundle = DiagnosticsProbeBuilder.Build(request);
                foreach (var kv in probeBundle)
                    env[kv.Key] = kv.Value;
            }
            catch
            {
                env["probeError"] = true;
            }

            var envJson = JsonSerializer.Serialize(env, JsonOptions);
            // JSON escapes backslashes; run Redact again for any residual path forms in the document.
            WriteText(staging, "environment.json", redact ? RedactJsonDocument(envJson) : envJson, redact: false);

            var readme =
                "Mechabellum Mod Manager diagnostics pack\n" +
                $"Redaction: {(redact ? "strong" : "none")}\n" +
                "Send this zip to llxmod@foxmail.com (attach the file manually).\n" +
                "mailto cannot attach local files automatically.\n";
            if (missing.Count > 0)
                readme += "Missing:\n- " + string.Join("\n- ", missing) + "\n";
            WriteText(staging, "README.txt", readme, redact: false);

            var zipDir = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(zipDir))
                Directory.CreateDirectory(zipDir);
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return new DiagnosticsExportResult
            {
                Success = true,
                Message = "OK",
                ZipPath = zipPath,
                Missing = missing
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch { /* ignore */ }
        }
    }

    static DiagnosticsExportResult Fail(string message) =>
        new() { Success = false, Message = message };

    static void TryCopyText(
        string staging,
        string relative,
        string sourcePath,
        List<string> missing,
        bool redact,
        bool recordMissing = true)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                if (recordMissing)
                    missing.Add(relative);
                return;
            }

            var text = File.ReadAllText(sourcePath);
            WriteText(staging, relative, text, redact);
        }
        catch
        {
            if (recordMissing)
                missing.Add(relative);
        }
    }

    static void WriteText(string staging, string relative, string text, bool redact)
    {
        var dest = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var body = redact ? Redact(text) : text;
        File.WriteAllText(dest, body, Encoding.UTF8);
    }

    internal static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = text;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var root = Path.GetPathRoot(userProfile) ?? @"C:\";
            var maskedProfile = Path.Combine(root, "Users", "<User>");
            result = ReplaceIgnoreCase(result, userProfile, maskedProfile);
        }

        result = Regex.Replace(
            result,
            @"([A-Za-z]:\\Users\\)([^\\/]+)",
            "$1<User>",
            RegexOptions.IgnoreCase);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var mmm = Path.Combine(appData, PathsService.AppFolderName);
            result = ReplaceIgnoreCase(result, mmm, @"<AppData>\MechabellumModManager");
            result = ReplaceIgnoreCase(result, appData, "<AppData>");
        }

        return result;
    }

    /// <summary>Redact path forms inside JSON where backslashes are escaped as \\.</summary>
    internal static string RedactJsonDocument(string json)
    {
        var result = Redact(json);
        result = Regex.Replace(
            result,
            @"([A-Za-z]:\\\\Users\\\\)([^\\/""]+)",
            "$1<User>",
            RegexOptions.IgnoreCase);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var escaped = userProfile.Replace("\\", "\\\\");
            var root = Path.GetPathRoot(userProfile) ?? @"C:\";
            var masked = Path.Combine(root, "Users", "<User>").Replace("\\", "\\\\");
            result = ReplaceIgnoreCase(result, escaped, masked);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            result = ReplaceIgnoreCase(result, appData.Replace("\\", "\\\\"), "<AppData>");
            var mmm = Path.Combine(appData, PathsService.AppFolderName).Replace("\\", "\\\\");
            result = ReplaceIgnoreCase(result, mmm, @"<AppData>\\MechabellumModManager");
        }

        return result;
    }

    static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(oldValue) || string.IsNullOrEmpty(input))
            return input;
        var sb = new StringBuilder();
        var i = 0;
        while (i < input.Length)
        {
            var idx = input.IndexOf(oldValue, i, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                sb.Append(input, i, input.Length - i);
                break;
            }

            sb.Append(input, i, idx - i);
            sb.Append(newValue);
            i = idx + oldValue.Length;
        }

        return sb.ToString();
    }
}
