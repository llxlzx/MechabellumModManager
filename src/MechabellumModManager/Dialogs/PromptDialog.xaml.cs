using System.Windows;

namespace MechabellumModManager.Dialogs;

public partial class PromptDialog : Window
{
    public PromptDialog(string prompt, string? initial = null)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        InputBox.Text = initial ?? "";
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
