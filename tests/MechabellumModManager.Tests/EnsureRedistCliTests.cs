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

    [Fact]
    public void Route_mirror_flag_is_parsed()
    {
        EnsureRedistCli.TryParseArgs(
                [EnsureRedistCli.Arg, "--redist-dir", @"C:\r", "--route-mirror"],
                out _, out _, out _, out var noMirror, out _, out var routeMirror)
            .Should().BeTrue();

        routeMirror.Should().BeTrue();
        noMirror.Should().BeFalse();
    }

    [Fact]
    public void Route_follows_the_flags_and_only_probes_when_neither_is_set()
    {
        var probes = 0;
        DownloadRouteKind Probe()
        {
            probes++;
            return DownloadRouteKind.GitHubOnly;
        }

        EnsureRedistCli.ResolveRoute(noMirror: true, routeMirror: false, Probe).Should().Be(DownloadRouteKind.GitHubOnly);
        EnsureRedistCli.ResolveRoute(noMirror: false, routeMirror: true, Probe).Should().Be(DownloadRouteKind.MirrorFirst);
        probes.Should().Be(0);

        EnsureRedistCli.ResolveRoute(noMirror: false, routeMirror: false, Probe).Should().Be(DownloadRouteKind.GitHubOnly);
        probes.Should().Be(1);
    }
}
