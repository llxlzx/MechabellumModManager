using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class GameDetectorTests
{
    [Fact]
    public void Missing_exe_is_GameMissing()
    {
        var root = CreateTempGame(exe: false, ga: false, melonDir: false, proxy: false);
        try
        {
            var s = new GameDetector().Detect(root);
            s.Kind.Should().Be(GameStatusKind.GameMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Game_without_loader_is_GameOkLoaderMissing()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: false, proxy: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.GameOkLoaderMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Melon_dir_without_proxy_is_LoaderPartial()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.LoaderPartial);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Melon_with_proxy_but_no_assemblies_is_AssembliesMissing()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: false);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.LoaderPresentAssembliesMissing);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Full_install_is_Ready()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            new GameDetector().Detect(root).Kind.Should().Be(GameStatusKind.Ready);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Ready_message_includes_below_bundled_version_from_latest_log()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            WriteLatestLog(root, "0.7.1", DateTime.Now.AddDays(-8));
            var s = new GameDetector().Detect(root);
            s.Kind.Should().Be(GameStatusKind.Ready);
            s.MelonLoaderVersion.Should().Be("0.7.1");
            s.LatestLogAge.Should().NotBeNull();
            s.LatestLogAge!.Value.TotalHours.Should().BeGreaterThan(24);
            s.Message.Should().Contain("Melon 0.7.1");
            s.Message.Should().Contain("低于内置 0.7.3");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void After_launch_stale_latest_log_is_not_injected()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            var launchedAt = DateTimeOffset.Now.AddMinutes(-5);
            WriteLatestLog(root, "0.7.1", launchedAt.LocalDateTime.AddDays(-8));
            var s = new GameDetector().Detect(root, lastLaunchRequestedAt: launchedAt);
            s.Kind.Should().Be(GameStatusKind.Ready);
            s.LoaderInjected.Should().BeFalse();
            s.Message.Should().Be("Loader 未在本次启动注入");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void After_launch_fresh_latest_log_stays_ready()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            var launchedAt = DateTimeOffset.Now.AddMinutes(-5);
            WriteLatestLog(root, "0.7.3", launchedAt.LocalDateTime.AddSeconds(20));
            var s = new GameDetector().Detect(root, lastLaunchRequestedAt: launchedAt);
            s.Kind.Should().Be(GameStatusKind.Ready);
            s.LoaderInjected.Should().NotBe(false);
            s.Message.Should().NotContain("未在本次启动注入");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Diagnostic_timeline_generation_log_before_last_launch_is_not_injected()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            var applyStarted = new DateTimeOffset(2026, 9, 7, 18, 11, 36, TimeSpan.FromHours(8));
            var lastLaunch = new DateTimeOffset(2026, 9, 7, 18, 12, 40, 556, TimeSpan.FromHours(8));
            var logWrite = new DateTimeOffset(2026, 9, 7, 18, 12, 28, 290, TimeSpan.FromHours(8));
            var now = new DateTimeOffset(2026, 9, 7, 18, 13, 1, TimeSpan.FromHours(8));
            WriteLatestLog(root, "0.7.3", logWrite.LocalDateTime, "6 Mods loaded.\n");

            var s = new GameDetector().Detect(
                root,
                lastLaunchRequestedAt: lastLaunch,
                now: now,
                applyStartedAt: applyStarted,
                gameAlreadyRunning: true);

            s.Kind.Should().Be(GameStatusKind.Ready);
            s.LoaderInjected.Should().BeFalse();
            s.Message.Should().Contain("未在本次启动注入");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Already_running_generation_log_before_last_launch_is_not_injected()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            var lastLaunch = new DateTimeOffset(2026, 9, 7, 18, 12, 40, 556, TimeSpan.FromHours(8));
            var logWrite = new DateTimeOffset(2026, 9, 7, 18, 12, 28, 290, TimeSpan.FromHours(8));
            var now = new DateTimeOffset(2026, 9, 7, 18, 13, 1, TimeSpan.FromHours(8));
            WriteLatestLog(root, "0.7.3", logWrite.LocalDateTime, "6 Mods loaded.\n");

            var s = new GameDetector().Detect(
                root,
                lastLaunchRequestedAt: lastLaunch,
                now: now,
                applyStartedAt: lastLaunch,
                gameAlreadyRunning: true);

            s.LoaderInjected.Should().BeFalse();
            s.Message.Should().Contain("未在本次启动注入");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Recent_launch_within_settle_does_not_mark_not_injected()
    {
        var root = CreateTempGame(exe: true, ga: true, melonDir: true, proxy: true, assemblies: true);
        try
        {
            var launchedAt = DateTimeOffset.Now.AddSeconds(-5);
            WriteLatestLog(root, "0.7.1", DateTime.Now.AddDays(-8));
            var s = new GameDetector().Detect(root, lastLaunchRequestedAt: launchedAt);
            s.LoaderInjected.Should().NotBe(false);
            s.Message.Should().NotContain("未在本次启动注入");
        }
        finally { Directory.Delete(root, true); }
    }

    static void WriteLatestLog(string root, string version, DateTime lastWriteLocal, string extra = "")
    {
        var path = Path.Combine(root, "MelonLoader", "Latest.log");
        File.WriteAllText(path, $"[14:17:52.025] MelonLoader v{version} Open-Beta\n{extra}");
        File.SetLastWriteTime(path, lastWriteLocal);
    }

    static string CreateTempGame(bool exe, bool ga, bool melonDir, bool proxy, bool assemblies = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-game-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (exe) File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
        if (ga) File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
        if (melonDir) Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
        if (proxy) File.WriteAllText(Path.Combine(root, "version.dll"), "");
        if (assemblies)
        {
            var il2cpp = Path.Combine(root, "MelonLoader", "Il2CppAssemblies");
            Directory.CreateDirectory(il2cpp);
            File.WriteAllText(Path.Combine(il2cpp, "Assembly-CSharp.dll"), "asm");
        }
        return root;
    }
}
