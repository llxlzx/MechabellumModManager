using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class ModFeedbackCommandTests
{
    static CatalogRoot CatalogWithFeedback(string feedback) => new()
    {
        Mods =
        [
            new CatalogMod
            {
                Id = "cam-aaaaaaaa",
                Name = "Cam",
                File = "mods/cam/Cam.dll",
                Feedback = feedback
            }
        ]
    };

    [Fact]
    public void Catalog_gmail_opens_author_compose_without_copying()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? opened = null;
        var vm = fx.CreateVm(
            copyText: t => copied = t,
            promptMailProvider: () => MailProvider.Gmail,
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback("author@example.com"));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        opened.Should().Contain("to=" + Uri.EscapeDataString("author@example.com"));
        opened.Should().NotContain("llxmod");
        copied.Should().BeNull();
    }

    [Fact]
    public void Catalog_qq_opens_webmail_and_copies_author()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? opened = null;
        var vm = fx.CreateVm(
            copyText: t => copied = t,
            promptMailProvider: () => MailProvider.Qq,
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback("author@example.com"));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        opened.Should().Be("https://wx.mail.qq.com/");
        copied.Should().Contain("To: author@example.com");
    }

    [Fact]
    public void Catalog_mail_cancel_does_not_open_or_copy()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? opened = null;
        var vm = fx.CreateVm(
            copyText: t => copied = t,
            promptMailProvider: () => null,
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback("author@example.com"));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        opened.Should().BeNull();
        copied.Should().BeNull();
    }

    [Fact]
    public void Catalog_https_opens_the_form_without_asking_mail()
    {
        using var fx = Fixture.CreateReady();
        var mailAsked = false;
        string? opened = null;
        var vm = fx.CreateVm(
            promptMailProvider: () =>
            {
                mailAsked = true;
                return MailProvider.Gmail;
            },
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback("https://example.com/form"));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        mailAsked.Should().BeFalse();
        opened.Should().Be("https://example.com/form");
    }

    [Fact]
    public void Catalog_copy_channel_copies_original_and_toasts_feedback_copied()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? notified = null;
        string? opened = null;
        var mailAsked = false;
        var vm = fx.CreateVm(
            notify: m => notified = m,
            copyText: t => copied = t,
            promptMailProvider: () =>
            {
                mailAsked = true;
                return MailProvider.Gmail;
            },
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback("QQ 12345"));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        copied.Should().Be("QQ 12345");
        notified.Should().Be(vm.Ui.FeedbackCopied);
        opened.Should().BeNull();
        mailAsked.Should().BeFalse();
    }

    [Fact]
    public void Library_gmail_subject_uses_display_name()
    {
        using var fx = Fixture.CreateReady();
        string? opened = null;
        var vm = fx.CreateVm(
            promptMailProvider: () => MailProvider.Gmail,
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback("author@example.com"));
        vm.SelectedLibraryMod = vm.Mods.Single();

        vm.SendLibraryModFeedbackCommand.Execute(null);

        opened.Should().Contain("to=" + Uri.EscapeDataString("author@example.com"));
        opened.Should().NotContain("llxmod");
        opened.Should().Contain(Uri.EscapeDataString(vm.SelectedLibraryMod.DisplayName));
    }

    static List<FeedbackChoice> TwoChannels() =>
    [
        new() { Label = "QQ 频道", Url = "https://pd.qq.com/s/mrm8s2di" },
        new() { Label = "Discord", Url = "https://discord.com/users/592955314635014155" },
        new() { Label = " ", Url = "https://example.com/ignored" },
        new() { Label = "bad", Url = "http://example.com/nope" }
    ];

    [Fact]
    public void China_edition_opens_the_qq_channel_without_copying()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? opened = null;
        var asked = false;
        var vm = fx.CreateVm(
            copyText: t => copied = t,
            promptFeedbackChoice: _ =>
            {
                asked = true;
                return null;
            },
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithChoices(
            "国内玩家请到 QQ 频道反馈帖：https://pd.qq.com/s/mrm8s2di\n国际玩家请打开巴巴的 Discord：https://discord.com/users/592955314635014155",
            TwoChannels()));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        asked.Should().BeFalse();
        opened.Should().Be("https://pd.qq.com/s/mrm8s2di");
        copied.Should().BeNull();
    }

    [Fact]
    public void Global_edition_opens_discord_without_copying()
    {
        using var fx = Fixture.CreateReady();
        string? opened = null;
        var vm = fx.CreateVm(tryOpenExternal: url =>
        {
            opened = url;
            return true;
        });
        vm.SelectedEditionCode = EditionPolicy.GlobalCode;
        vm.ApplyCatalogRoot(CatalogWithChoices("note", TwoChannels()));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        opened.Should().Be("https://discord.com/users/592955314635014155");
    }

    [Fact]
    public void Two_line_note_without_choices_still_opens_the_qq_channel()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? opened = null;
        var vm = fx.CreateVm(
            copyText: t => copied = t,
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithFeedback(
            "国内玩家请到 QQ 频道反馈帖：https://pd.qq.com/s/mrm8s2di\n国际玩家请打开巴巴的 Discord：https://discord.com/users/592955314635014155"));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        opened.Should().Be("https://pd.qq.com/s/mrm8s2di");
        copied.Should().BeNull();
    }

    [Fact]
    public void One_https_choice_opens_without_asking()
    {
        using var fx = Fixture.CreateReady();
        var asked = false;
        string? opened = null;
        var vm = fx.CreateVm(
            promptFeedbackChoice: _ =>
            {
                asked = true;
                return null;
            },
            tryOpenExternal: url =>
            {
                opened = url;
                return true;
            });
        vm.ApplyCatalogRoot(CatalogWithChoices(
            "note",
            [new FeedbackChoice { Label = "QQ 频道", Url = "https://pd.qq.com/s/mrm8s2di" }]));
        vm.SelectedCatalogMod = vm.CatalogMods.Single();

        vm.SendCatalogModFeedbackCommand.Execute(null);

        asked.Should().BeFalse();
        opened.Should().Be("https://pd.qq.com/s/mrm8s2di");
    }

    static CatalogRoot CatalogWithChoices(string feedback, IEnumerable<FeedbackChoice> choices) => new()
    {
        Mods =
        [
            new CatalogMod
            {
                Id = "cam-aaaaaaaa",
                Name = "Cam",
                File = "mods/cam/Cam.dll",
                Feedback = feedback,
                FeedbackChoices = choices.ToList()
            }
        ]
    };
}
