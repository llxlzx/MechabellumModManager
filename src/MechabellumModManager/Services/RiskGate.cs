namespace MechabellumModManager.Services;

public sealed class RiskGate
{
    public const string BannerText =
        "本工具仅用于客户端 QoL Mod。修改战斗逻辑可能导致 Data Error 与处罚；官方未支持 Mod，风险自负。";

    public bool CanEnable(bool highRisk, Func<string, bool> confirm)
    {
        if (!highRisk) return true;
        return confirm("该条目被标记为高风险，确定加入当前方案吗？");
    }
}
