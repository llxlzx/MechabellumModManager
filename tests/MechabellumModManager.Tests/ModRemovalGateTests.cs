using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class ModRemovalGateTests
{
    static ModPackage Package(string id, string? catalogId = null, string displayName = "Mod", params string[] hashes) => new()
    {
        Id = id,
        CatalogId = catalogId,
        DisplayName = displayName,
        Type = ModPackageType.MelonMod,
        Files = hashes.Select(h => new DeployableFile { RelativePathInPackage = id + ".dll", Sha256 = h }).ToList()
    };

    static CatalogRemoval Removal(string id, params string[] hashes) => new()
    {
        Id = id,
        Sha256 = hashes.ToList()
    };

    [Fact]
    public void Catalog_id_matches_ignoring_case()
    {
        ModRemovalGate.Matches(Package("econ-1234", catalogId: "Opponent-Economy"), Removal("opponent-economy"))
            .Should().BeTrue();
    }

    [Fact]
    public void Package_id_matches_when_it_is_the_catalog_id()
    {
        ModRemovalGate.Matches(Package("opponent-economy"), Removal("opponent-economy")).Should().BeTrue();
    }

    [Fact]
    public void File_hash_matches_ignoring_case()
    {
        var pkg = Package("renamed-99", displayName: "Something", hashes: "ABCDEF");

        ModRemovalGate.Matches(pkg, Removal("opponent-economy", "abcdef")).Should().BeTrue();
    }

    [Fact]
    public void Same_display_name_alone_does_not_match()
    {
        var pkg = Package("econ-hud-1", displayName: "opponent-economy", hashes: "1111");

        ModRemovalGate.Matches(pkg, Removal("opponent-economy", "2222")).Should().BeFalse();
    }

    [Fact]
    public void Blank_id_and_blank_hashes_never_match()
    {
        var pkg = Package("x", catalogId: "", hashes: "");

        ModRemovalGate.Matches(pkg, new CatalogRemoval { Id = "", Sha256 = [""] }).Should().BeFalse();
    }

    [Fact]
    public void Plan_pairs_each_hit_with_its_removal()
    {
        var hit = Package("opponent-economy");
        var keep = Package("show-grid", hashes: "9999");

        var plan = ModRemovalGate.Plan([hit, keep], [Removal("opponent-economy")]);

        plan.Should().ContainSingle();
        plan[0].Package.Should().BeSameAs(hit);
        plan[0].Removal.Id.Should().Be("opponent-economy");
    }

    [Fact]
    public void Plan_is_empty_without_a_removed_list()
    {
        ModRemovalGate.Plan([Package("a")], null).Should().BeEmpty();
    }

    [Fact]
    public void Manifest_entries_match_by_package_or_by_hash()
    {
        var manifest = new DeployManifest
        {
            Files =
            [
                new ManifestFileEntry { RelativePath = "Mods/A.dll", PackageId = "pkg-a", Sha256 = "1111" },
                new ManifestFileEntry { RelativePath = "Mods/Orphan.dll", PackageId = "gone", Sha256 = "BEEF" },
                new ManifestFileEntry { RelativePath = "Mods/Keep.dll", PackageId = "keep", Sha256 = "2222" }
            ]
        };

        var hits = ModRemovalGate.ManifestHits(
            manifest,
            [Removal("opponent-economy", "beef")],
            new HashSet<string>(["PKG-A"], StringComparer.OrdinalIgnoreCase));

        hits.Select(h => h.RelativePath).Should().BeEquivalentTo("Mods/A.dll", "Mods/Orphan.dll");
    }

    [Fact]
    public void Ids_both_listed_and_removed_are_reported()
    {
        CatalogMod[] mods =
        [
            new CatalogMod { Id = "Opponent-Economy", Name = "E", File = "mods/e/E.dll" },
            new CatalogMod { Id = "show-grid", Name = "G", File = "mods/g/G.dll" }
        ];

        ModRemovalGate.ListedAndRemoved(mods, [Removal("opponent-economy")])
            .Select(m => m.Id).Should().Equal("Opponent-Economy");
    }

    const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string HashB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    const string EmptyFileHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void Sanitize_keeps_only_full_sha256_values()
    {
        var clean = ModRemovalGate.Sanitize(
            [Removal("x", HashA, "aabbccdd", "", "not-hex-" + new string('z', 56), EmptyFileHash, HashB)],
            [],
            out var warnings);

        clean.Should().ContainSingle().Which.Sha256.Should().Equal(HashA, HashB.ToLowerInvariant());
        warnings.Should().HaveCount(3);
    }

    [Fact]
    public void Sanitize_never_removes_bytes_a_listed_mod_still_ships()
    {
        CatalogMod[] listed =
        [
            new CatalogMod
            {
                Id = "econ-v2",
                Name = "Econ",
                File = "mods/e/Econ.dll",
                Sha256 = HashA,
                Bundle = [new CatalogBundleFile { Path = "Econ.Assets/Text.dll", File = "mods/e/Text.dll", Sha256 = HashB }]
            }
        ];

        var clean = ModRemovalGate.Sanitize([Removal("econ-v1", HashA, HashB)], listed, out var warnings);

        clean.Should().ContainSingle().Which.Sha256.Should().BeEmpty();
        warnings.Should().HaveCount(2).And.OnlyContain(w => w.Contains("econ-v2"));
    }

    [Fact]
    public void Sanitize_drops_entries_with_neither_id_nor_hashes()
    {
        ModRemovalGate.Sanitize([new CatalogRemoval { Id = " ", Sha256 = ["bad"] }], [], out _)
            .Should().BeEmpty();
    }
}
