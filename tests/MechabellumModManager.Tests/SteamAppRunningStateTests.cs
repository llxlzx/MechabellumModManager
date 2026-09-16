using FluentAssertions;
using MechabellumModManager.Services;

public class SteamAppRunningStateTests
{
    [Fact]
    public void Stale_when_app_marked_running_and_process_is_gone()
    {
        SteamAppRunningState.IsStaleWithoutProcess(
                gameProcessRunning: false,
                appRunning: 1,
                runningAppId: SteamAppRunningState.MechabellumAppId)
            .Should().BeTrue();
    }

    [Fact]
    public void Stale_when_only_running_app_id_points_at_this_game()
    {
        SteamAppRunningState.IsStaleWithoutProcess(
                gameProcessRunning: false,
                appRunning: 0,
                runningAppId: SteamAppRunningState.MechabellumAppId)
            .Should().BeTrue();
    }

    [Fact]
    public void Not_stale_when_the_process_is_actually_running()
    {
        SteamAppRunningState.IsStaleWithoutProcess(
                gameProcessRunning: true,
                appRunning: 1,
                runningAppId: SteamAppRunningState.MechabellumAppId)
            .Should().BeFalse();
    }

    [Fact]
    public void Not_stale_when_steam_does_not_mark_this_game()
    {
        SteamAppRunningState.IsStaleWithoutProcess(
                gameProcessRunning: false,
                appRunning: 0,
                runningAppId: 730)
            .Should().BeFalse();

        SteamAppRunningState.IsStaleWithoutProcess(
                gameProcessRunning: false,
                appRunning: null,
                runningAppId: null)
            .Should().BeFalse();
    }
}
