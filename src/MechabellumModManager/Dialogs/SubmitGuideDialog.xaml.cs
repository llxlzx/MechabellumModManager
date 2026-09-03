using System.Diagnostics;
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

        InboxLink.NavigateUri = new Uri($"mailto:{GitHubCommunityLinks.Inbox}");
        InboxLinkText.Text = GitHubCommunityLinks.Inbox;
    }

    void InboxLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        TryOpen(e.Uri.AbsoluteUri);
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
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // User can still use the primary mailto button or copy the address.
        }
    }
}
