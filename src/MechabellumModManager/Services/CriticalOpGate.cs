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
        bool wizardInProgress)
    {
        if (recoveryModalPending)
            return CriticalOpGateLevel.HardBlock;

        // Disk CriticalOp must not be interrupted mid-write.
        if (guardRunning)
            return CriticalOpGateLevel.HardBlock;

        // Busy / branch work: SoftConfirm so user can cancel and exit (avoids zombie process).
        if (busyDialogOpen || branchSwitchBusy)
            return CriticalOpGateLevel.SoftConfirm;

        if (awaitingSteamSettle || wizardInProgress)
            return CriticalOpGateLevel.SoftConfirm;

        return CriticalOpGateLevel.Allow;
    }
}
