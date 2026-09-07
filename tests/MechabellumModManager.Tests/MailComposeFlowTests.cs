using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using Fixture = MechabellumModManager.Tests.Support.MainViewModelFixture;

public class MailComposeFlowTests
{
    [Fact]
    public void SubmitMod_cancel_copies_template_without_open_failed()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? notified = null;
        var vm = fx.CreateVm(
            confirm: _ => true,
            notify: m => notified = m,
            copyText: t => copied = t,
            promptMailProvider: () => null);

        vm.SubmitModCommand.Execute(null);

        copied.Should().Contain("To: llxmod@foxmail.com");
        copied.Should().Contain("[Mod投稿/Submit]");
        notified.Should().Be(vm.Ui.MailCancelled);
        vm.LogText.Should().Contain(vm.Ui.MailCancelled);
        vm.LogText.Should().NotContain(vm.Ui.MailOpenFailed);
    }

    [Fact]
    public void SendFeedback_cancel_copies_feedback_template()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? notified = null;
        var vm = fx.CreateVm(
            notify: m => notified = m,
            copyText: t => copied = t,
            promptMailProvider: () => null);

        vm.SendFeedbackCommand.Execute(null);

        copied.Should().Contain("[管理器建议/Feedback]");
        notified.Should().Be(vm.Ui.MailCancelled);
    }

    [Fact]
    public void Report_cancel_copies_report_template()
    {
        using var fx = Fixture.CreateReady();
        string? copied = null;
        string? notified = null;
        var vm = fx.CreateVm(
            confirm: _ => true,
            notify: m => notified = m,
            copyText: t => copied = t,
            promptReport: _ => (ReportCategory.Other, "notes"),
            promptMailProvider: () => null);

        vm.SelectedLibraryMod = vm.Mods[0];
        vm.ReportLibraryModCommand.Execute(null);

        copied.Should().Contain("[Mod举报/Report]");
        notified.Should().Be(vm.Ui.MailCancelled);
        vm.LogText.Should().NotContain(vm.Ui.MailOpenFailed);
    }

    [Fact]
    public void ExportDiagnostics_saves_zip_then_cancel_keeps_zip_without_opening_mail()
    {
        using var fx = Fixture.CreateReady();
        var zipPath = Path.Combine(Path.GetTempPath(), "mmm-mail-" + Guid.NewGuid().ToString("N") + ".zip");
        string? revealed = null;
        string? copied = null;
        string? notified = null;
        var mailAskedAfterReveal = false;
        var vm = fx.CreateVm(
            notify: m => notified = m,
            copyText: t => copied = t,
            promptExportDiagnostics: () => DiagnosticsRedactionMode.None,
            saveZipFile: _ => zipPath,
            revealInExplorer: path => revealed = path,
            promptMailProvider: () =>
            {
                mailAskedAfterReveal = revealed is not null && File.Exists(zipPath);
                return null;
            });

        try
        {
            vm.ExportDiagnosticsCommand.Execute(null);

            File.Exists(zipPath).Should().BeTrue();
            revealed.Should().Be(zipPath);
            mailAskedAfterReveal.Should().BeTrue();
            copied.Should().Contain("[诊断包/Diagnostics]");
            notified.Should().Contain(vm.Ui.MailCancelled);
            notified.Should().Contain(Path.GetFileName(zipPath));
            vm.LogText.Should().NotContain(vm.Ui.MailOpenFailed);
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }
}
