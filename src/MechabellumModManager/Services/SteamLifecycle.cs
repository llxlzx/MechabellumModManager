namespace MechabellumModManager.Services;

public interface ISteamLifecycle
{
    bool IsClientRunning();
    bool IsGameRunning();

    /// <summary>
    /// Soft steam://exit (only if client running) → wait → ForceClose → wait → cooldown recheck.
    /// Never starts Steam.
    /// </summary>
    Task<bool> EnsureClientAndGameExitedAsync(CancellationToken cancellationToken = default);

    /// <summary>Log + optional notify. Must not StartShell any steam:// URI.</summary>
    void PreferManualOpen(string message);
}

public sealed class SteamLifecycle : ISteamLifecycle
{
    readonly IProcessProbe _probe;
    readonly IProcessStarter _starter;
    readonly Func<TimeSpan, Task> _delay;
    readonly TimeSpan _exitTimeout;
    readonly TimeSpan _exitCooldown;
    readonly Action<string>? _log;
    readonly Action<string>? _notify;

    public SteamLifecycle(
        IProcessProbe probe,
        IProcessStarter starter,
        Func<TimeSpan, Task>? delay = null,
        TimeSpan? exitTimeout = null,
        TimeSpan? exitCooldown = null,
        Action<string>? log = null,
        Action<string>? notify = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _starter = starter ?? throw new ArgumentNullException(nameof(starter));
        _delay = delay ?? (ms => Task.Delay(ms));
        _exitTimeout = exitTimeout ?? TimeSpan.FromSeconds(45);
        _exitCooldown = exitCooldown ?? TimeSpan.FromSeconds(3);
        _log = log;
        _notify = notify;
    }

    public bool IsClientRunning() => _probe.IsSteamRunning();

    public bool IsGameRunning() => _probe.IsGameRunning();

    public void PreferManualOpen(string message)
    {
        _log?.Invoke(message);
        _notify?.Invoke(message);
    }

    public async Task<bool> EnsureClientAndGameExitedAsync(CancellationToken cancellationToken = default)
    {
        if (!_probe.IsGameOrSteamRunning())
            return true;

        // Only invoke steam://exit when a Steam *client* process exists.
        // Calling the protocol with Steam fully closed starts Steam (account picker).
        if (_probe.IsSteamRunning())
        {
            try
            {
                _starter.StartShell("steam://exit");
            }
            catch (Exception ex)
            {
                _log?.Invoke(string.Format(LocalizationService.T("LogSteamExitRequestFailed"), ex.Message));
            }
        }

        var deadline = DateTime.UtcNow + _exitTimeout;
        var softSeconds = Math.Min(8, Math.Max(0, _exitTimeout.TotalSeconds));
        var softDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(softSeconds);

        while (_probe.IsGameOrSteamRunning() && DateTime.UtcNow < softDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        // steam://exit often does nothing on the “Who’s playing?” account picker.
        if (_probe.IsGameOrSteamRunning())
        {
            _log?.Invoke(LocalizationService.T("LogSteamForceCloseClient"));
            _probe.ForceCloseSteamClientAndGame();
        }

        while (_probe.IsGameOrSteamRunning() && DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        if (_probe.IsGameOrSteamRunning())
        {
            _log?.Invoke(LocalizationService.T("LogSteamOrGameStillRunning"));
            return false;
        }

        if (_exitCooldown > TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _delay(_exitCooldown).ConfigureAwait(false);
        }

        // Re-check after cooldown: steamwebhelper often lags behind steam.exe.
        if (_probe.IsGameOrSteamRunning())
        {
            _log?.Invoke(LocalizationService.T("LogSteamOrGameStillRunning"));
            return false;
        }

        return true;
    }
}
