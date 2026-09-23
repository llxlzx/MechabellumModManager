using FluentAssertions;
using MechabellumModManager.Services;

public class LogicFrameGateTests
{
    [Theory]
    [InlineData(LogicFrameGrade.Low, false)]
    [InlineData(LogicFrameGrade.Unchecked, false)]
    [InlineData(LogicFrameGrade.Medium, true)]
    [InlineData(LogicFrameGrade.High, true)]
    public void NeedsConfirm_only_medium_and_high(LogicFrameGrade grade, bool expected)
    {
        LogicFrameGate.NeedsConfirm(grade).Should().Be(expected);
    }

    [Fact]
    public void CanEnable_medium_asks_even_while_keyword_detection_is_paused()
    {
        RiskHeuristic.DetectionEnabled.Should().BeFalse();
        string? seen = null;
        var ok = LogicFrameGate.CanEnable(
            LogicFrameGrade.Medium,
            "伤害排名",
            _ => "「{0}」",
            message => { seen = message; return false; });
        ok.Should().BeFalse();
        seen.Should().Contain("伤害排名");
    }

    [Fact]
    public void CanEnable_low_does_not_call_confirm()
    {
        var called = false;
        LogicFrameGate.CanEnable(LogicFrameGrade.Low, "格线", key => key, _ => { called = true; return false; })
            .Should().BeTrue();
        called.Should().BeFalse();
    }

    [Fact]
    public void CanEnable_display_name_with_braces_does_not_throw_and_appears_unchanged()
    {
        string? seen = null;
        const string name = "mod{x}";
        var ok = LogicFrameGate.CanEnable(
            LogicFrameGrade.High,
            name,
            _ => "「{0}」",
            message => { seen = message; return false; });
        ok.Should().BeFalse();
        seen.Should().Contain(name);
    }
}
