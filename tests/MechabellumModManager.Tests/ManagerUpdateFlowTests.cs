using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class ManagerUpdateFlowTests
{
    [Fact]
    public async Task Manual_update_download_starts_setup_then_exits()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var payload = Encoding.UTF8.GetBytes("setup");
        var handler = new ScriptedHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                };
                resp.Content.Headers.ContentLength = payload.Length;
                return resp;
            }

            return ScriptedHttpHandler.Json(HttpStatusCode.OK,
                """{"version":"99.0.0","notes":"n","setupUrl":"https://cdn.example/Setup.exe"}""");
        });
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.0.0");
        var downloader = new ManagerSetupDownloader(http, Path.Combine(fx.DataRoot, "dl"));
        var starter = new RecordingStarter();
        var exits = 0;

        var vm = fx.CreateVm(
            confirm: _ => true,
            starter: starter,
            updateChecker: checker,
            setupDownloader: downloader,
            requestProcessExit: () => exits++,
            promptManagerUpdate: async (prompt, ct) =>
            {
                prompt.SetupUrl.Should().Be("https://cdn.example/Setup.exe");
                var path = await downloader.DownloadAsync(prompt.SetupUrl!, prompt.RemoteVersion, cancellationToken: ct);
                return new ManagerUpdateUiResult(ManagerUpdateUiAction.LaunchSetup, path);
            });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        starter.Starts.Should().ContainSingle();
        File.Exists(starter.Starts[0]).Should().BeTrue();
        exits.Should().Be(1);
    }

    [Fact]
    public async Task Startup_skips_dialog_after_session_skip()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK,
                """{"version":"99.0.0","notes":"n","setupUrl":"https://cdn.example/Setup.exe"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.0.0");
        var prompts = 0;

        var vm = fx.CreateVm(
            updateChecker: checker,
            promptManagerUpdate: (_, _) =>
            {
                prompts++;
                return Task.FromResult(new ManagerUpdateUiResult(ManagerUpdateUiAction.Skip));
            });

        await vm.RunStartupManagerUpdateCheckAsync();
        prompts.Should().Be(1);

        await vm.RunStartupManagerUpdateCheckAsync();
        prompts.Should().Be(1, "session skip suppresses further startup prompts");

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);
        prompts.Should().Be(2, "manual check still prompts after skip");
    }

    [Fact]
    public async Task Startup_check_failure_does_not_confirm()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var handler = new ScriptedHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var checker = new UpdateChecker(http, () => "1.0.0");
        var confirms = new List<string>();

        var vm = fx.CreateVm(
            confirm: msg =>
            {
                confirms.Add(msg);
                return true;
            },
            updateChecker: checker);

        await vm.RunStartupManagerUpdateCheckAsync();

        confirms.Should().BeEmpty();
    }

    [Fact]
    public async Task Browse_only_update_opens_url_without_exit()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK,
                """{"version":"99.0.0","notes":"n","setupUrl":"https://github.com/llxlzx/MechabellumModManager/releases/latest"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.0.0");
        var starter = new RecordingStarter();
        var exits = 0;
        var browseOpened = false;

        var vm = fx.CreateVm(
            starter: starter,
            updateChecker: checker,
            requestProcessExit: () => exits++,
            promptManagerUpdate: (prompt, _) =>
            {
                prompt.SetupUrl.Should().BeNull();
                prompt.BrowseUrl.Should().NotBeNullOrEmpty();
                browseOpened = true;
                return Task.FromResult(new ManagerUpdateUiResult(ManagerUpdateUiAction.OpenBrowseUrl));
            });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        browseOpened.Should().BeTrue();
        exits.Should().Be(0);
        starter.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task LaunchSetup_failure_does_not_exit()
    {
        using var fx = Fixture.CreateReadyForCriticalOp();
        var handler = new ScriptedHttpHandler(_ =>
            ScriptedHttpHandler.Json(HttpStatusCode.OK,
                """{"version":"99.0.0","notes":"n","setupUrl":"https://cdn.example/Setup.exe"}"""));
        using var http = new HttpClient(handler);
        var checker = new UpdateChecker(http, () => "1.0.0");
        var exits = 0;
        var notes = new List<string>();

        var vm = fx.CreateVm(
            starter: new ThrowingProcessStarter(),
            updateChecker: checker,
            notify: notes.Add,
            requestProcessExit: () => exits++,
            promptManagerUpdate: (_, _) => Task.FromResult(
                new ManagerUpdateUiResult(ManagerUpdateUiAction.LaunchSetup, @"C:\missing\Setup.exe")));

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        exits.Should().Be(0);
        notes.Should().NotBeEmpty();
    }

    sealed class RecordingStarter : IProcessStarter
    {
        public List<string> Starts { get; } = new();
        public void StartShell(string uriOrPath) => Starts.Add(uriOrPath);
    }

    sealed class ThrowingProcessStarter : IProcessStarter
    {
        public void StartShell(string uriOrPath) =>
            throw new InvalidOperationException("boom");
    }
}
