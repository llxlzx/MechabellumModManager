using System.Threading;

namespace MechabellumModManager.Services;

/// <summary>
/// Session-local named Mutex so only one UI instance runs.
/// CLI helpers must not call this.
/// </summary>
public static class SingleInstanceGate
{
    public const string DefaultName = @"Local\MechabellumModManager.SingleInstance";

    public static bool TryAcquire(out Mutex? held) => TryAcquire(DefaultName, out held);

    public static bool TryAcquire(string name, out Mutex? held)
    {
        held = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        held = mutex;
        return true;
    }
}
