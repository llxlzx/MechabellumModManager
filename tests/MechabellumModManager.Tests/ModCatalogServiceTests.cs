using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class ModCatalogServiceTests
{
    const string SampleCatalogJson = """
        {
          "updatedAt": "2026-09-03",
          "mods": [
            {
              "id": "cam",
              "name": "Cam HUD",
              "author": "巴巴",
              "version": "1.0.0",
              "updatedAt": "2026-09-01",
              "summary": "摄像头相关 QoL",
              "file": "mods/cam/Cam.dll",
              "preview": "mods/cam/preview.png",
              "type": "melon_mod"
            },
            {
              "id": "damage-display",
              "name": "伤害显示",
              "author": "巴巴",
              "version": "1.2.0",
              "updatedAt": "2026-08-20",
              "summary": "战斗伤害数字",
              "file": "mods/damage-display/DamageDisplay.dll",
              "type": "melon_mod"
            }
          ]
        }
        """;

    [Fact]
    public void DeserializeCatalog_reads_camelCase_fields()
    {
        var root = ModCatalogService.DeserializeCatalog(SampleCatalogJson);

        root.UpdatedAt.Should().Be("2026-09-03");
        root.Mods.Should().HaveCount(2);
        var cam = root.Mods[0];
        cam.Id.Should().Be("cam");
        cam.Name.Should().Be("Cam HUD");
        cam.Author.Should().Be("巴巴");
        cam.Version.Should().Be("1.0.0");
        cam.UpdatedAt.Should().Be("2026-09-01");
        cam.Summary.Should().Be("摄像头相关 QoL");
        cam.File.Should().Be("mods/cam/Cam.dll");
        cam.Preview.Should().Be("mods/cam/preview.png");
        cam.Type.Should().Be("melon_mod");
        root.Mods[1].Preview.Should().BeNull();
    }

    [Fact]
    public void GetRawUrl_and_PreviewUrl_build_raw_github_urls()
    {
        ModCatalogService.GetRawUrl("mods/cam/preview.png").Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/preview.png");

        var cam = new CatalogMod { Preview = "mods/cam/preview.png" };
        ModCatalogService.PreviewUrl(cam).Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/preview.png");
        ModCatalogService.PreviewUrl(new CatalogMod()).Should().BeNull();
    }

    [Fact]
    public void IsInLibraryByFileName_matches_case_insensitive_filename()
    {
        var packages = new[]
        {
            new ModPackage
            {
                Id = "cam-aaaaaaaa",
                DisplayName = "Cam",
                Files =
                {
                    new DeployableFile { RelativePathInPackage = "Cam.dll", Sha256 = "abc" }
                }
            }
        };

        ModCatalogService.IsInLibraryByFileName(packages, "mods/cam/Cam.dll").Should().BeTrue();
        ModCatalogService.IsInLibraryByFileName(packages, "mods/cam/cam.dll").Should().BeTrue();
        ModCatalogService.IsInLibraryByFileName(packages, "mods/other/Other.dll").Should().BeFalse();
        ModCatalogService.IsInLibraryByFileName(packages, "").Should().BeFalse();
    }

    [Fact]
    public void BuildFileUrl_uses_owner_repo_branch()
    {
        var svc = new ModCatalogService();
        var url = svc.BuildFileUrl(new CatalogMod { File = "mods/cam/Cam.dll" });
        url.ToString().Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/mods/cam/Cam.dll");
    }

    [Fact]
    public void GetRawUrl_and_BuildFileUrl_tolerate_null_paths()
    {
        ModCatalogService.GetRawUrl(null!).Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/");
        ModCatalogService.PreviewUrl(null).Should().BeNull();

        var svc = new ModCatalogService();
        var url = svc.BuildFileUrl(new CatalogMod { File = null! });
        url.ToString().Should().Be(
            "https://raw.githubusercontent.com/llxlzx/MechabellumMods/master/");
    }

    [Theory]
    [InlineData("melon_mod", ModPackageType.MelonMod)]
    [InlineData("melon_plugin", ModPackageType.MelonPlugin)]
    [InlineData(null, ModPackageType.MelonMod)]
    public void ParsePackageType_maps_known_values(string? type, ModPackageType expected)
    {
        ModCatalogService.ParsePackageType(type).Should().Be(expected);
    }
}
