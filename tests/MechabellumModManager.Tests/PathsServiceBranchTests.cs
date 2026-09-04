using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class PathsServiceBranchTests
{
    [Fact]
    public void When_disabled_uses_legacy_manifest_names()
    {
        var paths = new PathsService(Path.Combine(Path.GetTempPath(), "mmm-p-" + Guid.NewGuid().ToString("N")));
        paths.GetDeployManifestPath(GameBranch.Official, enabled: false)
            .Should().EndWith("deploy-manifest.json");
        paths.GetDeployManifestPrevPath(GameBranch.Beta, enabled: false)
            .Should().EndWith("deploy-manifest.prev.json");
    }

    [Fact]
    public void When_enabled_uses_per_branch_manifest_names()
    {
        var paths = new PathsService(Path.Combine(Path.GetTempPath(), "mmm-p-" + Guid.NewGuid().ToString("N")));
        paths.GetDeployManifestPath(GameBranch.Official, enabled: true)
            .Should().EndWith("deploy-manifest.official.json");
        paths.GetDeployManifestPath(GameBranch.Beta, enabled: true)
            .Should().EndWith("deploy-manifest.beta.json");
        paths.GetDeployManifestPrevPath(GameBranch.Beta, enabled: true)
            .Should().EndWith("deploy-manifest.beta.prev.json");
    }

    [Fact]
    public void BranchSwitchConfigPath_is_under_DataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-p-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(root);
        paths.BranchSwitchConfigPath.Should().Be(Path.Combine(root, "branch-switch.json"));
    }
}
