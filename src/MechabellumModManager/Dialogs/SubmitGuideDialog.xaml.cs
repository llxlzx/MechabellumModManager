using System.Windows;
using System.Windows.Navigation;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class SubmitGuideDialog : Window
{
    readonly AppEdition _edition;
    readonly string _contributeGuideUrl;

    /// <param name="contributeGuideUrl">Submission guide for this edition (mirror copy or GitHub).</param>
    public SubmitGuideDialog(AppEdition edition, string contributeGuideUrl)
    {
        _edition = edition;
        _contributeGuideUrl = contributeGuideUrl;
        InitializeComponent();
        Title = LocalizationService.T("SubmitGuideTitle");
        IntroLabel.Text = LocalizationService.T("SubmitGuideIntro");
        BodyText.Text = LocalizationService.T("SubmitGuideBody");
        TipText.Text = LocalizationService.T("SubmitGuideTip");
        WaitNoticeText.Text = LocalizationService.T("SubmitGuideWaitNotice");
        CancelButton.Content = LocalizationService.T("Cancel");
        GuideButton.Content = LocalizationService.T("SubmitGuideOpen");
        OkButton.Content = LocalizationService.T("SubmitGuideOpenEmail");

        // NavigateUri is required for Hyperlink styling; actual open goes through the mail chooser.
        InboxLink.NavigateUri = new Uri(GitHubCommunityLinks.InboxWebMailUrl(EditionPolicy.PreferredMail(edition)));
        InboxLinkText.Text = GitHubCommunityLinks.Inbox;
    }

    void InboxLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        var picked = MailProviderDialog.Prompt(this, _edition);
        GitHubCommunityLinks.TryOpenInboxWebMail(text =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // Clipboard may be locked.
            }
        }, picked);
        e.Handled = true;
    }

    void Guide_Click(object sender, RoutedEventArgs e) =>
        TryOpen(_contributeGuideUrl);

    void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    static void TryOpen(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // User can still use the primary compose button or copy the address.
        }
    }
}
