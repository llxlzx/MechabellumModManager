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
        result.Missing.Should().Contain("events.jsonl");
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
        env.Should().Contain("\"melonLoaderVersion\"");
        env.Should().Contain("0.7.1");
        env.Should().Contain("\"latestLogAge\"");
        env.Should().Contain("\"hasPreferencesLoaded\": false");
        archive.GetEntry("summary.md").Should().NotBeNull();
        var summary = new StreamReader(archive.GetEntry("summary.md")!.Open()).ReadToEnd();
        summary.Should().Contain("Findings");
        summary.Should().Contain("melon_log_stale_or_incomplete");
        summary.Should().Contain("建议下一步");
    }

    [Fact]
    public void Export_includes_summary_and_timeline_for_session_events()
    {
        using var fx = new Fixture();
        File.WriteAllText(fx.Paths.BranchSwitchConfigPath,
            System.Text.Json.JsonSerializer.Serialize(new BranchSwitchConfig
            {
                Enabled = true,
                WizardStep = BranchWizardStep.AwaitingSteamSettle,
                SteamLinkPath = Path.Combine(fx.Root, "steam", "Mechabellum"),
                ActiveBranch = GameBranch.Beta
            }));
        var zip = Path.Combine(fx.Root, "summary.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = Path.Combine(fx.Root, "no-game"),
            SessionLogText =
                """
                [11:25:01] 正在生成 MelonLoader 程序集…
                [11:25:31] 等待 MelonLoader 生成 Il2Cpp 程序集…
                [12:00:00] 已导出诊断包：x.zip
                """,
            AppVersion = "1.1.3",
            BranchSwitchEnabled = true,
            BranchWizardStep = "AwaitingSteamSettle",
            ActiveGameBranch = "Beta",
            IsAwaitingSteamSettle = true,
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var summary = new StreamReader(archive.GetEntry("summary.md")!.Open()).ReadToEnd();
        summary.Should().Contain("1.1.3");
        summary.Should().Contain("awaiting_steam_settle");
        summary.Should().Contain("建议下一步");

        var timeline = new StreamReader(archive.GetEntry("timeline.jsonl")!.Open()).ReadToEnd();
        timeline.Should().Contain("melon_gen_start");
        timeline.Should().Contain("diag_export");
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

    [Fact]
    public void Export_probe_flags_game_path_is_branch_store()
    {
        using var fx = new Fixture();
        var common = Path.Combine(fx.Root, "steamapps", "common");
        var official = Path.Combine(common, "Mechabellum_official");
        Directory.CreateDirectory(official);
        File.WriteAllText(Path.Combine(official, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(official, "GameAssembly.dll"), "x");

        var zip = Path.Combine(fx.Root, "store-path.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = official,
            SessionLogText = "session",
            AppVersion = "1.1.2",
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var env = new StreamReader(archive.GetEntry("environment.json")!.Open()).ReadToEnd();
        env.Should().Contain("game_path_is_branch_store");
    }

    [Fact]
    public void Export_probe_flags_leftover_dual_store_folders_when_disabled()
    {
        using var fx = new Fixture();
        var common = Path.Combine(fx.Root, "steamapps", "common");
        var link = Path.Combine(common, "Mechabellum");
        var beta = Path.Combine(common, "Mechabellum_beta");
        Directory.CreateDirectory(link);
        Directory.CreateDirectory(beta);
        File.WriteAllText(Path.Combine(link, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(link, "GameAssembly.dll"), "x");
        File.WriteAllText(Path.Combine(beta, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(beta, "GameAssembly.dll"), "x");
        File.WriteAllText(fx.Paths.BranchSwitchConfigPath,
            System.Text.Json.JsonSerializer.Serialize(new BranchSwitchConfig
            {
                Enabled = false,
                WizardStep = BranchWizardStep.None,
                SteamLinkPath = link
            }));

        var zip = Path.Combine(fx.Root, "leftover.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = link,
            SessionLogText = "session",
            AppVersion = "1.1.2",
            BranchSwitchEnabled = false,
            BranchWizardStep = "None",
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var env = new StreamReader(archive.GetEntry("environment.json")!.Open()).ReadToEnd();
        env.Should().Contain("leftover_dual_store_folders");
        env.Should().Contain("orphan_dual_layout");
        env.Should().NotContain("game_path_is_branch_store");
    }

    [Fact]
    public void Export_writes_diagnosis_json_and_summary_titled_by_primary()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        Directory.CreateDirectory(Path.Combine(game, "MelonLoader", "Il2CppAssemblies"));
        File.WriteAllText(Path.Combine(game, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "x");
        File.WriteAllText(Path.Combine(game, "version.dll"), "x");
        File.WriteAllText(Path.Combine(game, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"), "x");
        File.WriteAllText(Path.Combine(game, "MelonLoader", "Latest.log"), "MelonLoader v0.7.1 Open-Beta\n");
        File.SetLastWriteTime(Path.Combine(game, "MelonLoader", "Latest.log"), DateTime.Now.AddDays(-8));

        var zip = Path.Combine(fx.Root, "diagnosis.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = game,
            SessionLogText = "session",
            AppVersion = "1.1.3",
            GameStatusKind = "Ready",
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        var diagnosisEntry = archive.GetEntry("diagnosis.json");
        diagnosisEntry.Should().NotBeNull();
        var diagnosis = new StreamReader(diagnosisEntry!.Open()).ReadToEnd();
        diagnosis.Should().Contain("\"code\": \"melon_needs_upgrade\"");
        diagnosis.Should().Contain("dual_folder_broken");

        var summary = new StreamReader(archive.GetEntry("summary.md")!.Open()).ReadToEnd();
        summary.Should().StartWith("# ");
        summary.Should().Contain("MelonLoader");
        summary.Should().Contain("## Findings");
    }

    [Fact]
    public void Export_includes_events_jsonl_and_critical_op_when_present()
    {
        using var fx = new Fixture();
        File.WriteAllText(fx.Paths.CriticalOpMarkerPath, """{"kind":"BranchDiskWrite","detail":"SwitchToBeta","pid":1}""");
        var events = new ManagerEventLog(fx.Paths.LogsDir);
        events.Write(ManagerEventLog.LaunchRequested, new Dictionary<string, string?> { ["mode"] = "SteamThenExe" });

        var zip = Path.Combine(fx.Root, "events.zip");
        var svc = new DiagnosticsExportService();
        var result = svc.ExportToFile(zip, new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = Path.Combine(fx.Root, "no-game"),
            SessionLogText = "[12:00:00] hello without needles",
            AppVersion = "1.1.3",
            Redaction = DiagnosticsRedactionMode.None,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

        result.Success.Should().BeTrue();
        using var archive = ZipFile.OpenRead(zip);
        archive.GetEntry("critical-op.json").Should().NotBeNull();
        archive.GetEntry("events.jsonl").Should().NotBeNull();
        var timeline = new StreamReader(archive.GetEntry("timeline.jsonl")!.Open()).ReadToEnd();
        timeline.Should().Contain("launch_requested");
        var diagnosis = new StreamReader(archive.GetEntry("diagnosis.json")!.Open()).ReadToEnd();
        diagnosis.Should().Contain("critical_op_interrupted");
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
