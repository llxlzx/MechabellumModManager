using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public class MainViewModelEditionTests
{
    static void SetConfig(MainViewModelFixture fx, Action<AppConfig> edit)
    {
        var config = fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig());
        edit(config);
        fx.Store.Save(fx.Paths.ConfigPath, config);
    }

    AppConfig Config(MainViewModelFixture fx) => fx.Store.LoadOrDefault(fx.Paths.ConfigPath, () => new AppConfig());

    [Fact]
    public void An_old_config_gets_its_edition_inferred_and_saved_without_other_changes()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SetConfig(fx, c =>
        {
            c.Edition = null;
            c.DownloadRoutePreference = (int)DownloadRoutePreference.AlwaysGitHub;
        });

        var vm = fx.CreateVm();

        vm.Edition.Should().Be(AppEdition.Global);
        Config(fx).Edition.Should().Be("global");
        Config(fx).DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
    }

    [Fact]
    public void A_saved_edition_is_used_as_is()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SetConfig(fx, c =>
        {
            c.Edition = "cn";
            c.DownloadRoutePreference = (int)DownloadRoutePreference.AlwaysGitHub;
        });

        var vm = fx.CreateVm();

        vm.Edition.Should().Be(AppEdition.Cn);
        vm.SelectedEditionCode.Should().Be("cn");
        vm.DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
    }

    [Fact]
    public void Switching_edition_applies_its_route_title_and_update_channel()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SetConfig(fx, c =>
        {
            c.Edition = "cn";
            c.DownloadRoutePreference = (int)DownloadRoutePreference.AlwaysMirror;
        });
        var checker = new UpdateChecker();
        var vm = fx.CreateVm(updateChecker: checker);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        checker.Edition.Should().Be(AppEdition.Cn);
        vm.CommunityBarText.Should().Be(LocalizationService.T("CommunityBarQq"));

        vm.SelectedEditionCode = "global";

        vm.Edition.Should().Be(AppEdition.Global);
        Config(fx).Edition.Should().Be("global");
        Config(fx).DownloadRoutePreference.Should().Be((int)DownloadRoutePreference.AlwaysGitHub);
        vm.DownloadRoute.Should().Be(DownloadRouteKind.GitHubOnly);
        checker.Edition.Should().Be(AppEdition.Global);
        vm.WindowTitle.Should().Contain(LocalizationService.T("EditionShortGlobal"));
        vm.CommunityBarText.Should().Be(LocalizationService.T("CommunityBarDiscord"));
        vm.EditionBadge.Should().Be(LocalizationService.T("EditionShortGlobal"));
        changed.Should().Contain(nameof(vm.WindowTitle));
        vm.LogText.Should().Contain(LocalizationService.T("EditionGlobal"));
    }

    [Fact]
    public void An_unknown_edition_code_from_the_ui_is_ignored()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SetConfig(fx, c => c.Edition = "cn");
        var vm = fx.CreateVm();

        vm.SelectedEditionCode = "intl";

        vm.Edition.Should().Be(AppEdition.Cn);
        Config(fx).Edition.Should().Be("cn");
    }

    [Fact]
    public void The_startup_log_names_the_edition()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SetConfig(fx, c => c.Edition = "global");

        var vm = fx.CreateVm();

        vm.LogText.Should().Contain(LocalizationService.T("EditionGlobal"));
    }

    [Fact]
    public void Community_bar_opens_the_qq_group_or_discord_for_the_edition()
    {
        using var fx = MainViewModelFixture.CreateReady();
        SetConfig(fx, c => c.Edition = "cn");
        string? opened = null;
        var vm = fx.CreateVm(tryOpenExternal: url =>
        {
            opened = url;
            return true;
        });

        vm.OpenCommunityBarCommand.Execute(null);
        opened.Should().Be(GitHubCommunityLinks.QqGroupUrl);

        vm.SelectedEditionCode = "global";
        vm.OpenCommunityBarCommand.Execute(null);
        opened.Should().Be(GitHubCommunityLinks.DiscordFeedbackUrl);
    }
}
