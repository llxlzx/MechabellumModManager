using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed class CriticalOpGuard
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PathsService _paths;
    private readonly Action<string>? _log;
    private CriticalOpKind _runningKind = CriticalOpKind.None;

    public CriticalOpGuard(PathsService paths, Action<string>? log = null)
    {
        _paths = paths;
        _log = log;
    }

    public bool IsRunning => _runningKind != CriticalOpKind.None;

    public CriticalOpKind RunningKind => _runningKind;

    public void Begin(CriticalOpKind kind, string detail)
    {
        _runningKind = kind;
        try
        {
            Directory.CreateDirectory(_paths.DataRoot);
            var marker = new CriticalOpMarker
            {
                Kind = kind,
                StartedUtc = DateTimeOffset.UtcNow,
                Detail = detail,
                Pid = Environment.ProcessId
            };
            var json = JsonSerializer.Serialize(marker, JsonOptions);
            File.WriteAllText(_paths.CriticalOpMarkerPath, json);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"CriticalOpGuard.Begin IO failed: {ex.Message}");
        }
    }

    public void Complete()
    {
        _runningKind = CriticalOpKind.None;
        try
        {
            if (File.Exists(_paths.CriticalOpMarkerPath))
                File.Delete(_paths.CriticalOpMarkerPath);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"CriticalOpGuard.Complete IO failed: {ex.Message}");
        }
    }

    public bool TryLoadInterrupted(out CriticalOpMarker? marker)
    {
        marker = null;
        if (IsRunning)
            return false;

        if (!File.Exists(_paths.CriticalOpMarkerPath))
            return false;

        try
        {
            var json = File.ReadAllText(_paths.CriticalOpMarkerPath);
            marker = JsonSerializer.Deserialize<CriticalOpMarker>(json, JsonOptions);
            if (marker is null)
            {
                marker = CorruptMarker();
                return true;
            }

            return true;
        }
        catch
        {
            marker = CorruptMarker();
            return true;
        }
    }

    public void ClearInterrupted()
    {
        if (IsRunning)
            return;

        try
        {
            if (File.Exists(_paths.CriticalOpMarkerPath))
                File.Delete(_paths.CriticalOpMarkerPath);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"CriticalOpGuard.ClearInterrupted IO failed: {ex.Message}");
        }
    }

    private static CriticalOpMarker CorruptMarker() =>
        new() { Kind = CriticalOpKind.BranchDiskWrite, Detail = "corrupt" };
}
