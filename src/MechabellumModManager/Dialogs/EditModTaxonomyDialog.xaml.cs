using System.Windows;
using System.Windows.Controls;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.ViewModels;

namespace MechabellumModManager.Dialogs;

public partial class EditModTaxonomyDialog : Window
{
    public EditModTaxonomyDialog(ModPackage package, UiStrings ui)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(ui);
        InitializeComponent();

        Title = ui.EditModTaxonomy;
        PromptText.Text = package.DisplayName;
        CategoryLabel.Text = ui.FilterCategory;
        ExtraTagsLabel.Text = ui.ExtraTagsHint;
        CancelButton.Content = ui.Cancel;
        OkButton.Content = ui.Ok;

        CategoryCombo.Items.Add(new ComboBoxItem
        {
            Content = ui.CategoryFollowCatalog,
            Tag = null
        });
        foreach (var cat in ModTaxonomy.CatalogWritableCategories)
        {
            CategoryCombo.Items.Add(new ComboBoxItem
            {
                Content = ui.CategoryLabel(cat),
                Tag = cat.ToString()
            });
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(package.CategoryOverride) &&
            ModTaxonomy.TryParseCategory(package.CategoryOverride, out var parsed))
        {
            for (var i = 1; i < CategoryCombo.Items.Count; i++)
            {
                if (CategoryCombo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag as string, parsed.ToString(), StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        CategoryCombo.SelectedIndex = selectedIndex;

        UsageFieldLabel.Text = ui.FilterUsage;
        UsageCombo.Items.Add(new ComboBoxItem
        {
            Content = ui.UsageFollowCatalog,
            Tag = null
        });
        foreach (var usage in ModTaxonomy.CatalogWritableUsages)
        {
            UsageCombo.Items.Add(new ComboBoxItem
            {
                Content = ui.UsageLabel(usage),
                Tag = usage.ToString()
            });
        }
        UsageCombo.SelectedIndex = ModTaxonomy.TryParseUsage(package.UsageOverride, out var usageParsed)
            ? 1 + ModTaxonomy.CatalogWritableUsages.ToList().IndexOf(usageParsed)
            : 0;
        ExtraTagsBox.Text = package.ExtraTags is null || package.ExtraTags.Count == 0
            ? ""
            : string.Join(", ", package.ExtraTags);
    }

    public (string? Override, IReadOnlyList<string> ExtraTags, string? UsageOverride)? Result { get; private set; }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        string? categoryOverride = null;
        if (CategoryCombo.SelectedItem is ComboBoxItem item && item.Tag is string s)
            categoryOverride = s;

        var extras = ModTaxonomy.NormalizeTags(
            ExtraTagsBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        string? usageOverride = null;
        if (UsageCombo.SelectedItem is ComboBoxItem usageItem && usageItem.Tag is string u)
            usageOverride = u;
        Result = (categoryOverride, extras, usageOverride);
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }
}
