using FluentAssertions;
using MechabellumModManager.Services;

public class SteamGameLocatorTests
{
    [Fact]
    public void LooksLikeGameRoot_requires_exe_and_gameassembly()
    {
        var root = Path.Combine(Path.GetTempPath(), "mmm-steam-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SteamGameLocator.LooksLikeGameRoot(root).Should().BeFalse();
            File.WriteAllText(Path.Combine(root, "Mechabellum.exe"), "");
            SteamGameLocator.LooksLikeGameRoot(root).Should().BeFalse();
            File.WriteAllText(Path.Combine(root, "GameAssembly.dll"), "");
            SteamGameLocator.LooksLikeGameRoot(root).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Default_AppConfig_game_path_is_empty_for_portability()
    {
        new MechabellumModManager.Models.AppConfig().GamePath.Should().BeEmpty();
    }
}
