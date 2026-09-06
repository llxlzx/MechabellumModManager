using FluentAssertions;
using MechabellumModManager.Services;

public class SteamBranchLayoutTests
{
    [Theory]
    [InlineData(@"D:\steam\steamapps\common\Mechabellum_official", @"D:\steam\steamapps\common\Mechabellum")]
    [InlineData(@"H:\Steam\steamapps\common\Mechabellum_beta", @"H:\Steam\steamapps\common\Mechabellum")]
    public void TryGetCanonicalSteamLink_maps_store_to_Mechabellum(string store, string expected)
    {
        SteamBranchLayout.TryGetCanonicalSteamLink(store, out var link).Should().BeTrue();
        Path.GetFullPath(link).Should().Be(Path.GetFullPath(expected));
    }

    [Fact]
    public void TryGetCanonicalSteamLink_false_for_normal_link()
    {
        SteamBranchLayout.TryGetCanonicalSteamLink(@"D:\steam\steamapps\common\Mechabellum", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void IsBranchStorePath_detects_official_and_beta()
    {
        SteamBranchLayout.IsBranchStorePath(@"D:\steam\steamapps\common\Mechabellum_official").Should().BeTrue();
        SteamBranchLayout.IsBranchStorePath(@"D:\steam\steamapps\common\Mechabellum").Should().BeFalse();
    }

    [Fact]
    public void EnumerateExistingStoreFolders_lists_leftovers()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-layout-" + Guid.NewGuid().ToString("N"));
        var common = Path.Combine(root, "steamapps", "common");
        var official = Path.Combine(common, "Mechabellum_official");
        var beta = Path.Combine(common, "Mechabellum_beta");
        Directory.CreateDirectory(official);
        Directory.CreateDirectory(beta);
        try
        {
            var found = SteamBranchLayout.EnumerateExistingStoreFolders(Path.Combine(common, "Mechabellum"))
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            found.Should().Contain(Path.GetFullPath(official));
            found.Should().Contain(Path.GetFullPath(beta));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }
}
