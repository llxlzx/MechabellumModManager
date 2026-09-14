using FluentAssertions;

/// <summary>
/// Guards against Thin Setup hanging on "写入管理器配置" while MelonLoader
/// downloads from GitHub before COS EnsureRedist fills the local zip.
/// </summary>
public class InstallerMelonHangGuardsTests
{
    static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    static string ReadRepoFile(params string[] parts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());
        File.Exists(path).Should().BeTrue(because: path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Restore_skips_melon_ensure_when_local_zip_missing()
    {
        var text = ReadRepoFile("installer", "scripts", "Restore-DualFolderConfig.ps1");
        text.Should().Contain("local zip not ready yet");
        var gate = text.IndexOf("local zip not ready yet", StringComparison.Ordinal);
        var call = text.IndexOf("Ensuring MelonLoader ($Why)", StringComparison.Ordinal);
        gate.Should().BeGreaterThanOrEqualTo(0);
        call.Should().BeGreaterThan(gate, "skip gate must run before invoking Install-MelonLoader");
    }

    [Fact]
    public void Melon_and_prereqs_github_downloads_use_timeout()
    {
        ReadRepoFile("installer", "scripts", "Install-MelonLoader.ps1")
            .Should().Contain("TimeoutSec 90");
        ReadRepoFile("installer", "scripts", "Install-Prereqs.ps1")
            .Should().Contain("TimeoutSec 90");
    }

    [Fact]
    public void Iss_re_runs_restore_after_ensure_redist_so_dual_melon_can_use_local_zip()
    {
        var iss = ReadRepoFile("installer", "MechabellumModManager.iss");
        var post = iss.IndexOf("procedure CurStepChanged", StringComparison.Ordinal);
        post.Should().BeGreaterThanOrEqualTo(0);

        var firstRestore = iss.IndexOf("Restore-DualFolderConfig.ps1", post, StringComparison.Ordinal);
        var ensureRedist = iss.IndexOf("EnsureRedistViaApp(Redist)", post, StringComparison.Ordinal);
        firstRestore.Should().BeGreaterThan(post);
        ensureRedist.Should().BeGreaterThan(firstRestore, "config restore must stay before EnsureRedist");

        var secondRestore = iss.IndexOf("Restore-DualFolderConfig.ps1", ensureRedist, StringComparison.Ordinal);
        secondRestore.Should().BeGreaterThan(ensureRedist,
            "after EnsureRedist, Restore must run again so dual-store Ensure-Melon sees MelonLoader.x64.zip");
    }
}
