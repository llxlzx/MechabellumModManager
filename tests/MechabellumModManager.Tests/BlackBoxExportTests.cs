using System.IO.Compression;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class BlackBoxExportTests
{
    [Fact]
    public void Missing_directory_is_absent()
    {
        using var fx = new Fixture();
        var result = Export(fx, Path.Combine(fx.Root, "game"), DiagnosticsRedactionMode.None);
        result.Success.Should().BeTrue();
        result.Missing.Should().Contain("blackbox/");
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"absent\"");
    }

    [Fact]
    public void Summary_is_redacted_and_dump_bytes_are_not()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        var user = Environment.UserName;
        File.WriteAllText(Path.Combine(box, "summary.txt"), "gamePath: C:\\Users\\" + user + "\\Games\n");
        File.WriteAllBytes(Path.Combine(box, "blackbox.dmp"), new byte[] { 9, 8, 7 });
        var result = Export(fx, game, DiagnosticsRedactionMode.Strong);
        result.Success.Should().BeTrue();
        Read(fx, "blackbox/summary.txt").Should().Contain(@"Users\<User>");
        Read(fx, "blackbox/summary.txt").Should().NotContain(user);
        ReadBytes(fx, "blackbox/blackbox.dmp").Should().Equal(9, 8, 7);
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"summary-and-dump\"");
        Read(fx, "README.txt").Should().Contain("not redacted");
        Read(fx, "summary.md").Should().Contain("## BlackBox");
    }

    [Fact]
    public void Oversize_dump_is_skipped()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        File.WriteAllText(Path.Combine(box, "summary.txt"), "dump: written\n");
        using (var stream = new FileStream(Path.Combine(box, "blackbox.dmp"), FileMode.CreateNew))
            stream.SetLength(64L * 1024 * 1024 + 1);
        var result = Export(fx, game, DiagnosticsRedactionMode.None);
        result.Missing.Should().Contain(m => m.Contains("over 64MB"));
        using var archive = ZipFile.OpenRead(Zip(fx));
        archive.GetEntry("blackbox/blackbox.dmp").Should().BeNull();
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"summary\"");
    }

    [Fact]
    public void Tmp_and_helper_are_not_zipped()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        File.WriteAllText(Path.Combine(box, "heartbeat.txt"), "scene: unknown\n");
        File.WriteAllText(Path.Combine(box, "blackbox.dmp.tmp"), "nope");
        File.WriteAllText(Path.Combine(box, "BlackBoxDump.exe"), "nope");
        var result = Export(fx, game, DiagnosticsRedactionMode.None);
        using var archive = ZipFile.OpenRead(Zip(fx));
        archive.GetEntry("blackbox/blackbox.dmp.tmp").Should().BeNull();
        archive.GetEntry("blackbox/BlackBoxDump.exe").Should().BeNull();
        Read(fx, "environment.json").Should().Contain("\"blackbox\": \"heartbeat-only\"");
        result.Missing.Should().Contain("blackbox/summary.txt");
    }

    [Fact]
    public void Large_text_keeps_the_last_8mb()
    {
        using var fx = new Fixture();
        var game = Path.Combine(fx.Root, "game");
        var box = Path.Combine(game, "UserData", "BlackBox");
        Directory.CreateDirectory(box);
        const long cap = 8L * 1024 * 1024;
        var head = Encoding.ASCII.GetBytes("HEAD-SHOULD-DROP-XXXX");
        var tail = Encoding.ASCII.GetBytes("TAIL-MARKER-BLACKBOX");
        var path = Path.Combine(box, "summary.txt");
        using (var stream = new FileStream(path, FileMode.CreateNew))
        {
            stream.Write(head);
            stream.SetLength(cap + head.Length);
            stream.Seek(-tail.Length, SeekOrigin.End);
            stream.Write(tail);
        }

        var result = Export(fx, game, DiagnosticsRedactionMode.None);
        result.Success.Should().BeTrue();
        var text = Read(fx, "blackbox/summary.txt");
        text.Should().StartWith("[truncated: kept the last 8 MB of ");
        text.Should().Contain("TAIL-MARKER-BLACKBOX");
        text.Should().NotContain("HEAD-SHOULD-DROP");
    }

    static string Zip(Fixture fx) => Path.Combine(fx.Root, "out.zip");

    static DiagnosticsExportResult Export(Fixture fx, string game, DiagnosticsRedactionMode mode) =>
        new DiagnosticsExportService().ExportToFile(Zip(fx), new DiagnosticsExportRequest
        {
            Paths = fx.Paths,
            GamePath = game,
            SessionLogText = "",
            AppVersion = "1.3.5",
            Redaction = mode,
            LogWriter = new ManagerLogWriter(fx.Paths.LogsDir)
        });

    static string Read(Fixture fx, string name)
    {
        using var archive = ZipFile.OpenRead(Zip(fx));
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    static byte[] ReadBytes(Fixture fx, string name)
    {
        using var archive = ZipFile.OpenRead(Zip(fx));
        using var stream = archive.GetEntry(name)!.Open();
        using var mem = new MemoryStream();
        stream.CopyTo(mem);
        return mem.ToArray();
    }

    sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "bb-export-" + Guid.NewGuid().ToString("N"));
        public PathsService Paths { get; }
        public Fixture()
        {
            Paths = new PathsService(Root);
            Paths.EnsureCreated();
        }
        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { }
        }
    }
}
