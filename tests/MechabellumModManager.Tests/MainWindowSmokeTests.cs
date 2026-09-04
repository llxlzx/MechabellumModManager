using System.Windows;
using System.Windows.Threading;

namespace MechabellumModManager.Tests;

public class MainWindowSmokeTests
{
    [Fact]
    public void MainWindow_construct_and_show_does_not_throw()
    {
        Exception? error = null;
        var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            App? app = null;
            try
            {
                app = new App();
                app.InitializeComponent();

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

                window.Show();
                // Pump so layout/loaded handlers run, then close the window we opened.
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.Close();

                CloseOwnedWindows(app);
                app.Shutdown();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                try
                {
                    if (app is not null)
                        CloseOwnedWindows(app);
                }
                catch
                {
                    // ignore cleanup races
                }

                try
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
                catch
                {
                    // ignore shutdown races after explicit Close/Shutdown
                }

                done.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(done.Wait(TimeSpan.FromSeconds(30)), "STA smoke timed out");
        Assert.Null(error);
    }

    /// <summary>
    /// Closes only windows belonging to the test <see cref="Application"/> instance.
    /// Does not touch other processes (Steam, IDE, etc.).
    /// </summary>
    static void CloseOwnedWindows(Application app)
    {
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
