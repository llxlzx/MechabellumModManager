using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

public class SteamLifecycleTests
{
    sealed class RecordingStarter : IProcessStarter
    {
        public List<string> Starts { get; } = new();
        public void StartShell(string uriOrPath) => Starts.Add(uriOrPath);
    }

    [Fact]
    public async Task EnsureExited_when_already_stopped_does_not_call_exit_or_force()
    {
        var probe = new FakeProcessProbe();
        var starter = new RecordingStarter();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.Zero, exitCooldown: TimeSpan.Zero);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeTrue();
        starter.Starts.Should().BeEmpty();
        probe.ForceCloseCalls.Should().Be(0);
    }

    [Fact]
    public async Task EnsureExited_when_client_running_requests_exit_then_force_close()
    {
        var probe = new FakeProcessProbe { SteamRunning = true, ForceCloseClearsRunning = true };
        var starter = new RecordingStarter();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.FromSeconds(1), exitCooldown: TimeSpan.Zero);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeTrue();
        starter.Starts.Should().Contain("steam://exit");
        starter.Starts.Should().NotContain(s => s.Contains("open", StringComparison.OrdinalIgnoreCase));
        probe.ForceCloseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EnsureExited_when_only_game_running_skips_steam_exit_but_force_closes()
    {
        var probe = new FakeProcessProbe { GameRunning = true, ForceCloseClearsRunning = true };
        var starter = new RecordingStarter();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.FromSeconds(1), exitCooldown: TimeSpan.Zero);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeTrue();
        starter.Starts.Should().NotContain("steam://exit");
        probe.ForceCloseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EnsureExited_returns_false_if_still_running_after_force()
    {
        var probe = new FakeProcessProbe { SteamRunning = true, ForceCloseClearsRunning = false };
        var starter = new RecordingStarter();
        var logs = new List<string>();
        var life = new SteamLifecycle(probe, starter, _ => Task.CompletedTask,
            exitTimeout: TimeSpan.Zero, exitCooldown: TimeSpan.Zero, log: logs.Add);

        (await life.EnsureClientAndGameExitedAsync()).Should().BeFalse();
        probe.ForceCloseCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PreferManualOpen_never_starts_shell()
    {
        var starter = new RecordingStarter();
        var logs = new List<string>();
        var notes = new List<string>();
        var life = new SteamLifecycle(new FakeProcessProbe(), starter, log: logs.Add, notify: notes.Add);

        life.PreferManualOpen("please open steam yourself");

        starter.Starts.Should().BeEmpty();
        logs.Should().ContainSingle(x => x.Contains("please open steam", StringComparison.Ordinal));
        notes.Should().ContainSingle();
    }
}
