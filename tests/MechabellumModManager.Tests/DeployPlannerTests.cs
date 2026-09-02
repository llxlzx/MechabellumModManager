using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DeployPlannerTests
{
    [Fact]
    public void Flatten_mod_dll_to_Mods_root()
    {
        var pkg = new ModPackage
        {
            Id = "qc",
            Type = ModPackageType.MelonMod,
            PackageDirectory = @"C:\lib\mods\qc",
            Files = { new DeployableFile { RelativePathInPackage = "QuickCamera.dll", Sha256 = "a" } }
        };
        var profile = new Profile { Id = "p", EnabledPackageIds = { "qc" } };
        var plan = new DeployPlanner().Build(@"G:\Game", profile, new Dictionary<string, ModPackage> { ["qc"] = pkg }, null, false);
        plan.Copies.Single().RelativeGamePath.Replace('\\', '/').Should().Be("Mods/QuickCamera.dll");
    }

    [Fact]
    public void Does_not_delete_unmanaged_files()
    {
        // manifest empty; game has Mods/Other.dll unmanaged — Deletes must not include Other.dll
        var gameRoot = Path.Combine(Path.GetTempPath(), "mmm-deploy-" + Guid.NewGuid().ToString("N"));
        var unmanaged = Path.Combine(gameRoot, "Mods", "Other.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(unmanaged)!);
        File.WriteAllText(unmanaged, "x");
        try
        {
            var plan = new DeployPlanner().Build(
                gameRoot,
                new Profile { Id = "p" },
                new Dictionary<string, ModPackage>(),
                existingManifest: null,
                allowOverwriteUnmanaged: false);

            plan.Deletes.Should().BeEmpty();
            plan.Deletes.Should().NotContain(d =>
                d.EndsWith("Other.dll", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(gameRoot, true);
        }
    }

    [Fact]
    public void Deletes_only_manifest_entries_not_in_new_profile()
    {
        var manifest = new DeployManifest
        {
            GamePath = @"G:\Game",
            ProfileId = "old",
            Files = { new ManifestFileEntry { RelativePath = "Mods/Old.dll", PackageId = "old", Sha256 = "1" } }
        };
        var plan = new DeployPlanner().Build(@"G:\Game", new Profile { Id = "p" }, new Dictionary<string, ModPackage>(), manifest, false);
        plan.Deletes.Should().Contain(d => d.EndsWith("Old.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Loader_cfg_in_userdata_package()
    {
        var pkg = new ModPackage
        {
            Id = "u",
            Type = ModPackageType.MelonUserData,
            PackageDirectory = @"C:\lib\userdata\u",
            Files = { new DeployableFile { RelativePathInPackage = "Loader.cfg", Sha256 = "x" } }
        };
        var act = () => new DeployPlanner().Build(@"G:\Game", new Profile { Id = "p", EnabledPackageIds = { "u" } },
            new Dictionary<string, ModPackage> { ["u"] = pkg }, null, false);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GamePath_mismatch_skips_deletes()
    {
        var manifest = new DeployManifest
        {
            GamePath = @"G:\Old",
            Files = { new ManifestFileEntry { RelativePath = "Mods/A.dll", PackageId = "a", Sha256 = "1" } }
        };
        var plan = new DeployPlanner().Build(@"G:\New", new Profile { Id = "p" }, new Dictionary<string, ModPackage>(), manifest, false);
        plan.ManifestInvalidDueToGamePath.Should().BeTrue();
        plan.Deletes.Should().BeEmpty();
    }

    [Fact]
    public void Intra_profile_name_collision_detected()
    {
        var a = new ModPackage { Id = "a", Type = ModPackageType.MelonMod, PackageDirectory = @"C:\a", Files = { new DeployableFile { RelativePathInPackage = "Same.dll", Sha256 = "1" } } };
        var b = new ModPackage { Id = "b", Type = ModPackageType.MelonMod, PackageDirectory = @"C:\b", Files = { new DeployableFile { RelativePathInPackage = "Same.dll", Sha256 = "2" } } };
        var plan = new DeployPlanner().Build(@"G:\Game", new Profile { Id = "p", EnabledPackageIds = { "a", "b" } },
            new Dictionary<string, ModPackage> { ["a"] = a, ["b"] = b }, null, false);
        plan.IntraProfileNameCollisions.Should().NotBeEmpty();
        plan.Copies.Should().BeEmpty();
        plan.Deletes.Should().BeEmpty();
    }

    [Fact]
    public void Rejects_Loader_cfg_via_parent_traversal()
    {
        var act = () => DeployPlanner.MapRelativeGamePath(ModPackageType.MelonUserData, @"foo\..\Loader.cfg");
        act.Should().Throw<InvalidOperationException>();

        var act2 = () => DeployPlanner.MapRelativeGamePath(ModPackageType.MelonUserData, "foo/../Loader.cfg");
        act2.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_rooted_UserData_path()
    {
        var act = () => DeployPlanner.MapRelativeGamePath(ModPackageType.MelonUserData, @"C:\Windows\Loader.cfg");
        act.Should().Throw<InvalidOperationException>();

        var act2 = () => DeployPlanner.MapRelativeGamePath(ModPackageType.MelonUserData, "/etc/Loader.cfg");
        act2.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_UserData_escape_with_dotdot()
    {
        var act = () => DeployPlanner.MapRelativeGamePath(ModPackageType.MelonUserData, @"..\Mods\x.dll");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Allows_nested_Loader_cfg_under_UserData_subdir()
    {
        var path = DeployPlanner.MapRelativeGamePath(ModPackageType.MelonUserData, @"foo\Loader.cfg");
        path.Replace('\\', '/').Should().Be("UserData/foo/Loader.cfg");
    }
}
