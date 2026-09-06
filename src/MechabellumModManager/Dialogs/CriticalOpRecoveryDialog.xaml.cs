using System.ComponentModel;
using System.Windows;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public enum CriticalOpRecoveryChoice
{
    Continue,
    Repair,
    Abandon
}

public partial class CriticalOpRecoveryDialog : Window
{
    CriticalOpRecoveryChoice? _choice;

    public CriticalOpRecoveryDialog(Action? diagnosticsRequested = null)
    {
        InitializeComponent();
        DiagnosticsRequested = diagnosticsRequested;
        Title = LocalizationService.T("CriticalOpRecoveryTitle");
        TitleHint.Text = LocalizationService.T("CriticalOpRecoveryTitle");
        BodyText.Text = LocalizationService.T("CriticalOpRecoveryBody");
        ContinueButton.Content = LocalizationService.T("CriticalOpRecoveryContinue");
        RepairButton.Content = LocalizationService.T("CriticalOpRecoveryRepair");
        AbandonButton.Content = LocalizationService.T("CriticalOpRecoveryAbandon");
        DiagnosticsButton.Content = LocalizationService.T("CriticalOpRecoveryDiagnostics");
        DiagnosticsButton.Visibility = diagnosticsRequested is null
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public CriticalOpRecoveryChoice Choice => _choice ?? CriticalOpRecoveryChoice.Continue;

    public Action? DiagnosticsRequested { get; }

    void Continue_Click(object sender, RoutedEventArgs e)
    {
        _choice = CriticalOpRecoveryChoice.Continue;
        DialogResult = true;
    }

    void Repair_Click(object sender, RoutedEventArgs e)
    {
        _choice = CriticalOpRecoveryChoice.Repair;
        DialogResult = true;
    }

    void Abandon_Click(object sender, RoutedEventArgs e)
    {
        _choice = CriticalOpRecoveryChoice.Abandon;
        DialogResult = true;
    }

    void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsRequested?.Invoke();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_choice is null)
            e.Cancel = true;
        base.OnClosing(e);
    }
}
