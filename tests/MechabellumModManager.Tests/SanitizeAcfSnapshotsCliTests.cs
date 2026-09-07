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
    public void Run_skips_when_dir_missing_without_throwing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mmm-sanitize-missing-" + Guid.NewGuid().ToString("N"));
        SanitizeAcfSnapshotsCli.Run(missing, "public_test").Should().Be(0);
        Directory.Exists(missing).Should().BeFalse();
    }

    [Fact]
    public void Powershell_fallback_exits_zero_when_dir_missing()
    {
        var script = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "installer", "scripts", "Sanitize-AcfSnapshots.ps1"));
        File.Exists(script).Should().BeTrue(because: script);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                        script + "\" -SnapshotsDir \"" +
                        Path.Combine(Path.GetTempPath(), "mmm-ps-no-acf-" + Guid.NewGuid().ToString("N")) + "\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = System.Diagnostics.Process.Start(psi);
        p.Should().NotBeNull();
        p!.WaitForExit(15000).Should().BeTrue("missing snapshot dir must not hang Setup");
        p.ExitCode.Should().Be(0);
    }

    [Fact]
    public void Timed_cli_wrapper_exits_when_exe_missing()
    {
        var script = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "installer", "scripts", "Run-SanitizeAcfCli.ps1"));
        File.Exists(script).Should().BeTrue(because: script);

        var missingExe = Path.Combine(Path.GetTempPath(), "mmm-no-exe-" + Guid.NewGuid().ToString("N") + ".exe");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                        script + "\" -ExePath \"" + missingExe + "\" -TimeoutSec 5",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = System.Diagnostics.Process.Start(psi);
        p.Should().NotBeNull();
        p!.WaitForExit(15000).Should().BeTrue("missing exe must not hang Setup");
        p.ExitCode.Should().NotBe(0);
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
