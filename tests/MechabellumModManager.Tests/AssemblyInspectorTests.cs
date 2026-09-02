using FluentAssertions;
using MechabellumModManager.Services;

public class AssemblyInspectorTests
{
    [Fact]
    public void QuickCamera_looks_like_MelonMod()
    {
        var dll = @"D:\gongzuo\钢铁指挥官mod管理器开发\_samples\QuickCamera\QuickCamera.dll";
        File.Exists(dll).Should().BeTrue();
        var r = new AssemblyInspector().Inspect(dll);
        r.ReferencesMelonLoader.Should().BeTrue();
        r.LooksLikeMelonMod.Should().BeTrue();
        r.LooksLikeMelonPlugin.Should().BeFalse();
    }
}
