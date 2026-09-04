using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string message, string? title = null, bool yesNo = true, bool defaultYes = true)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title)
            ? LocalizationService.T(yesNo ? "Confirm" : "Notice")
            : title;
        MessageText.Text = message ?? "";

        if (yesNo)
        {
            YesButton.Content = LocalizationService.T("DialogYes");
            NoButton.Content = LocalizationService.T("DialogNo");
            NoButton.Visibility = Visibility.Visible;
            YesButton.IsDefault = defaultYes;
            NoButton.IsCancel = true;
            if (!defaultYes)
            {
                YesButton.IsDefault = false;
                NoButton.IsDefault = true;
                NoButton.Focus();
            }
        }
        else
        {
            YesButton.Content = LocalizationService.T("Ok");
            NoButton.Visibility = Visibility.Collapsed;
            YesButton.IsDefault = true;
            YesButton.IsCancel = true;
        }
    }

    void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    void No_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
