using FluentAssertions;
using MechabellumModManager.Services;

namespace MechabellumModManager.Tests;

public class UninstallCleanupPolicyTests
{
    [Fact]
    public void Iss_asks_before_pure_game_cleanup_and_defaults_to_no()
    {
        var iss = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "installer", "MechabellumModManager.iss"));
        File.Exists(iss).Should().BeTrue(because: iss);
        var text = File.ReadAllText(iss);

        text.Should().Contain("AskPureGameCleanup");
        text.Should().Contain("MB_DEFBUTTON2");
        text.Should().Contain("IDYES");
        text.Should().Contain("--pure-game-cleanup --confirm-delete-other-store");

        var ask = text.IndexOf("AskPureGameCleanup", StringComparison.Ordinal);
        var exec = text.IndexOf("--pure-game-cleanup --confirm-delete-other-store", StringComparison.Ordinal);
        ask.Should().BeGreaterThan(0);
        exec.Should().BeGreaterThan(ask, "cleanup must run only after the optional Yes answer");
    }

    [Fact]
    public void Uninstall_only_request_skips_appdata_and_other_store()
    {
        var request = UninstallCleanupPolicy.CreateManagerOnlyRequest();
        request.SkipAppData.Should().BeTrue();
        request.SkipMelon.Should().BeTrue();
        request.ConfirmDeleteOtherStore.Should().BeFalse();
    }
}
