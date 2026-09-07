using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class EmergencyRecoverGuidanceTests
{
    [Fact]
    public void Official_missing_skips_delete_other_and_uses_honest_confirm()
    {
        var prompt = EmergencyRecoverGuidance.Build(
            new BranchSwitchConfig
            {
                Enabled = false,
                SteamLinkPath = @"D:\steam\steamapps\common\Mechabellum",
                OfficialStorePath = "",
                BetaStorePath = "",
                ActiveBranch = GameBranch.Beta
            },
            preferredLinkPath: @"D:\steam\steamapps\common\Mechabellum",
            looksLikeGameRoot: _ => false);

        prompt.Kind.Should().Be(EmergencyRecoverPromptKind.OfficialMissing);
        prompt.ConfirmKey.Should().Be("ConfirmEmergencyRecoverSingleOfficialMissing");
        prompt.SkipDeleteOtherStore.Should().BeTrue();
    }

    [Fact]
    public void Official_complete_keeps_standard_confirm_and_asks_delete_other()
    {
        var official = Path.Combine(Path.GetTempPath(), "mmm-emg-off-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(official);
        File.WriteAllText(Path.Combine(official, "Mechabellum.exe"), "x");
        File.WriteAllText(Path.Combine(official, "GameAssembly.dll"), "x");
        try
        {
            var prompt = EmergencyRecoverGuidance.Build(
                new BranchSwitchConfig
                {
                    Enabled = true,
                    SteamLinkPath = Path.Combine(Path.GetDirectoryName(official)!, "Mechabellum"),
                    OfficialStorePath = official,
                    BetaStorePath = Path.Combine(Path.GetDirectoryName(official)!, "Mechabellum_beta"),
                    ActiveBranch = GameBranch.Beta
                },
                preferredLinkPath: Path.Combine(Path.GetDirectoryName(official)!, "Mechabellum"));

            prompt.Kind.Should().Be(EmergencyRecoverPromptKind.Standard);
            prompt.ConfirmKey.Should().Be("ConfirmEmergencyRecoverSingle");
            prompt.SkipDeleteOtherStore.Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(official, true); } catch { /* ignore */ }
        }
    }

    [Theory]
    [InlineData("ConfirmEmergencyRecoverSingle")]
    [InlineData("ConfirmEmergencyRecoverSingleOfficialMissing")]
    [InlineData("ConfirmOrphanDualRepair")]
    public void Emergency_confirms_share_single_folder_lead(string key)
    {
        LocalizationService.Apply("zh-CN");
        LocalizationService.T(key).Should().StartWith(LocalizationService.T("EmergencyRecoverLead"));
    }

    [Fact]
    public void Button_zh_is_restore_official_folder_not_emergency()
    {
        LocalizationService.Apply("zh-CN");
        var label = LocalizationService.T("BranchSwitchEmergencyRecover");
        label.Should().Be("还原官方目录");
        label.Should().NotContain("急救");
        LocalizationService.T("EmergencyRecoverLead")
            .Should().Be("将还原为官方游戏目录（Steam 只保留一个 Mechabellum 目录）。");
        var confirm = LocalizationService.T("ConfirmEmergencyRecoverSingle");
        confirm.Should().NotContain("急救");
        confirm.Should().NotContain("仓");
        LocalizationService.T("BranchStatusNeedsEmergency").Should().Be("需还原官方目录");
    }
}
