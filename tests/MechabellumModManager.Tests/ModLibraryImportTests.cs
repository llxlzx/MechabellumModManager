using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ModLibraryImportTests
{
    static readonly string SampleDll =
        @"D:\gongzuo\钢铁指挥官mod管理器开发\_samples\QuickCamera\QuickCamera.dll";

    [Fact]
    public void Import_QuickCamera_dll_creates_melon_mod_package_flat_files()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        try
        {
            var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore(), new ProfileService(paths, new JsonStore()));
            var pkg = lib.ImportDll(SampleDll);
            pkg.Type.Should().Be(ModPackageType.MelonMod);
            pkg.Files.Should().ContainSingle(f =>
                f.RelativePathInPackage.Equals("QuickCamera.dll", StringComparison.OrdinalIgnoreCase));
            File.Exists(Path.Combine(pkg.PackageDirectory, "QuickCamera.dll")).Should().BeTrue();
            File.Exists(Path.Combine(pkg.PackageDirectory, "package.json")).Should().BeTrue();
            pkg.Id.Should().NotBeNullOrWhiteSpace();
            pkg.Id.Should().Contain("-");

            var json = File.ReadAllText(Path.Combine(pkg.PackageDirectory, "package.json"));
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("type").GetString().Should().Be("melon_mod");
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Import_zip_with_Mods_prefix_strips_prefix()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var zipPath = Path.Combine(Path.GetTempPath(), "mmm-zip-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            CreateZip(zipPath, ("Mods/QuickCamera.dll", File.ReadAllBytes(SampleDll)));

            var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore(), new ProfileService(paths, new JsonStore()));
            var pkgs = lib.ImportZip(zipPath);
            pkgs.Should().ContainSingle();
            var pkg = pkgs[0];
            pkg.Type.Should().Be(ModPackageType.MelonMod);
            pkg.Files.Should().ContainSingle(f =>
                f.RelativePathInPackage.Equals("QuickCamera.dll", StringComparison.OrdinalIgnoreCase));
            pkg.Files.Should().NotContain(f =>
                f.RelativePathInPackage.Contains("Mods", StringComparison.OrdinalIgnoreCase));
            File.Exists(Path.Combine(pkg.PackageDirectory, "QuickCamera.dll")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void Import_mixed_zip_splits_into_mod_and_userlibs()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var zipPath = Path.Combine(Path.GetTempPath(), "mmm-zip-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            CreateZip(zipPath,
                ("Mods/a.dll", File.ReadAllBytes(SampleDll)),
                ("UserLibs/b.dll", new byte[] { 0x4D, 0x5A })); // stub MZ header

            var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore(), new ProfileService(paths, new JsonStore()));
            var pkgs = lib.ImportZip(zipPath);
            pkgs.Should().HaveCount(2);
            pkgs.Should().Contain(p => p.Type == ModPackageType.MelonMod);
            pkgs.Should().Contain(p => p.Type == ModPackageType.MelonUserLibs);

            var mod = pkgs.Single(p => p.Type == ModPackageType.MelonMod);
            mod.Files.Should().ContainSingle(f =>
                f.RelativePathInPackage.Equals("a.dll", StringComparison.OrdinalIgnoreCase));

            var libs = pkgs.Single(p => p.Type == ModPackageType.MelonUserLibs);
            libs.Files.Should().ContainSingle(f =>
                f.RelativePathInPackage.Equals("b.dll", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void Import_unknown_dll_without_force_throws_ImportNeedsTypeException()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var stub = Path.Combine(data, "mystery.dll");
        try
        {
            File.WriteAllBytes(stub, new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore(), new ProfileService(paths, new JsonStore()));
            var act = () => lib.ImportDll(stub);
            act.Should().Throw<ImportNeedsTypeException>()
                .Which.StagingPath.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    [Fact]
    public void Import_unprefixed_ambiguous_zip_keeps_StagingPath()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var zipPath = Path.Combine(Path.GetTempPath(), "mmm-zip-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            CreateZip(zipPath, ("mystery.dll", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }));

            var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore(), new ProfileService(paths, new JsonStore()));
            var act = () => lib.ImportZip(zipPath);
            var ex = act.Should().Throw<ImportNeedsTypeException>().Which;
            ex.StagingPath.Should().NotBeNullOrWhiteSpace();
            Directory.Exists(ex.StagingPath).Should().BeTrue(
                "ImportNeedsTypeException must preserve staging for UI forceType");
            File.Exists(Path.Combine(ex.StagingPath, "mystery.dll")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void Import_package_json_without_type_does_not_force_MelonMod()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var zipPath = Path.Combine(Path.GetTempPath(), "mmm-zip-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            // Typeless / partial package.json must NOT silently become MelonMod.
            var meta = Encoding.UTF8.GetBytes("""{"displayName":"mystery","version":"1.0"}""");
            CreateZip(zipPath,
                ("mystery.dll", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }),
                ("package.json", meta));

            var lib = new ModLibraryService(paths, new AssemblyInspector(), new JsonStore(), new ProfileService(paths, new JsonStore()));
            var act = () => lib.ImportZip(zipPath);
            var ex = act.Should().Throw<ImportNeedsTypeException>().Which;
            Directory.Exists(ex.StagingPath).Should().BeTrue();
            lib.List().Should().BeEmpty("no package should be committed when type is unresolved");
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void Delete_removes_package_id_from_profile_EnabledPackageIds()
    {
        var data = Path.Combine(Path.GetTempPath(), "mmm-lib-" + Guid.NewGuid().ToString("N"));
        var paths = new PathsService(data);
        paths.EnsureCreated();
        var store = new JsonStore();
        try
        {
            var profiles = new ProfileService(paths, store);
            profiles.EnsureDefaults();
            var lib = new ModLibraryService(paths, new AssemblyInspector(), store, profiles);

            var pkg = lib.ImportDll(SampleDll);
            var profileId = profiles.List().Single().Id;
            profiles.SetEnabled(profileId, pkg.Id, true);
            profiles.Get(profileId).EnabledPackageIds.Should().Contain(pkg.Id);

            lib.Delete(pkg.Id);

            profiles.Get(profileId).EnabledPackageIds.Should().NotContain(pkg.Id);
            lib.List().Should().NotContain(p => p.Id == pkg.Id);
        }
        finally
        {
            Directory.Delete(data, true);
        }
    }

    static void CreateZip(string zipPath, params (string Entry, byte[] Bytes)[] entries)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (entry, bytes) in entries)
        {
            var e = zip.CreateEntry(entry.Replace('\\', '/'));
            using var s = e.Open();
            s.Write(bytes, 0, bytes.Length);
        }
    }
}