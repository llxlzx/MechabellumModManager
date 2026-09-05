using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public enum CriticalOpRecoveryChoice
{
    None,
    Continue,
    Repair
}

public partial class CriticalOpRecoveryDialog : Window
{
    public CriticalOpRecoveryChoice ResultChoice { get; private set; } = CriticalOpRecoveryChoice.None;

    public event Action? DiagnosticsRequested;

    public CriticalOpRecoveryDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("CriticalOpRecoveryTitle");
        TitleText.Text = LocalizationService.T("CriticalOpRecoveryTitle");
        MessageText.Text = LocalizationService.T("CriticalOpRecoveryBody");
        ContinueButton.Content = LocalizationService.T("CriticalOpRecoveryContinue");
        RepairButton.Content = LocalizationService.T("CriticalOpRecoveryRepair");
        DiagnosticsButton.Content = LocalizationService.T("CriticalOpRecoveryDiagnostics");
        Closing += (_, e) =>
        {
            if (ResultChoice == CriticalOpRecoveryChoice.None)
                e.Cancel = true;
        };
    }

    void Continue_Click(object sender, RoutedEventArgs e)
    {
        ResultChoice = CriticalOpRecoveryChoice.Continue;
        DialogResult = true;
    }

    void Repair_Click(object sender, RoutedEventArgs e)
    {
        ResultChoice = CriticalOpRecoveryChoice.Repair;
        DialogResult = true;
    }

    void Diagnostics_Click(object sender, RoutedEventArgs e) =>
        DiagnosticsRequested?.Invoke();
}
