using System.Windows;
using System.Windows.Controls;
using MechabellumModManager.Models;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class TypePickDialog : Window
{
    public TypePickDialog(string? prompt = null)
    {
        InitializeComponent();
        Title = LocalizationService.T("TypePickTitle");
        PromptText.Text = string.IsNullOrWhiteSpace(prompt)
            ? LocalizationService.T("TypePickPrompt")
            : prompt;
        CancelButton.Content = LocalizationService.T("DialogCancel");
        OkButton.Content = LocalizationService.T("DialogOk");

        TypeCombo.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.T("PackageTypeMelonMod"),
            Tag = ModPackageType.MelonMod
        });
        TypeCombo.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.T("PackageTypeMelonPlugin"),
            Tag = ModPackageType.MelonPlugin
        });
        TypeCombo.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.T("PackageTypeMelonUserLibs"),
            Tag = ModPackageType.MelonUserLibs
        });
        TypeCombo.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.T("PackageTypeMelonUserData"),
            Tag = ModPackageType.MelonUserData
        });
        TypeCombo.SelectedIndex = 0;
    }

    public ModPackageType? Result { get; private set; }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (TypeCombo.SelectedItem is ComboBoxItem item && item.Tag is ModPackageType type)
            Result = type;
        else
            Result = ModPackageType.MelonMod;
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
