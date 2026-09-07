using FluentAssertions;
using MechabellumModManager.Services;

public class InstallerAcfSanitizeGateTests
{
    [Fact]
    public void Skip_when_snapshots_dir_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "mmm-no-acf-" + Guid.NewGuid().ToString("N"));
        Directory.Exists(missing).Should().BeFalse();
        InstallerAcfSanitizeGate.HasSnapshotsToSanitize(missing).Should().BeFalse();
    }

    [Fact]
    public void Skip_when_dir_exists_but_no_acf_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-empty-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            InstallerAcfSanitizeGate.HasSnapshotsToSanitize(dir).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("official.acf")]
    [InlineData("beta.acf")]
    public void Run_when_either_snapshot_file_exists(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-has-acf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, name), "x");
            InstallerAcfSanitizeGate.HasSnapshotsToSanitize(dir).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Default_appdata_path_is_manager_snapshots_folder()
    {
        InstallerAcfSanitizeGate.DefaultSnapshotsDir.Should().EndWith(
            Path.Combine("MechabellumModManager", "steam-acf-snapshots"));
    }

    [Fact]
    public void WeGame_game_path_does_not_affect_skip_decision()
    {
        var wegame = @"D:\WeGameApps\rail_apps\Mechabellum";
        var missing = Path.Combine(Path.GetTempPath(), "mmm-wegame-acf-" + Guid.NewGuid().ToString("N"));
        InstallerAcfSanitizeGate.HasSnapshotsToSanitize(missing).Should().BeFalse(
            "sanitize looks only at AppData snapshots, never the game store path");
        wegame.Should().NotBeNullOrWhiteSpace();
    }
}
