using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class BusyProgressDialog : Window
{
    bool _allowClose;

    public BusyProgressDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("BusyProgressTitle");
        TitleText.Text = LocalizationService.T("BusyProgressTitle");
        Closing += (_, e) => { if (!_allowClose) e.Cancel = true; };
    }

    public void SetMessage(string message) => MessageText.Text = message ?? "";

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}
