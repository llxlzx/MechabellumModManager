using System.IO.Compression;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DiagnosticsExportServiceTests
{
    [Fact]
    public void Export_succeeds_when_melon_log_missing_and_marks_missing()
    {
        using var fx = new Fixture();
        File.WriteAllText(fx.Paths.ConfigPath, """{"gamePath":"D:\\steam\\Mechabellum"}""");
        var zip = Path.Combine(fx.Root, "out.zip");

        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = Path.Combine(fx.Root, "no-game"),
            SessionLogText = "[12:00:00] session",
            AppVersion = "1.1.2",
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        File.Exists(zip).Should().BeTrue();
        result.Missing.Should().Contain(m => m.Contains("Latest.log", StringComparison.OrdinalIgnoreCase)
            || m.Contains("melon", StringComparison.OrdinalIgnoreCase));

        using var archive = ZipFile.OpenRead(zip);
        archive.GetEntry("logs/session.log").Should().NotBeNull();
        archive.GetEntry("environment.json").Should().NotBeNull();
        archive.GetEntry("config.json").Should().NotBeNull();
        var env = new StreamReader(archive.GetEntry("environment.json")!.Open()).ReadToEnd();
        env.Should().Contain("\"redaction\": \"none\"");
    }

    [Fact]
    public void Strong_redaction_replaces_user_profile_segments()
    {
        using var fx = new Fixture();
        var userSeg = Path.Combine("Users", Environment.UserName);
        File.WriteAllText(fx.Paths.ConfigPath,
            $"{{\"gamePath\":\"C:\\\\{userSeg}\\\\Games\\\\Mechabellum\"}}");
        var writer = new ManagerLogWriter(fx.Paths.LogsDir);
        writer.Append($"path C:\\{userSeg}\\AppData\\Roaming\\MechabellumModManager\\logs");

        var zip = Path.Combine(fx.Root, "redact.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = $@"C:\{userSeg}\Games\Mechabellum",
            SessionLogText = $@"C:\{userSeg}\secret",
            AppVersion = "1.1.2",
            Redaction = DiagnosticsRedactionMode.Strong,
            LogWriter = writer
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var session = new StreamReader(archive.GetEntry("logs/session.log")!.Open()).ReadToEnd();
        session.Should().Contain(@"Users\<User>");
        session.Should().NotContain(Environment.UserName);
        var env = new StreamReader(archive.GetEntry("environment.json")!.Open()).ReadToEnd();
        env.Should().Contain("\"redaction\": \"strong\"");
        env.Should().NotContain(Environment.UserName);
    }

    [Fact]
    public void Redact_helper_masks_Users_Name()
    {
        var raw = @"Access to C:\Users\Alice\steamapps\common\Mechabellum";
        DiagnosticsExportService.Redact(raw).Should().Contain(@"Users\<User>");
        DiagnosticsExportService.Redact(raw).Should().NotContain("Alice");
    }

    [Fact]
    public void Export_includes_probe_findings_for_stale_melon_log()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        Directory.CreateDirectory(Path.Combine(game, "MelonLoader", "Il2CppAssemblies"));
        File.WriteAllText(Path.Combine(game, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "x");
        File.WriteAllText(Path.Combine(game, "version.dll"), "x");
        File.WriteAllText(Path.Combine(game, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "x");
        var melonLog = Path.Combine(game, "MelonLoader", "Latest.log");
        File.WriteAllText(melonLog,
            """
            [14:17:52.019] ------------------------------
            [14:17:52.025] MelonLoader v0.7.1 Open-Beta
            [14:17:52.473] Game Version: 2.0.0.0.2276
            """);
        File.SetLastWriteTime(melonLog, DateTime.Now.AddHours(-2));

        var zip = Path.Combine(fx.Root, "probe.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = game,
            SessionLogText = "session",
            AppVersion = "1.1.1",
            GameStatusKind = "Ready",
            LaunchMode = "SteamThenExe",
            GameRunning = false,
            SteamRunning = false,
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var env = new StreamReader(archive.GetEntry("environment.json")!.Open()).ReadToEnd();
        env.Should().Contain("\"findings\"");
        env.Should().Contain("melon_log_stale_or_incomplete");
        env.Should().Contain("\"staleOrIncomplete\": true");
        env.Should().Contain("\"launchMode\": \"SteamThenExe\"");
        env.Should().Contain("\"hasPreferencesLoaded\": false");
    }

    [Fact]
    public void Export_probe_flags_acf_not_settled_when_branch_enabled()
    {
        using var fx = new Fixture();
        var steamapps = Path.Combine(fx.Root, "steamapps");
        var common = Path.Combine(steamapps, "common");
        var link = Path.Combine(common, "Mechabellum");
        var official = Path.Combine(common, "Mechabellum_official");
        Directory.CreateDirectory(official);
        File.WriteAllText(Path.Combine(official, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(official, "GameAssembly.dll"), "x");
        new JunctionService().CreateJunction(link, official);
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_669330.acf"),
            """
            "AppState"
            {
            	"appid"		"669330"
            	"StateFlags"		"1190"
            	"buildid"		"1"
            	"TargetBuildID"		"2"
            	"BytesToDownload"		"100"
            	"BytesDownloaded"		"1"
            	"UserConfig"
            	{
            		"BetaKey"		"public_test"
            	}
            }
            """);
        File.WriteAllText(fx.Paths.BranchSwitchConfigPath,
            System.Text.Json.JsonSerializer.Serialize(new BranchSwitchConfig
            {
                Enabled = true,
                WizardStep = BranchWizardStep.Ready,
                SteamLinkPath = link,
                OfficialStorePath = official,
                BetaStorePath = Path.Combine(common, "Mechabellum_beta"),
                ActiveBranch = GameBranch.Beta,
                BetaBranchName = "public_test"
            }));

        var zip = Path.Combine(fx.Root, "branch-probe.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = link,
            SessionLogText = "session",
            AppVersion = "1.1.1",
            BranchSwitchEnabled = true,
            BranchWizardStep = "Ready",
            ActiveGameBranch = "Beta",
            IsAwaitingSteamSettle = false,
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var env = new StreamReader(archive.GetEntry("environment.json")!.Open()).ReadToEnd();
        env.Should().Contain("acf_not_settled");
        env.Should().Contain("ready_without_branch_deploy_manifest");
        env.Should().Contain("\"settled\": false");
    }

    sealed class Fixture : IDisposable
    {
        public string Root { get; }
        public PathsService Paths { get; }

        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mmm-diag-" + Guid.NewGuid().ToString("N"));
            Paths = new PathsService(Root);
            Paths.EnsureCreated();
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { /* ignore */ }
        }
    }
}
