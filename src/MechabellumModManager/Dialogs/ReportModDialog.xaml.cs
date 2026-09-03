using System.Windows;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class ReportModDialog : Window
{
    public ReportModDialog(string modDisplayName)
    {
        InitializeComponent();
        Title = LocalizationService.T("Report");
        ModLabel.Text = modDisplayName;
        CatCheat.Content = LocalizationService.T("ReportCategoryCheat");
        CatVirus.Content = LocalizationService.T("ReportCategoryVirus");
        CatUnrelated.Content = LocalizationService.T("ReportCategoryUnrelated");
        CatOther.Content = LocalizationService.T("ReportCategoryOther");
        OtherHint.Text = LocalizationService.T("ReportOtherHint");
        CancelButton.Content = LocalizationService.T("Cancel");
        OkButton.Content = LocalizationService.T("Confirm");
        UpdateOtherVisibility();
    }

    public ReportCategory Category { get; private set; } = ReportCategory.Cheat;
    public string Notes { get; private set; } = "";

    void Category_Changed(object sender, RoutedEventArgs e) => UpdateOtherVisibility();

    void UpdateOtherVisibility()
    {
        var other = CatOther.IsChecked == true;
        OtherHint.Visibility = other ? Visibility.Visible : Visibility.Collapsed;
        NotesBox.Visibility = other ? Visibility.Visible : Visibility.Collapsed;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        Category =
            CatVirus.IsChecked == true ? ReportCategory.Virus :
            CatUnrelated.IsChecked == true ? ReportCategory.Unrelated :
            CatOther.IsChecked == true ? ReportCategory.Other :
            ReportCategory.Cheat;
        Notes = NotesBox.Text?.Trim() ?? "";

        if (Category == ReportCategory.Other && string.IsNullOrWhiteSpace(Notes))
        {
            MessageBox.Show(
                this,
                LocalizationService.T("ReportOtherHint"),
                LocalizationService.T("Report"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
