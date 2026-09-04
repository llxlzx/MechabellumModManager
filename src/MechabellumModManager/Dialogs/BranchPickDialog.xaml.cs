using System.Windows;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class BranchPickDialog : Window
{
    public BranchPickDialog(string? message = null, string? title = null)
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(title)
            ? LocalizationService.T("Confirm")
            : title;
        MessageText.Text = string.IsNullOrWhiteSpace(message)
            ? LocalizationService.T("WizardPickCurrentBranchPrompt")
            : message;
        OfficialButton.Content = LocalizationService.T("BranchStatusOfficial");
        BetaButton.Content = LocalizationService.T("BranchStatusBeta");
        CancelButton.Content = LocalizationService.T("DialogCancel");
    }

    public GameBranch? Result { get; private set; }

    void Official_Click(object sender, RoutedEventArgs e)
    {
        Result = GameBranch.Official;
        DialogResult = true;
    }

    void Beta_Click(object sender, RoutedEventArgs e)
    {
        Result = GameBranch.Beta;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
