using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Themes;

public class ThemeAccentTests
{
    [Fact]
    public void Official_branch_uses_amber_gold()
    {
        var palette = ThemeAccent.For(GameBranch.Official);

        palette.Base.R.Should().Be(0xD6);
        palette.Base.G.Should().Be(0xAA);
        palette.Base.B.Should().Be(0x63);
    }

    [Fact]
    public void Beta_branch_uses_ice_blue_with_the_same_roles()
    {
        var normal = ThemeAccent.For(GameBranch.Official);
        var expedition = ThemeAccent.For(GameBranch.Beta);

        expedition.Base.R.Should().Be(0x78);
        expedition.Base.G.Should().Be(0xB9);
        expedition.Base.B.Should().Be(0xDE);
        expedition.Hot.Should().NotBe(normal.Hot);
        expedition.Pressed.Should().NotBe(normal.Pressed);
    }
}
