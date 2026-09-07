using FluentAssertions;
using MechabellumModManager.Services;

public class ProcessProbeTests
{
    [Fact]
    public void IsManagedGameExe_false_when_steam_opened_other_library()
    {
        ProcessProbe.IsManagedGameExe(
                @"D:\chesed\steamapps\common\Mechabellum",
                @"D:\steam\steamapps\common\Mechabellum\Mechabellum.exe")
            .Should().BeFalse();
    }

    [Fact]
    public void IsManagedGameExe_true_when_running_exe_is_manager_path()
    {
        ProcessProbe.IsManagedGameExe(
                @"D:\chesed\steamapps\common\Mechabellum",
                @"D:\chesed\steamapps\common\Mechabellum\Mechabellum.exe")
            .Should().BeTrue();
    }

    [Fact]
    public void IsManagedGameExe_true_when_path_unknown()
    {
        ProcessProbe.IsManagedGameExe(@"D:\chesed\steamapps\common\Mechabellum", null)
            .Should().BeTrue();
    }
}
