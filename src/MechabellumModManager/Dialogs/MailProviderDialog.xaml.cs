using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class MailProviderDialog : Window
{
    public MailProvider? SelectedProvider { get; private set; }

    public MailProviderDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("MailProviderTitle");
        HintText.Text = LocalizationService.T("MailProviderHint");
        QqButton.Content = LocalizationService.T("MailProviderQq");
        GmailButton.Content = LocalizationService.T("MailProviderGmail");
        CancelButton.Content = LocalizationService.T("Cancel");

        var accent = (Style)FindResource("AccentButtonStyle");
        var ghost = (Style)FindResource("GhostButtonStyle");
        QqButton.Style = accent;
        GmailButton.Style = ghost;
        CancelButton.Style = ghost;
    }

    public static MailProvider? Prompt(Window? owner)
    {
        var dialog = new MailProviderDialog();
        if (owner is not null)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.SelectedProvider : null;
    }

    void Qq_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = MailProvider.Qq;
        DialogResult = true;
    }

    void Gmail_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = MailProvider.Gmail;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = null;
        DialogResult = false;
    }
}
