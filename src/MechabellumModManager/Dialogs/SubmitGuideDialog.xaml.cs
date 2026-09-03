using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class SubmitGuideDialog : Window
{
    public SubmitGuideDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("SubmitGuideTitle");
        IntroText.Text = LocalizationService.T("SubmitGuideIntro");
        Step1Text.Text = LocalizationService.T("SubmitGuideStep1");
        Step2Text.Text = LocalizationService.T("SubmitGuideStep2");
        Step3Text.Text = LocalizationService.T("SubmitGuideStep3");
        CancelButton.Content = LocalizationService.T("Cancel");
        OkButton.Content = LocalizationService.T("SubmitGuideOpen");
    }

    void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
