using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class PromptDialog : Window
{
    public PromptDialog(string prompt, string? initial = null)
    {
        InitializeComponent();
        Title = LocalizationService.T("PromptDialogTitle");
        PromptText.Text = prompt;
        InputBox.Text = initial ?? "";
        CancelButton.Content = LocalizationService.T("Cancel");
        OkButton.Content = LocalizationService.T("Ok");
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string? Result { get; private set; }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
