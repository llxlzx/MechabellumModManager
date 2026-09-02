using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class RiskHeuristicTests
{
    readonly RiskHeuristic _sut = new();

    [Theory]
    [InlineData("QuickDamage", "damage")]
    [InlineData("SuperCHEAT", "cheat")]
    [InlineData("economy-helper", "economy")]
    [InlineData("战斗增强", "战斗")]
    [InlineData("数值修改器", "修改器")]
    public void Marks_high_risk_when_display_name_hits(string displayName, string expectedKeyword)
    {
        var pkg = Pkg(displayName: displayName);
        var result = _sut.Evaluate(pkg);
        result.HighRisk.Should().BeTrue();
        result.MatchedKeyword.Should().BeEquivalentTo(expectedKeyword);
    }

    [Fact]
    public void Marks_high_risk_from_filename()
    {
        var pkg = Pkg(displayName: "QoL Pack", files: "Mods/GodModeTool.dll");
        var result = _sut.Evaluate(pkg);
        result.HighRisk.Should().BeTrue();
        result.MatchedKeyword.Should().BeEquivalentTo("godmode");
    }

    [Fact]
    public void Marks_high_risk_from_author()
    {
        var pkg = Pkg(displayName: "Camera", author: "TrainerLabs");
        _sut.Evaluate(pkg).HighRisk.Should().BeTrue();
    }

    [Theory]
    [InlineData("QuickCamera")]
    [InlineData("BetterWindow")]
    [InlineData("UI Scale")]
    public void Does_not_mark_common_qol_names(string displayName)
    {
        _sut.Evaluate(Pkg(displayName: displayName)).HighRisk.Should().BeFalse();
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var result = _sut.Evaluate(Pkg(displayName: "DaMaGeBoost"));
        result.HighRisk.Should().BeTrue();
        result.MatchedKeyword.Should().BeEquivalentTo("damage");
    }

    static ModPackage Pkg(string displayName, string? author = null, string? files = null) =>
        new()
        {
            Id = "id-" + displayName.Replace(' ', '-'),
            DisplayName = displayName,
            Author = author,
            PackageDirectory = Path.Combine(Path.GetTempPath(), "lib", displayName.Replace(' ', '_')),
            Files = string.IsNullOrEmpty(files)
                ? new List<DeployableFile>()
                : new List<DeployableFile> { new() { RelativePathInPackage = files } }
        };
}
