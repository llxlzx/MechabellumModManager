using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class DiagnosisEngineTests
{
    [Fact]
    public void Qiancengxue_official_071_stale_log_beta_key_mismatch_is_melon_upgrade()
    {
        var snap = Qiancengxue(lastLaunch: null);

        var d = DiagnosisEngine.Evaluate(snap);

        d.Code.Should().Be(DiagnosisCodes.MelonNeedsUpgrade);
        d.RuledOut.Should().Contain(DiagnosisCodes.DualFolderBroken);
        d.Also.Should().Contain(DiagnosisCodes.AcfBetaMismatch);
        d.Also.Should().NotContain(DiagnosisCodes.MelonNeedsUpgrade);
        d.Title.Should().NotBeNullOrWhiteSpace();
        d.Action.Should().NotBeNullOrWhiteSpace();
        d.Evidence.Should().NotBeEmpty();
    }

    [Fact]
    public void Qiancengxue_after_launch_without_log_update_is_loader_not_injected()
    {
        var snap = Qiancengxue(
            lastLaunch: DateTimeOffset.Now.AddMinutes(-5),
            melonNeedsUpgrade: false,
            melonVersion: "0.7.3",
            loaderNotInjected: true);

        var d = DiagnosisEngine.Evaluate(snap);

        d.Code.Should().Be(DiagnosisCodes.LoaderNotInjected);
        d.RuledOut.Should().Contain(DiagnosisCodes.DualFolderBroken);
        d.Also.Should().Contain(DiagnosisCodes.AcfBetaMismatch);
    }

    [Fact]
    public void Qiancengxue_old_melon_and_stale_launch_still_prefers_upgrade()
    {
        var snap = Qiancengxue(
            lastLaunch: DateTimeOffset.Now.AddMinutes(-5),
            loaderNotInjected: true);

        var d = DiagnosisEngine.Evaluate(snap);

        d.Code.Should().Be(DiagnosisCodes.MelonNeedsUpgrade);
        d.Also.Should().Contain(DiagnosisCodes.AcfBetaMismatch);
        d.RuledOut.Should().Contain(DiagnosisCodes.DualFolderBroken);
    }

    [Fact]
    public void Critical_op_beats_wizard_and_melon()
    {
        var snap = new DiagnosisSnapshot
        {
            HasInterruptedCriticalOp = true,
            CriticalOpKind = "BranchDiskWrite",
            CriticalOpDetail = "SwitchToBeta",
            IsWizardOrSettleBlocking = true,
            BranchWizardStep = nameof(BranchWizardStep.AwaitingSteamSettle),
            MelonNeedsUpgrade = true,
            MelonVersion = "0.7.1",
            AcfBetaMismatch = true
        };

        var d = DiagnosisEngine.Evaluate(snap);

        d.Code.Should().Be(DiagnosisCodes.CriticalOpInterrupted);
        d.Evidence.Should().Contain(e => e.Contains("BranchDiskWrite", StringComparison.Ordinal));
    }

    [Fact]
    public void Wizard_or_settle_beats_melon_upgrade()
    {
        var snap = new DiagnosisSnapshot
        {
            IsWizardOrSettleBlocking = true,
            IsAwaitingSteamSettle = true,
            BranchWizardStep = nameof(BranchWizardStep.AwaitingSteamSettle),
            MelonNeedsUpgrade = true,
            MelonVersion = "0.7.1",
            AcfBetaMismatch = true
        };

        var d = DiagnosisEngine.Evaluate(snap);

        d.Code.Should().Be(DiagnosisCodes.WizardOrSettleBlocking);
    }

    [Fact]
    public void Healthy_has_empty_evidence()
    {
        var d = DiagnosisEngine.Evaluate(new DiagnosisSnapshot());

        d.Code.Should().Be(DiagnosisCodes.Healthy);
        d.Evidence.Should().BeEmpty();
        d.Also.Should().BeEmpty();
        d.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Game_missing_is_link_incomplete_code()
    {
        var d = DiagnosisEngine.Evaluate(new DiagnosisSnapshot
        {
            GameMissing = true,
            MelonNeedsUpgrade = true
        });

        d.Code.Should().Be(DiagnosisCodes.GameOrLinkIncomplete);
    }

    [Fact]
    public void Steam_update_unhealthy_beats_melon()
    {
        var d = DiagnosisEngine.Evaluate(new DiagnosisSnapshot
        {
            SteamUpdateUnhealthy = true,
            MelonNeedsUpgrade = true,
            MelonVersion = "0.7.1"
        });

        d.Code.Should().Be(DiagnosisCodes.SteamUpdateUnhealthy);
    }

    [Fact]
    public void Acf_mismatch_is_primary_only_when_nothing_else_matches()
    {
        var d = DiagnosisEngine.Evaluate(new DiagnosisSnapshot
        {
            AcfBetaMismatch = true,
            SteamBetaKey = "public_test",
            ActiveGameBranch = nameof(GameBranch.Official)
        });

        d.Code.Should().Be(DiagnosisCodes.AcfBetaMismatch);
        d.Also.Should().BeEmpty();
    }

    static DiagnosisSnapshot Qiancengxue(
        DateTimeOffset? lastLaunch,
        bool melonNeedsUpgrade = true,
        string melonVersion = "0.7.1",
        bool loaderNotInjected = false)
    {
        return new DiagnosisSnapshot
        {
            MelonNeedsUpgrade = melonNeedsUpgrade,
            MelonVersion = melonVersion,
            LoaderNotInjected = loaderNotInjected,
            LastLaunchRequestedAt = lastLaunch,
            LatestLogAge = TimeSpan.FromDays(8),
            AcfBetaMismatch = true,
            SteamBetaKey = "public_test",
            ActiveGameBranch = nameof(GameBranch.Official),
            DualFolderLooksBroken = false
        };
    }
}
