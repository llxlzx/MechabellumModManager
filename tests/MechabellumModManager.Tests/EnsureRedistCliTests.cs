using FluentAssertions;
using MechabellumModManager.Services;

public class EnsureRedistCliTests
{
    [Fact]
    public void Exit_code_sidecar_is_written_beside_the_progress_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mmm-ensure-exit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var progress = Path.Combine(dir, "ensure-redist-progress.txt");
        try
        {
            EnsureRedistCli.WriteExitCodeFile(progress, 0);

            File.ReadAllText(progress + ".exit").Trim().Should().Be("0");
            File.Exists(progress + ".exit.tmp").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
