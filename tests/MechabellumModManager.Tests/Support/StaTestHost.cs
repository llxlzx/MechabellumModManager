using System.Windows;
using System.Windows.Threading;

namespace MechabellumModManager.Tests.Support;

/// <summary>
/// One STA thread with a running dispatcher and the single WPF <see cref="Application"/> the
/// AppDomain is allowed. Every UI test runs on this one thread.
///
/// Both halves of that matter. WPF refuses a second Application, so tests cannot each build
/// their own; and Application.Resources has thread affinity, so a window built on a second
/// thread could not read the styles the first thread created. Sharing one host thread avoids
/// both, at the cost of requiring UI tests to share a collection so they never overlap.
/// </summary>
public sealed class StaTestHost : IDisposable
{
    readonly Thread _thread;
    Dispatcher? _dispatcher;

    public StaTestHost()
    {
        var ready = new ManualResetEventSlim(false);
        Exception? startupError = null;

        _thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;

                // App derives from Application, so constructing it is what claims the
                // AppDomain's one slot; InitializeComponent then merges the resource
                // dictionaries the windows resolve their StaticResources against.
                var app = new App();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.InitializeComponent();
            }
            catch (Exception ex)
            {
                startupError = ex;
            }
            finally
            {
                ready.Set();
            }

            if (startupError is null)
                Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF STA test host"
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!ready.Wait(TimeSpan.FromSeconds(60)))
            throw new TimeoutException("STA test host did not start");
        if (startupError is not null)
            throw new InvalidOperationException("STA test host failed to start", startupError);
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread, rethrowing whatever it throws.</summary>
    public void Run(Action action, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(action);

        var operation = _dispatcher!.InvokeAsync(action);
        if (!operation.Task.Wait(timeout))
            throw new TimeoutException($"UI work did not finish within {timeout}");

        operation.Task.GetAwaiter().GetResult();
    }

    /// <summary>Lets queued layout, binding and Loaded work drain before the test looks at it.</summary>
    public static void Pump() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    public void Dispose() => _dispatcher?.InvokeShutdown();
}

/// <summary>
/// Serialises every test that touches the shared <see cref="StaTestHost"/>. Without this xunit
/// would run the UI test classes in parallel and they would fight over one UI thread.
/// </summary>
[CollectionDefinition(Name)]
public sealed class StaCollection : ICollectionFixture<StaTestHost>
{
    public const string Name = "WPF STA";
}
