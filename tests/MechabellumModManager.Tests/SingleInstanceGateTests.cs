using System.Threading;
using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class SingleInstanceGateTests
{
    [Fact]
    public void TryAcquire_first_succeeds_second_fails_until_dispose()
    {
        var name = @"Local\MechabellumModManager.SingleInstance.Test." + Guid.NewGuid().ToString("N");
        SingleInstanceGate.TryAcquire(name, out var first).Should().BeTrue();
        first.Should().NotBeNull();
        try
        {
            SingleInstanceGate.TryAcquire(name, out var second).Should().BeFalse();
            second.Should().BeNull();
        }
        finally
        {
            first!.Dispose();
        }

        SingleInstanceGate.TryAcquire(name, out var again).Should().BeTrue();
        again!.Dispose();
    }
}
