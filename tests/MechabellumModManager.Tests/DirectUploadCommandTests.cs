using FluentAssertions;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class DirectUploadCommandTests
{
    [Fact]
    public void DirectUpload_invokes_prompt()
    {
        using var fx = Fixture.CreateReady();
        var called = false;
        var vm = fx.CreateVm(promptDirectUpload: () => called = true);

        vm.DirectUploadCommand.Execute(null);

        called.Should().BeTrue();
    }
}
