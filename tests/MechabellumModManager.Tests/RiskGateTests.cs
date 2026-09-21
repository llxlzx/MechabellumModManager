using FluentAssertions;
using MechabellumModManager.Services;

public class RiskGateTests
{
    [Fact]
    public void BannerText_matches_spec_chinese_copy()
    {
        RiskGate.BannerText.Should().Be(
            "本工具仅用于客户端 QoL Mod。修改战斗逻辑可能导致 Data Error 与处罚；官方未支持 Mod，风险自负。");
    }

    [Fact]
    public void CanEnable_non_high_risk_returns_true_without_confirm()
    {
        var called = false;
        var gate = new RiskGate();

        var ok = gate.CanEnable(highRisk: false, confirm: _ =>
        {
            called = true;
            return false;
        });

        ok.Should().BeTrue();
        called.Should().BeFalse();
    }

    [Fact]
    public void CanEnable_does_not_confirm_while_detection_is_paused()
    {
        RiskHeuristic.DetectionEnabled.Should().BeFalse();
        var called = false;
        var gate = new RiskGate();

        var ok = gate.CanEnable(highRisk: true, confirm: _ =>
        {
            called = true;
            return false;
        });

        ok.Should().BeTrue();
        called.Should().BeFalse();
    }
}
