using FluentAssertions;
using MechabellumModManager.Services;

public class BranchSwitchArchiveErrorTests
{
    [Fact]
    public void MapArchiveException_access_denied_is_chinese_guidance()
    {
        var mapped = BranchSwitchService.MapArchiveException(
            new UnauthorizedAccessException("Access to the path 'd:\\chesed\\steamapps\\common\\Mechabellum' is denied."));

        mapped.Should().Contain("访问被拒绝");
        mapped.Should().Contain("Steam");
        mapped.Should().Contain("管理员");
        mapped.Should().Contain("denied");
    }
}
