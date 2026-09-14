using FluentAssertions;
using MechabellumModManager.Services;

public class RedistDirectoryResolverTests
{
    [Fact]
    public void Resolve_uses_preferred_when_writable()
    {
        var preferredBase = Path.Combine(Path.GetTempPath(), "mmm-redist-pref-" + Guid.NewGuid().ToString("N"));
        var localRoot = Path.Combine(Path.GetTempPath(), "mmm-redist-local-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(preferredBase);
            var result = RedistDirectoryResolver.Resolve(preferredBase, localRoot, _ => true);

            result.Should().Be(Path.GetFullPath(Path.Combine(preferredBase, RedistDirectoryResolver.FolderName)));
            Directory.Exists(result).Should().BeTrue();
        }
        finally
        {
            TryDelete(preferredBase);
            TryDelete(localRoot);
        }
    }

    [Fact]
    public void Resolve_falls_back_to_local_app_data_when_preferred_not_writable()
    {
        var preferredBase = Path.Combine(Path.GetTempPath(), "mmm-redist-pref-" + Guid.NewGuid().ToString("N"));
        var localRoot = Path.Combine(Path.GetTempPath(), "mmm-redist-local-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RedistDirectoryResolver.Resolve(preferredBase, localRoot, _ => false);

            var expected = Path.GetFullPath(Path.Combine(localRoot, "MechabellumModManager", RedistDirectoryResolver.FolderName));
            result.Should().Be(expected);
            Directory.Exists(result).Should().BeTrue();
        }
        finally
        {
            TryDelete(preferredBase);
            TryDelete(localRoot);
        }
    }

    [Fact]
    public void Resolve_reports_fallback_used()
    {
        var preferredBase = Path.Combine(Path.GetTempPath(), "mmm-redist-pref-" + Guid.NewGuid().ToString("N"));
        var localRoot = Path.Combine(Path.GetTempPath(), "mmm-redist-local-" + Guid.NewGuid().ToString("N"));
        try
        {
            var resolved = RedistDirectoryResolver.Resolve(
                preferredBase,
                localRoot,
                _ => false,
                out var usedFallback);

            usedFallback.Should().BeTrue();
            resolved.Should().Contain("MechabellumModManager");
        }
        finally
        {
            TryDelete(preferredBase);
            TryDelete(localRoot);
        }
    }

    static void TryDelete(string root)
    {
        try { Directory.Delete(root, true); } catch { /* ignore */ }
    }
}
