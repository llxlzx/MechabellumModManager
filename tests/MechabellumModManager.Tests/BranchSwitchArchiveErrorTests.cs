using FluentAssertions;
using MechabellumModManager.Services;

public class BranchSwitchArchiveErrorTests
{
    [Fact]
    public void MapArchiveException_access_denied_is_chinese_guidance()
    {
        var mapped = BranchSwitchService.MapArchiveException(
            new UnauthorizedAccessException("Access to the path 'd:\\chesed\\steamapps\\common\\Mechabellum' is denied."));

        mapped.Should().Contain("访问被拒绝");
        mapped.Should().Contain("steamservice");
        mapped.Should().Contain("应用并启动");
        mapped.Should().Contain("denied");
    }

    [Fact]
    public void MoveDirectoryWithRetry_moves_when_unlocked()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-move-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        try
        {
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "a.txt"), "ok");

            BranchSwitchService.MoveDirectoryWithRetry(src, dst, attempts: 3, delay: TimeSpan.Zero);

            Directory.Exists(dst).Should().BeTrue();
            File.Exists(Path.Combine(dst, "a.txt")).Should().BeTrue();
            Directory.Exists(src).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void MoveDirectoryWithRetry_retries_then_throws_when_locked()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-move-lock-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        Directory.CreateDirectory(src);
        var locked = Path.Combine(src, "locked.txt");
        File.WriteAllText(locked, "lock");
        var sleeps = 0;
        try
        {
            using var fs = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
            var act = () => BranchSwitchService.MoveDirectoryWithRetry(
                src,
                dst,
                attempts: 3,
                delay: TimeSpan.FromMilliseconds(1),
                sleep: _ => sleeps++);

            act.Should().Throw<Exception>();
            sleeps.Should().Be(2);
            Directory.Exists(src).Should().BeTrue();
            Directory.Exists(dst).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void MoveDirectoryWithRetry_clears_readonly_and_moves()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-move-ro-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "src");
        var dst = Path.Combine(root, "dst");
        try
        {
            Directory.CreateDirectory(src);
            var file = Path.Combine(src, "ro.txt");
            File.WriteAllText(file, "x");
            File.SetAttributes(file, FileAttributes.ReadOnly);

            BranchSwitchService.MoveDirectoryWithRetry(src, dst, attempts: 2, delay: TimeSpan.Zero);

            Directory.Exists(dst).Should().BeTrue();
            File.Exists(Path.Combine(dst, "ro.txt")).Should().BeTrue();
            Directory.Exists(src).Should().BeFalse();
        }
        finally
        {
            try
            {
                if (Directory.Exists(dst))
                    BranchSwitchService.ClearReadOnlyAttributes(dst);
                Directory.Delete(root, true);
            }
            catch { /* ignore */ }
        }
    }
}
