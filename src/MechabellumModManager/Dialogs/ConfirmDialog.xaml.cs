using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class ConfirmDialog : Window
{
    public bool IsOptionChecked => OptionCheck.IsChecked == true;

    public ConfirmDialog(
        string message,
        string? title = null,
        bool yesNo = true,
        bool defaultYes = true,
        string? optionLabel = null)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title)
            ? LocalizationService.T(yesNo ? "Confirm" : "Notice")
            : title;
        MessageText.Text = message ?? "";
        if (!string.IsNullOrWhiteSpace(optionLabel))
        {
            OptionCheck.Content = optionLabel;
            OptionCheck.IsChecked = false;
            OptionCheck.Visibility = Visibility.Visible;
        }

        var accent = (Style)FindResource("AccentButtonStyle");
        var ghost = (Style)FindResource("GhostButtonStyle");

        if (yesNo)
        {
            YesButton.Content = LocalizationService.T("DialogYes");
            NoButton.Content = LocalizationService.T("DialogNo");
            NoButton.Visibility = Visibility.Visible;

            // Visual primary must match keyboard default (Enter).
            if (defaultYes)
            {
                YesButton.Style = accent;
                NoButton.Style = ghost;
                YesButton.IsDefault = true;
                NoButton.IsDefault = false;
                NoButton.IsCancel = true;
            }
            else
            {
                YesButton.Style = ghost;
                NoButton.Style = accent;
                YesButton.IsDefault = false;
                NoButton.IsDefault = true;
                YesButton.IsCancel = false;
                NoButton.IsCancel = true;
            }

            Loaded += (_, _) =>
            {
                if (defaultYes)
                    YesButton.Focus();
                else
                    NoButton.Focus();
            };
        }
        else
        {
            YesButton.Content = LocalizationService.T("Ok");
            NoButton.Visibility = Visibility.Collapsed;
            YesButton.Style = accent;
            YesButton.IsDefault = true;
            YesButton.IsCancel = true;
        }
    }

    void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    void No_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
