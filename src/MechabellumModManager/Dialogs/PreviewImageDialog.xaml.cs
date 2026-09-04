using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class PreviewImageDialog : Window
{
    public PreviewImageDialog(ImageSource source)
    {
        InitializeComponent();
        Title = LocalizationService.T("PreviewImageTitle");
        HintText.Text = LocalizationService.T("PreviewImageHint");
        Preview.Source = source;
    }

    void Close_Click(object sender, MouseButtonEventArgs e) => Close();

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.Space)
            Close();
    }
}
