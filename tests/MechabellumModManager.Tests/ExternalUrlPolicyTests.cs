using FluentAssertions;
using MechabellumModManager.Services;

public class ExternalUrlPolicyTests
{
    [Theory]
    [InlineData("https://github.com/llxlzx/MechabellumModManager/releases/latest")]
    [InlineData("https://wx.mail.qq.com/")]
    [InlineData("mailto:someone@example.com?subject=hi")]
    public void Allows_https_and_mailto(string url)
    {
        ExternalUrlPolicy.IsAllowed(url).Should().BeTrue();
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData(@"\\attacker\share\malware.exe")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData("http://mirror.example/setup.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("steam://run/669330")]
    [InlineData("https://github.com@evil.example/setup.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_everything_the_shell_could_execute(string? url)
    {
        ExternalUrlPolicy.IsAllowed(url).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://cdn.example/MechabellumModManager_Setup_v1.2.8.exe", true)]
    [InlineData("https://cdn.example/Setup.exe?x=1", true)]
    [InlineData("https://github.com/x/y/releases/latest", false)]
    [InlineData("http://cdn.example/Setup.exe", false)]
    [InlineData("mailto:a@b.com", false)]
    [InlineData(null, false)]
    public void IsDownloadableSetupUrl_requires_https_exe(string? url, bool expected)
    {
        ExternalUrlPolicy.IsDownloadableSetupUrl(url).Should().Be(expected);
    }
}
