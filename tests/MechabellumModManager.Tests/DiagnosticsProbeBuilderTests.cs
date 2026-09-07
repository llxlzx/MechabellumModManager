using FluentAssertions;
using MechabellumModManager.Services;

public class DiagnosticsProbeBuilderTests
{
    [Fact]
    public void Junction_desync_when_link_empty_but_target_store_looks_valid()
    {
        DiagnosticsProbeBuilder.IsJunctionDesync(
            isJunction: true,
            target: @"H:\Steam\steamapps\common\Mechabellum_official",
            official: @"H:\Steam\steamapps\common\Mechabellum_official",
            beta: @"H:\Steam\steamapps\common\Mechabellum_beta",
            linkOk: false,
            officialOk: true,
            betaOk: true).Should().BeTrue();
    }

    [Fact]
    public void Junction_desync_false_when_link_and_store_match()
    {
        DiagnosticsProbeBuilder.IsJunctionDesync(
            isJunction: true,
            target: @"H:\Steam\steamapps\common\Mechabellum_official",
            official: @"H:\Steam\steamapps\common\Mechabellum_official",
            beta: @"H:\Steam\steamapps\common\Mechabellum_beta",
            linkOk: true,
            officialOk: true,
            betaOk: true).Should().BeFalse();
    }
}
