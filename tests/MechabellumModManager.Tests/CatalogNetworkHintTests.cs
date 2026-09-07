using FluentAssertions;
using MechabellumModManager.Services;

public class CatalogNetworkHintTests
{
    [Fact]
    public void No_mirror_tells_player_to_fill_mirror_or_enable_proxy()
    {
        var text = CatalogNetworkHint.Format(error: "timeout", mirrorBaseUrl: null, usedCache: false);
        text.Should().Contain("timeout");
        text.Should().Contain(LocalizationService.T("CatalogFetchHintNoMirror"));
    }

    [Fact]
    public void Mirror_set_tells_player_mirror_also_failed()
    {
        var text = CatalogNetworkHint.Format("403", "https://mirror.example", usedCache: false);
        text.Should().Contain("403");
        text.Should().Contain(LocalizationService.T("CatalogFetchHintMirrorFailed"));
    }

    [Fact]
    public void Cache_fallback_keeps_error_and_adds_hint()
    {
        var text = CatalogNetworkHint.Format("dns", null, usedCache: true);
        text.Should().Contain("dns");
        text.Should().Contain(LocalizationService.T("CatalogFetchHintNoMirror"));
    }
}
