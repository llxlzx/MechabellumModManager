using System.IO;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

/// <summary>
/// Append-only JSONL decision events under LogsDir. Write failures never break the UI path.
/// </summary>
public sealed class ManagerEventLog
{
    public const string FileName = "events.jsonl";
    public const string UpgradeSkipped = "upgrade_skipped";
    public const string CatalogFetchFailed = "catalog_fetch_failed";
    public const string UpdateCheckFailed = "update_check_failed";
    public const string SettleAbandoned = "settle_abandoned";
    public const string BetaKeyWriteFailed = "betakey_write_failed";
    public const string LaunchRequested = "launch_requested";
    public const string LoaderNotInjected = "loader_not_injected";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    readonly object _gate = new();
    readonly string _logsDir;

    public ManagerEventLog(string logsDir)
    {
        _logsDir = logsDir ?? throw new ArgumentNullException(nameof(logsDir));
    }

    public string FilePath => Path.Combine(_logsDir, FileName);

    public void Write(string code, IReadOnlyDictionary<string, string?>? data = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["t"] = DateTimeOffset.Now.ToString("o"),
                ["code"] = code
            };
            if (data is { Count: > 0 })
                payload["data"] = data;

            var line = JsonSerializer.Serialize(payload, JsonOptions);
            lock (_gate)
            {
                Directory.CreateDirectory(_logsDir);
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never break the UI/business path for event I/O.
        }
    }

    public IReadOnlyList<DiagnosisEvent> ReadRecent(int max = 200)
    {
        try
        {
            if (!File.Exists(FilePath))
                return Array.Empty<DiagnosisEvent>();

            string text;
            lock (_gate)
                text = File.ReadAllText(FilePath);

            var list = new List<DiagnosisEvent>();
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;
                    var code = root.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                    if (code.Length == 0)
                        continue;
                    DateTimeOffset time = DateTimeOffset.Now;
                    if (root.TryGetProperty("t", out var tEl)
                        && tEl.ValueKind == JsonValueKind.String
                        && DateTimeOffset.TryParse(tEl.GetString(), out var parsed))
                        time = parsed;

                    Dictionary<string, string?>? data = null;
                    if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                    {
                        data = new Dictionary<string, string?>(StringComparer.Ordinal);
                        foreach (var p in dataEl.EnumerateObject())
                            data[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                    }

                    list.Add(new DiagnosisEvent { Time = time, Code = code, Data = data });
                }
                catch
                {
                    // skip bad line
                }
            }

            if (list.Count <= max)
                return list;
            return list.Skip(list.Count - max).ToList();
        }
        catch
        {
            return Array.Empty<DiagnosisEvent>();
        }
    }
}
