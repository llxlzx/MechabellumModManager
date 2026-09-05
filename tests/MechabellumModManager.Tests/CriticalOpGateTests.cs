using FluentAssertions;
using MechabellumModManager.Services;

public class CriticalOpGateTests
{
    [Theory]
    [InlineData(true, false, false, false, false, false, CriticalOpGateLevel.HardBlock)]
    [InlineData(false, true, false, false, false, false, CriticalOpGateLevel.HardBlock)]
    [InlineData(false, false, true, false, false, false, CriticalOpGateLevel.HardBlock)]
    [InlineData(false, false, false, true, false, false, CriticalOpGateLevel.HardBlock)]
    [InlineData(false, false, false, false, true, false, CriticalOpGateLevel.SoftConfirm)]
    [InlineData(false, false, false, false, false, true, CriticalOpGateLevel.SoftConfirm)]
    [InlineData(false, false, false, false, false, false, CriticalOpGateLevel.Allow)]
    public void Classify_returns_expected_level(
        bool guardRunning,
        bool busyDialogOpen,
        bool branchSwitchBusy,
        bool recoveryModalPending,
        bool awaitingSteamSettle,
        bool wizardInProgress,
        CriticalOpGateLevel expected)
    {
        CriticalOpGate.Classify(
                guardRunning,
                busyDialogOpen,
                branchSwitchBusy,
                recoveryModalPending,
                awaitingSteamSettle,
                wizardInProgress)
            .Should().Be(expected);
    }

    [Fact]
    public void Classify_hard_block_wins_over_soft_confirm()
    {
        CriticalOpGate.Classify(
                guardRunning: true,
                busyDialogOpen: false,
                branchSwitchBusy: false,
                recoveryModalPending: false,
                awaitingSteamSettle: true,
                wizardInProgress: true)
            .Should().Be(CriticalOpGateLevel.HardBlock);
    }
}
