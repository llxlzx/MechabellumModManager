namespace MechabellumModManager.Models;

public sealed class BranchSwitchJournal
{
    public string Phase { get; set; } = "";
    public GameBranch PreviousBranch { get; set; }
    public GameBranch TargetBranch { get; set; }
    public string SteamLinkPath { get; set; } = "";
    public string PreviousStorePath { get; set; } = "";
    public string TargetStorePath { get; set; } = "";
}
