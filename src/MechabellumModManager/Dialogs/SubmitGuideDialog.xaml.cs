using System.Windows;
using System.Windows.Navigation;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class SubmitGuideDialog : Window
{
    public SubmitGuideDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("SubmitGuideTitle");
        IntroLabel.Text = LocalizationService.T("SubmitGuideIntro");
        BodyText.Text = LocalizationService.T("SubmitGuideBody");
        TipText.Text = LocalizationService.T("SubmitGuideTip");
        WaitNoticeText.Text = LocalizationService.T("SubmitGuideWaitNotice");
        CancelButton.Content = LocalizationService.T("Cancel");
        GuideButton.Content = LocalizationService.T("SubmitGuideOpen");
        OkButton.Content = LocalizationService.T("SubmitGuideOpenEmail");

        // NavigateUri is required for Hyperlink styling; actual open is region webmail in handler.
        InboxLink.NavigateUri = new Uri(GitHubCommunityLinks.DomesticWebMailUrl);
        InboxLinkText.Text = GitHubCommunityLinks.Inbox;
    }

    void InboxLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
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
        });
        e.Handled = true;
    }

    void Guide_Click(object sender, RoutedEventArgs e) =>
        TryOpen(GitHubCommunityLinks.ContributeGuideUrl);

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
