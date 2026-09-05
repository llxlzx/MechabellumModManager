using System.Windows;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class ExportDiagnosticsDialog : Window
{
    public ExportDiagnosticsDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("ExportDiagnosticsTitle");
        TitleHint.Text = LocalizationService.T("ExportDiagnosticsTitle");
        ConsentText.Text = LocalizationService.T("ExportDiagnosticsConsent");
        ModeFull.Content = LocalizationService.T("ExportDiagnosticsModeFull");
        ModeStrong.Content = LocalizationService.T("ExportDiagnosticsModeStrong");
        OkButton.Content = LocalizationService.T("ExportDiagnosticsContinue");
        CancelButton.Content = LocalizationService.T("Cancel");
    }

    public DiagnosticsRedactionMode RedactionMode { get; private set; } = DiagnosticsRedactionMode.None;

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        RedactionMode = ModeStrong.IsChecked == true
            ? DiagnosticsRedactionMode.Strong
            : DiagnosticsRedactionMode.None;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
