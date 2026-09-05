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
