using System.Windows;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

[Collection(StaCollection.Name)]
public class MainWindowSmokeTests
{
    readonly StaTestHost _host;

    public MainWindowSmokeTests(StaTestHost host) => _host = host;

    [Fact]
    public void MainWindow_construct_and_show_does_not_throw()
    {
        _host.Run(() =>
        {
            // Keep the smoke window off-screen / inactive so it does not steal focus
            // or linger maximized on the user's desktop.
            var window = new MainWindow
            {
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowState = WindowState.Normal,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Width = 200,
                Height = 150,
                Opacity = 0
            };

            try
            {
                window.Show();
                // Pump so layout/loaded handlers run, then close the window we opened.
                StaTestHost.Pump();
            }
            finally
            {
                window.Close();
                CloseOwnedWindows();
            }
        }, TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Closes only windows belonging to the test <see cref="Application"/> instance.
    /// Does not touch other processes (Steam, IDE, etc.).
    /// </summary>
    internal static void CloseOwnedWindows()
    {
        var app = Application.Current;
        if (app is null)
            return;

        foreach (Window w in app.Windows.Cast<Window>().ToList())
        {
            try
            {
                if (w.IsVisible || w.IsLoaded)
                    w.Close();
            }
            catch
            {
                // ignore per-window close races during shutdown
            }
        }
    }
}
