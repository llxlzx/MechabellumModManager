using System.Windows;
using System.Windows.Controls;
using MechabellumModManager.Models;

namespace MechabellumModManager.Dialogs;

public partial class TypePickDialog : Window
{
    public TypePickDialog(string? prompt = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(prompt))
            PromptText.Text = prompt;

        TypeCombo.Items.Add(new ComboBoxItem { Content = "MelonMod", Tag = ModPackageType.MelonMod });
        TypeCombo.Items.Add(new ComboBoxItem { Content = "MelonPlugin", Tag = ModPackageType.MelonPlugin });
        TypeCombo.Items.Add(new ComboBoxItem { Content = "MelonUserLibs", Tag = ModPackageType.MelonUserLibs });
        TypeCombo.Items.Add(new ComboBoxItem { Content = "MelonUserData", Tag = ModPackageType.MelonUserData });
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