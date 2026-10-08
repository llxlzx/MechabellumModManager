using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

public class InboxIsolationTests
{
    [Fact]
    public void Player_candidates_do_not_name_the_inbox_bucket()
    {
        var mirror = "https://mmm-mirror-1312774738.cos.ap-shanghai.myqcloud.com";
        var catalog = new ModCatalogService { MirrorBaseUrl = mirror };
        var updates = new UpdateChecker { MirrorBaseUrl = mirror };
        var joined = string.Join("\n",
            catalog.BuildCatalogCandidates().Select(u => u.AbsoluteUri)
            .Concat(catalog.BuildFileCandidates("mods/quick-mark/QuickMark.dll").Select(u => u.AbsoluteUri))
            .Concat(updates.BuildLatestJsonCandidates().Select(u => u.AbsoluteUri))
            .Concat(RedistEnsureService.BuildArtifactCandidates(mirror, new RedistArtifact { Path = "melonloader/MelonLoader.x64.zip", OriginUrl = "https://example.invalid/m.zip" }, DownloadRouteKind.MirrorFirst).Select(u => u.AbsoluteUri)));
        joined.Should().NotContain("mmm-inbox");
        joined.Should().NotContain("/inbox/");
    }
}
