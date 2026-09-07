using FluentAssertions;
using MechabellumModManager.Services;

public class MelonLoaderVersionGateTests
{
    [Theory]
    [InlineData("0.7.1")]
    [InlineData("0.7.1.0")]
    [InlineData("0.6.9")]
    public void ShouldUpgrade_true_when_below_bundled_minimum(string version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeTrue();
    }

    [Theory]
    [InlineData("0.7.3")]
    [InlineData("0.7.3.0")]
    [InlineData("0.8.0")]
    public void ShouldUpgrade_false_when_at_or_above_bundled_minimum(string version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeFalse();
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("0.0.0")]
    public void ShouldUpgrade_true_when_file_version_is_zero(string version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void ShouldUpgrade_false_when_unreadable_string_only(string? version)
    {
        MelonLoaderVersionGate.ShouldUpgradeInstalled(version).Should().BeFalse();
    }

    [Fact]
    public void ShouldForceUpgradeStore_true_when_file_version_empty_and_loader_dll_present()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader", "net6"));
            File.WriteAllBytes(Path.Combine(root, "MelonLoader", "net6", "MelonLoader.dll"), new byte[] { 0x4D, 0x5A });
            MelonLoaderVersionGate.ShouldForceUpgradeStore(root, fileVersion: null).Should().BeTrue();
            MelonLoaderVersionGate.ShouldForceUpgradeStore(root, fileVersion: "").Should().BeTrue();
            MelonLoaderVersionGate.ShouldForceUpgradeStore(root, fileVersion: "0.0.0.0").Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ShouldForceUpgradeStore_false_when_file_version_empty_and_no_loader_dll()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-gate-nodll-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
            MelonLoaderVersionGate.ShouldForceUpgradeStore(root, fileVersion: null).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ShouldForceUpgradeStore_true_when_latest_log_below_bundled()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-gate-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
            File.WriteAllText(
                Path.Combine(root, "MelonLoader", "Latest.log"),
                "[14:17:52.025] MelonLoader v0.7.1 Open-Beta\n");

            MelonLoaderVersionGate.ShouldForceUpgradeStore(root, fileVersion: null).Should().BeTrue();
            MelonLoaderVersionGate.TryReadVersionFromLatestLog(root).Should().Be("0.7.1");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ShouldForceUpgradeStore_false_when_file_version_meets_bundled()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-ml-gate-ok-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "MelonLoader"));
            File.WriteAllText(
                Path.Combine(root, "MelonLoader", "Latest.log"),
                "[14:17:52.025] MelonLoader v0.7.1 Open-Beta\n");

            MelonLoaderVersionGate.ShouldForceUpgradeStore(root, fileVersion: "0.7.3").Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
}
