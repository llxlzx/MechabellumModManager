using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class MailProviderDialog : Window
{
    public MailProvider? SelectedProvider { get; private set; }

    public DiagnosticsFollowUp FollowUp { get; private set; } = DiagnosticsFollowUp.Cancel;

    public MailProviderDialog(string? hint = null, bool showDiscord = false)
    {
        InitializeComponent();
        Title = LocalizationService.T("MailProviderTitle");
        HintText.Text = string.IsNullOrWhiteSpace(hint)
            ? LocalizationService.T("MailProviderHint")
            : hint;
        QqButton.Content = LocalizationService.T("MailProviderQq");
        GmailButton.Content = LocalizationService.T("MailProviderGmail");
        DiscordButton.Content = LocalizationService.T("DiagnosticsMailDiscord");
        DiscordButton.Visibility = showDiscord ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Content = LocalizationService.T("Cancel");

        var ghost = (Style)FindResource("GhostButtonStyle");
        QqButton.Style = ghost;
        GmailButton.Style = ghost;
        DiscordButton.Style = ghost;
        CancelButton.Style = ghost;
    }

    public static MailProvider? Prompt(Window? owner, string? hint = null)
    {
        var dialog = new MailProviderDialog(hint);
        if (owner is not null)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.SelectedProvider : null;
    }

    public static DiagnosticsFollowUp PromptDiagnostics(Window? owner, string hint)
    {
        var dialog = new MailProviderDialog(hint, showDiscord: true);
        if (owner is not null)
            dialog.Owner = owner;
        dialog.ShowDialog();
        return dialog.FollowUp;
    }

    void Qq_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = MailProvider.Qq;
        FollowUp = DiagnosticsFollowUp.Qq;
        DialogResult = true;
    }

    void Gmail_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = MailProvider.Gmail;
        FollowUp = DiagnosticsFollowUp.Gmail;
        DialogResult = true;
    }

    void Discord_Click(object sender, RoutedEventArgs e)
    {
        FollowUp = DiagnosticsFollowUp.Discord;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = null;
        FollowUp = DiagnosticsFollowUp.Cancel;
        DialogResult = false;
    }
}
