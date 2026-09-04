namespace MechabellumModManager.Models;

public enum GameBranch { Official, Beta }

public enum BranchWizardStep
{
    None, Declared, ArchivedA, WaitingDownloadB, ArchivedB, Linked, AwaitingSteamSettle, Ready
}

public sealed class BranchSwitchConfig
{
    public bool Enabled { get; set; }
    public BranchWizardStep WizardStep { get; set; } = BranchWizardStep.None;
    public string SteamLinkPath { get; set; } = "";
    public string OfficialStorePath { get; set; } = "";
    public string BetaStorePath { get; set; } = "";
    public GameBranch ActiveBranch { get; set; } = GameBranch.Official;
    public string OfficialProfileId { get; set; } = "default";
    public string BetaProfileId { get; set; } = "default";
    public string BetaBranchName { get; set; } = "";
    public string? ManifestBackupPath { get; set; }
}
