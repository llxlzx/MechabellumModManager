namespace MechabellumModManager.Services;

public enum CriticalOpGateLevel
{
    Allow,
    SoftConfirm,
    HardBlock
}

public static class CriticalOpGate
{
    public static CriticalOpGateLevel Classify(
        bool guardRunning,
        bool busyDialogOpen,
        bool branchSwitchBusy,
        bool recoveryModalPending,
        bool awaitingSteamSettle,
        bool wizardWaitingSteam)
    {
        if (recoveryModalPending)
            return CriticalOpGateLevel.HardBlock;

        if (guardRunning || busyDialogOpen || branchSwitchBusy)
            return CriticalOpGateLevel.HardBlock;

        if (awaitingSteamSettle || wizardWaitingSteam)
            return CriticalOpGateLevel.SoftConfirm;

        return CriticalOpGateLevel.Allow;
    }
}
