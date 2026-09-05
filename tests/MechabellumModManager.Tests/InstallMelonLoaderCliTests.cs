using FluentAssertions;
using MechabellumModManager.Services;

public class InstallMelonLoaderCliTests
{
    [Fact]
    public void TryParseArgs_reads_game_and_redist()
    {
        var ok = InstallMelonLoaderCli.TryParseArgs(
            ["--install-melon-loader", "--game-path", @"D:\steam\common\Mechabellum", "--redist-dir", @"C:\redist"],
            out var game, out var redist);
        ok.Should().BeTrue();
        game.Should().Be(@"D:\steam\common\Mechabellum");
        redist.Should().Be(@"C:\redist");
    }

    [Fact]
    public void TryParseArgs_false_without_flag()
    {
        InstallMelonLoaderCli.TryParseArgs(["--game-path", @"D:\x"], out _, out _).Should().BeFalse();
    }
}
