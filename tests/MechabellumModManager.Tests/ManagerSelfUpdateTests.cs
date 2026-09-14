using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class ManagerDeploymentDetectorTests
{
    [Fact]
    public void Detects_installed_when_unins000_exe_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-dep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "unins000.exe"), "x");
        try
        {
            ManagerDeploymentDetector.Detect(root).Should().Be(ManagerDeploymentKind.Installed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Detects_portable_when_no_uninstaller()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-dep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ManagerDeploymentDetector.Detect(root).Should().Be(ManagerDeploymentKind.Portable);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class ManagerSelfUpdatePlannerTests
{
    [Fact]
    public void One_click_requires_https_url_and_sha_for_channel()
    {
        var installedOk = new UpdateManifest
        {
            SetupUrl = "https://cdn.example/Setup.exe",
            SetupSha256 = new string('a', 64)
        };
        ManagerSelfUpdatePlanner.CanApplyOneClick(ManagerDeploymentKind.Installed, installedOk)
            .Should().BeTrue();

        ManagerSelfUpdatePlanner.CanApplyOneClick(
                ManagerDeploymentKind.Installed,
                new UpdateManifest { SetupUrl = "https://cdn.example/Setup.exe" })
            .Should().BeFalse();

        var portableOk = new UpdateManifest
        {
            PortableUrl = "https://cdn.example/p.zip",
            PortableSha256 = new string('b', 64)
        };
        ManagerSelfUpdatePlanner.CanApplyOneClick(ManagerDeploymentKind.Portable, portableOk)
            .Should().BeTrue();

        ManagerSelfUpdatePlanner.CanApplyOneClick(
                ManagerDeploymentKind.Portable,
                new UpdateManifest
                {
                    SetupUrl = "https://cdn.example/Setup.exe",
                    SetupSha256 = new string('a', 64)
                })
            .Should().BeFalse();
    }

    [Fact]
    public void Rejects_non_https_urls()
    {
        ManagerSelfUpdatePlanner.CanApplyOneClick(
                ManagerDeploymentKind.Installed,
                new UpdateManifest
                {
                    SetupUrl = "http://cdn.example/Setup.exe",
                    SetupSha256 = new string('a', 64)
                })
            .Should().BeFalse();
    }
}

public class PortableUpdateApplierTests
{
    [Fact]
    public void ApplyStagedFiles_copies_over_target_excluding_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-port-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "_update_staging");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(root, "MechabellumModManager.exe"), "old");
        File.WriteAllText(Path.Combine(root, "keep-me.txt"), "old");
        File.WriteAllText(Path.Combine(data, "config.json"), "user");
        File.WriteAllText(Path.Combine(staging, "MechabellumModManager.exe"), "new");
        File.WriteAllText(Path.Combine(staging, "keep-me.txt"), "new");
        Directory.CreateDirectory(Path.Combine(staging, "Assets"));
        File.WriteAllText(Path.Combine(staging, "Assets", "a.txt"), "asset");

        try
        {
            PortableUpdateApplier.ApplyStagedFiles(staging, root);

            File.ReadAllText(Path.Combine(root, "MechabellumModManager.exe")).Should().Be("new");
            File.ReadAllText(Path.Combine(root, "keep-me.txt")).Should().Be("new");
            File.ReadAllText(Path.Combine(root, "Assets", "a.txt")).Should().Be("asset");
            File.ReadAllText(Path.Combine(data, "config.json")).Should().Be("user");
            Directory.Exists(staging).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FlattenIfSingleRootFolder_unwraps_nested_zip_root()
    {
        var staging = Path.Combine(Path.GetTempPath(), "mmm-flat-" + Guid.NewGuid().ToString("N"));
        var inner = Path.Combine(staging, "MechabellumModManager");
        Directory.CreateDirectory(inner);
        File.WriteAllText(Path.Combine(inner, "MechabellumModManager.exe"), "x");
        try
        {
            ManagerSelfUpdateService.FlattenIfSingleRootFolder(staging);
            File.Exists(Path.Combine(staging, "MechabellumModManager.exe")).Should().BeTrue();
            Directory.Exists(inner).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }
}
