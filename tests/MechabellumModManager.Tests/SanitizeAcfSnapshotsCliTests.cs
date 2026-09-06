using FluentAssertions;
using MechabellumModManager.Services;

public class SanitizeAcfSnapshotsCliTests
{
    [Fact]
    public void TryParseArgs_reads_optional_dirs()
    {
        var ok = SanitizeAcfSnapshotsCli.TryParseArgs(
            ["--sanitize-acf-snapshots", "--snapshots-dir", @"C:\snap", "--beta-branch-name", "public_test"],
            out var dir, out var beta);
        ok.Should().BeTrue();
        dir.Should().Be(@"C:\snap");
        beta.Should().Be("public_test");
    }

    [Fact]
    public void TryParseArgs_false_without_flag()
    {
        SanitizeAcfSnapshotsCli.TryParseArgs(["--snapshots-dir", @"C:\x"], out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Run_deletes_dirty_official_in_custom_dir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-sanitize-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "official.acf"),
                "\"AppState\"\n{\n\t\"UserConfig\"\n\t{\n\t\t\"BetaKey\"\t\t\"public_test\"\n\t}\n}\n");
            SanitizeAcfSnapshotsCli.Run(dir, "public_test").Should().Be(0);
            File.Exists(Path.Combine(dir, "official.acf")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
