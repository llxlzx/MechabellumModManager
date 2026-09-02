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
    public void CanEnable_high_risk_uses_confirm_and_message()
    {
        string? seen = null;
        var gate = new RiskGate();

        var ok = gate.CanEnable(highRisk: true, confirm: msg =>
        {
            seen = msg;
            return true;
        });

        ok.Should().BeTrue();
        seen.Should().Be("该条目被标记为高风险，确定加入当前方案吗？");
    }

    [Fact]
    public void CanEnable_high_risk_respects_confirm_false()
    {
        new RiskGate().CanEnable(true, _ => false).Should().BeFalse();
    }
}
