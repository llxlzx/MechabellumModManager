using FluentAssertions;
using MechabellumModManager.Services;

public class AssemblyInspectorTests
{
    [SkippableFact]
    public void QuickCamera_looks_like_MelonMod()
    {
        var dll = SampleModPaths.RequireQuickCameraDll();
        var r = new AssemblyInspector().Inspect(dll);
        r.ReferencesMelonLoader.Should().BeTrue();
        r.LooksLikeMelonMod.Should().BeTrue();
        r.LooksLikeMelonPlugin.Should().BeFalse();
    }
}
