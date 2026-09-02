using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class GameLauncherTests
{
    [Fact]
    public void SteamThenExe_starts_steam_uri_first()
    {
        var starter = new FakeProcessStarter();
        var launcher = new GameLauncher(starter, () => false);
        var cfg = new AppConfig
        {
            GamePath = @"D:\games\Mechabellum",
            LaunchMode = LaunchMode.SteamThenExe
        };

        var result = launcher.Launch(cfg);

        result.Success.Should().BeTrue();
        starter.Started.Should().Equal("steam://rungameid/669330");
    }

    [Fact]
    public void SteamThenExe_falls_back_to_exe_when_steam_fails()
    {
        var starter = new FakeProcessStarter { FailOn = "steam://rungameid/669330" };
        var launcher = new GameLauncher(starter, () => false);
        var gamePath = @"D:\games\Mechabellum";
        var cfg = new AppConfig { GamePath = gamePath, LaunchMode = LaunchMode.SteamThenExe };

        var result = launcher.Launch(cfg);

        result.Success.Should().BeTrue();
        starter.Started.Should().Equal(
            "steam://rungameid/669330",
            Path.Combine(gamePath, "Mechabellum.exe"));
    }

    [Fact]
    public void Already_running_returns_friendly_failure_without_start()
    {
        var starter = new FakeProcessStarter();
        var launcher = new GameLauncher(starter, () => true);
        var cfg = new AppConfig { LaunchMode = LaunchMode.SteamThenExe };

        var result = launcher.Launch(cfg);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("已在运行");
        starter.Started.Should().BeEmpty();
    }

    [Fact]
    public void ExeOnly_starts_exe_path()
    {
        var starter = new FakeProcessStarter();
        var launcher = new GameLauncher(starter, () => false);
        var gamePath = @"C:\Mechabellum";
        var cfg = new AppConfig { GamePath = gamePath, LaunchMode = LaunchMode.ExeOnly };

        launcher.Launch(cfg).Success.Should().BeTrue();
        starter.Started.Should().Equal(Path.Combine(gamePath, "Mechabellum.exe"));
    }

    sealed class FakeProcessStarter : IProcessStarter
    {
        public string? FailOn { get; init; }
        public List<string> Started { get; } = new();

        public void StartShell(string uriOrPath)
        {
            Started.Add(uriOrPath);
            if (FailOn is not null && uriOrPath == FailOn)
                throw new InvalidOperationException("simulated start failure");
        }
    }
}
