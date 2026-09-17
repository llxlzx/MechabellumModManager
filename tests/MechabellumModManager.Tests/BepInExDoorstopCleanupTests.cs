using FluentAssertions;
using MechabellumModManager.Services;

public class BepInExDoorstopCleanupTests
{
    [Fact]
    public void TryDisable_removes_doorstop_when_melon_version_dll_present()
    {
        var game = Path.Combine(Path.GetTempPath(), "mmm-doorstop-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(game, "MelonLoader"));
            Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
            File.WriteAllBytes(Path.Combine(game, "version.dll"), new byte[] { 0x4D, 0x5A });
            File.WriteAllBytes(Path.Combine(game, "winhttp.dll"), new byte[] { 0x4D, 0x5A, 0x01 });
            File.WriteAllText(Path.Combine(game, "doorstop_config.ini"), "enabled=true\n");
            File.WriteAllText(Path.Combine(game, ".doorstop_version"), "4.4.0");

            var result = new BepInExDoorstopCleanup().TryDisableConflictingDoorstop(game);
            result.Changed.Should().BeTrue();
            File.Exists(Path.Combine(game, "winhttp.dll")).Should().BeFalse();
            File.Exists(Path.Combine(game, "doorstop_config.ini")).Should().BeFalse();
            File.Exists(Path.Combine(game, ".doorstop_version")).Should().BeFalse();
            File.Exists(Path.Combine(game, "version.dll")).Should().BeTrue();
            Directory.Exists(Path.Combine(game, "BepInEx")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(game)) Directory.Delete(game, true);
        }
    }

    [Fact]
    public void TryDisable_noop_without_melon()
    {
        var game = Path.Combine(Path.GetTempPath(), "mmm-doorstop-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(game, "BepInEx"));
            File.WriteAllBytes(Path.Combine(game, "winhttp.dll"), new byte[] { 0x4D, 0x5A });
            File.WriteAllText(Path.Combine(game, "doorstop_config.ini"), "enabled=true\n");

            var result = new BepInExDoorstopCleanup().TryDisableConflictingDoorstop(game);
            result.Changed.Should().BeFalse();
            File.Exists(Path.Combine(game, "winhttp.dll")).Should().BeTrue();
            File.Exists(Path.Combine(game, "doorstop_config.ini")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(game)) Directory.Delete(game, true);
        }
    }
}
