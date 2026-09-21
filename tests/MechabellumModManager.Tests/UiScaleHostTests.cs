using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using MechabellumModManager.Dialogs;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

[Collection(StaCollection.Name)]
public class UiScaleHostTests
{
    readonly StaTestHost _host;

    public UiScaleHostTests(StaTestHost host) => _host = host;

    [Fact]
    public void Height_sized_dialog_grows_so_scaled_buttons_stay_fully_visible()
    {
        _host.Run(() =>
        {
            UiScaleHost.EnsureHooked();
            UiScaleHost.SetConfigured("1.5");

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            for (var i = 0; i < 4; i++)
            {
                buttons.Children.Add(new Button
                {
                    Content = "QQ 邮箱",
                    MinWidth = 96,
                    MinHeight = 32,
                    Margin = new Thickness(0, 0, i == 3 ? 0 : 8, 0)
                });
            }

            var window = new Window
            {
                SizeToContent = SizeToContent.Height,
                Width = 520,
                MinWidth = 360,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Opacity = 0,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = buttons
                }
            };

            try
            {
                window.Show();
                StaTestHost.Pump();

                window.Width.Should().BeApproximately(520 * 1.5, 1);
                window.MinWidth.Should().BeApproximately(360 * 1.5, 1);
                buttons.ActualWidth.Should().BeGreaterThanOrEqualTo(buttons.DesiredSize.Width - 1);
            }
            finally
            {
                window.Close();
                UiScaleHost.SetConfigured(UiScalePolicy.Auto);
            }
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Manual_window_keeps_its_own_width_when_scaled()
    {
        _host.Run(() =>
        {
            UiScaleHost.EnsureHooked();
            UiScaleHost.SetConfigured("1.5");

            var window = new Window
            {
                SizeToContent = SizeToContent.Manual,
                Width = 400,
                Height = 300,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Opacity = 0,
                Content = new Border()
            };

            try
            {
                window.Show();
                StaTestHost.Pump();

                window.Width.Should().BeApproximately(400, 1);
            }
            finally
            {
                window.Close();
                UiScaleHost.SetConfigured(UiScalePolicy.Auto);
            }
        }, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Mail_provider_dialog_follows_scale_without_compounding_or_clipping_buttons()
    {
        _host.Run(() =>
        {
            UiScaleHost.EnsureHooked();
            UiScaleHost.SetConfigured("1.5");

            var dialog = new MailProviderDialog(showDiscord: true)
            {
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Opacity = 0
            };

            try
            {
                dialog.Show();
                StaTestHost.Pump();

                dialog.Width.Should().BeApproximately(520 * 1.5, 1);
                AssertButtonsFit(dialog);

                UiScaleHost.SetConfigured("2");
                StaTestHost.Pump();
                dialog.Width.Should().BeApproximately(520 * 2, 1);
                AssertButtonsFit(dialog);

                UiScaleHost.SetConfigured("1");
                StaTestHost.Pump();
                dialog.Width.Should().BeApproximately(520, 1);
                dialog.MinWidth.Should().BeApproximately(360, 1);
                AssertButtonsFit(dialog);
            }
            finally
            {
                dialog.Close();
                UiScaleHost.SetConfigured(UiScalePolicy.Auto);
            }
        }, TimeSpan.FromSeconds(30));
    }

    static void AssertButtonsFit(Window dialog)
    {
        dialog.UpdateLayout();
        var buttons = FindButtons(dialog).Where(button => button.IsVisible).ToList();
        buttons.Should().NotBeEmpty();

        var row = (FrameworkElement)buttons[0].Parent;
        var root = (FrameworkElement)dialog.Content;
        var origin = row.TranslatePoint(new Point(0, 0), root);
        origin.X.Should().BeGreaterThanOrEqualTo(-1);
        (origin.X + row.ActualWidth).Should().BeLessThanOrEqualTo(root.ActualWidth + 1);
        row.ActualWidth.Should().BeGreaterThanOrEqualTo(row.DesiredSize.Width - 1);
        var top = root.TranslatePoint(new Point(0, 0), dialog);
        var bottom = root.TranslatePoint(new Point(root.ActualWidth, root.ActualHeight), dialog);
        top.X.Should().BeGreaterThanOrEqualTo(-1);
        top.Y.Should().BeGreaterThanOrEqualTo(-1);
        bottom.X.Should().BeLessThanOrEqualTo(dialog.ActualWidth + 1);
        bottom.Y.Should().BeLessThanOrEqualTo(dialog.ActualHeight + 1);
        // TranslatePoint is in the client area; ActualHeight includes the caption.
        (dialog.ActualHeight - bottom.Y).Should().BeInRange(8, 56);
    }

    static IEnumerable<Button> FindButtons(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Button button)
                yield return button;
            foreach (var nested in FindButtons(child))
                yield return nested;
        }
    }
}
