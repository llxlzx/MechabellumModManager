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
            try
            {
                var app = new App();
                app.InitializeComponent();
                var window = new MainWindow();
                window.Show();
                window.Close();
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
}
