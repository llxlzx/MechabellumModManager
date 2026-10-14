using System.Windows;
using System.Windows.Controls;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class MailProviderDialog : Window
{
    readonly DiagnosticsFollowUp _community;

    public MailProvider? SelectedProvider { get; private set; }

    public DiagnosticsFollowUp FollowUp { get; private set; } = DiagnosticsFollowUp.Cancel;

    /// <param name="edition">Puts that edition's mail service first and highlights it; the other stays available.</param>
    /// <param name="showCommunity">Diagnostics only: adds Discord (Global) or the QQ group (China, when configured).</param>
    public MailProviderDialog(string? hint, AppEdition edition, bool showCommunity = false)
    {
        InitializeComponent();
        Title = LocalizationService.T("MailProviderTitle");
        HintText.Text = string.IsNullOrWhiteSpace(hint)
            ? LocalizationService.T("MailProviderHint")
            : hint;
        QqButton.Content = LocalizationService.T("MailProviderQq");
        GmailButton.Content = LocalizationService.T("MailProviderGmail");
        CancelButton.Content = LocalizationService.T("Cancel");

        var ghost = (Style)FindResource("GhostButtonStyle");
        var primary = (Style)FindResource("PrimaryButtonStyle");
        var preferredIsQq = EditionPolicy.PreferredMail(edition) == MailProvider.Qq;
        QqButton.Style = preferredIsQq ? primary : ghost;
        GmailButton.Style = preferredIsQq ? ghost : primary;
        CommunityButton.Style = ghost;
        CancelButton.Style = ghost;
        if (!preferredIsQq && QqButton.Parent is Panel panel)
        {
            panel.Children.Remove(GmailButton);
            panel.Children.Insert(panel.Children.IndexOf(QqButton), GmailButton);
        }

        _community = edition == AppEdition.Global
            ? DiagnosticsFollowUp.Discord
            : GitHubCommunityLinks.HasQqGroup ? DiagnosticsFollowUp.QqGroup : DiagnosticsFollowUp.Cancel;
        var communityLabel = LocalizationService.T(
            _community == DiagnosticsFollowUp.Discord ? "DiagnosticsMailDiscord" : "DiagnosticsMailQqGroup");
        CommunityButton.Content = communityLabel.Contains("{0}", StringComparison.Ordinal)
            ? string.Format(communityLabel, GitHubCommunityLinks.QqGroupNumber)
            : communityLabel;
        CommunityButton.Visibility = showCommunity && _community != DiagnosticsFollowUp.Cancel
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public static MailProvider? Prompt(Window? owner, AppEdition edition, string? hint = null)
    {
        var dialog = new MailProviderDialog(hint, edition);
        if (owner is not null)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.SelectedProvider : null;
    }

    public static DiagnosticsFollowUp PromptDiagnostics(Window? owner, string hint, AppEdition edition)
    {
        var dialog = new MailProviderDialog(hint, edition, showCommunity: true);
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

    void Community_Click(object sender, RoutedEventArgs e)
    {
        FollowUp = _community;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedProvider = null;
        FollowUp = DiagnosticsFollowUp.Cancel;
        DialogResult = false;
    }
}
