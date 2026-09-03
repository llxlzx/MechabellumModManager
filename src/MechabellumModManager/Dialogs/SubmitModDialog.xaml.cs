using System.IO;
using System.Windows;
using MechabellumModManager.Services;
using Microsoft.Win32;

namespace MechabellumModManager.Dialogs;

public sealed class SubmitModDialogResult
{
    public string DllPath { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public string Version { get; init; } = "";
    public string Summary { get; init; } = "";
}

public partial class SubmitModDialog : Window
{
    public SubmitModDialog()
    {
        InitializeComponent();
        Title = LocalizationService.T("SubmitModTitle");
        DllLabel.Text = "DLL";
        NameLabel.Text = LocalizationService.T("SubmitModName");
        AuthorLabel.Text = LocalizationService.T("SubmitModAuthor");
        VersionLabel.Text = LocalizationService.T("SubmitModVersion");
        SummaryLabel.Text = LocalizationService.T("SubmitModSummary");
        PickButton.Content = LocalizationService.T("SubmitModPickDll");
        CancelButton.Content = LocalizationService.T("Cancel");
        OkButton.Content = LocalizationService.T("Confirm");
    }

    public SubmitModDialogResult? Result { get; private set; }

    void Pick_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Melon Mod DLL|*.dll|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        DllPathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
            NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);

        try
        {
            var inspector = new AssemblyInspector();
            var info = inspector.Inspect(dialog.FileName);
            if (!string.IsNullOrWhiteSpace(info.MelonName) && string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = info.MelonName;
            if (!string.IsNullOrWhiteSpace(info.MelonAuthor) && string.IsNullOrWhiteSpace(AuthorBox.Text))
                AuthorBox.Text = info.MelonAuthor;
            if (!string.IsNullOrWhiteSpace(info.MelonVersion) && string.IsNullOrWhiteSpace(VersionBox.Text))
                VersionBox.Text = info.MelonVersion;
        }
        catch
        {
            // Optional metadata only.
        }
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        var path = DllPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, LocalizationService.T("SubmitModPickDll"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, LocalizationService.T("SubmitModInvalidExt"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, LocalizationService.T("SubmitModName"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Result = new SubmitModDialogResult
        {
            DllPath = path,
            Name = NameBox.Text.Trim(),
            Author = AuthorBox.Text?.Trim() ?? "",
            Version = VersionBox.Text?.Trim() ?? "",
            Summary = SummaryBox.Text?.Trim() ?? ""
        };
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
