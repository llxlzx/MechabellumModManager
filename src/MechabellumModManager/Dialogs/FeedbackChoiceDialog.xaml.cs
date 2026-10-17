using System.Windows;
using System.Windows.Controls;
using MechabellumModManager.Services;

namespace MechabellumModManager.Dialogs;

public partial class FeedbackChoiceDialog : Window
{
    public FeedbackChoice? Selected { get; private set; }

    public FeedbackChoiceDialog(IReadOnlyList<FeedbackChoice> choices)
    {
        InitializeComponent();
        Title = LocalizationService.T("FeedbackChoiceTitle");
        HintText.Text = LocalizationService.T("FeedbackChoiceHint");
        CancelButton.Content = LocalizationService.T("Cancel");
        CancelButton.Style = (Style)FindResource("GhostButtonStyle");

        var primary = (Style)FindResource("PrimaryButtonStyle");
        var ghost = (Style)FindResource("GhostButtonStyle");
        var first = true;
        foreach (var choice in choices)
        {
            var button = new Button
            {
                Content = choice.Label,
                MinWidth = 96,
                MinHeight = 36,
                Margin = new Thickness(0, 0, 8, 0),
                Style = first ? primary : ghost,
                Tag = choice
            };
            button.Click += Choice_Click;
            ChoiceButtons.Children.Add(button);
            first = false;
        }
    }

    public static FeedbackChoice? Prompt(Window? owner, IReadOnlyList<FeedbackChoice> choices)
    {
        var dialog = new FeedbackChoiceDialog(choices);
        if (owner is not null)
            dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog.Selected : null;
    }

    void Choice_Click(object sender, RoutedEventArgs e)
    {
        Selected = sender is Button { Tag: FeedbackChoice choice } ? choice : null;
        DialogResult = Selected is not null;
    }

    void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Selected = null;
        DialogResult = false;
    }
}
