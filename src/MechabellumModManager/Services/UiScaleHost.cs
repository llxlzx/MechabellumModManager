using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MechabellumModManager.Services;

/// <summary>
/// Applies <see cref="UiScalePolicy"/> to each window's root element so dialogs follow the same choice.
/// </summary>
public static class UiScaleHost
{
    const uint MonitorDefaultToNearest = 2;

    static string _configured = UiScalePolicy.Auto;
    static bool _hooked;
    static bool _publishedAuto;
    static double _recommendedAutoScale = 1;

    public static double RecommendedAutoScale => _recommendedAutoScale;

    public static event Action? AutoRecommendationChanged;
    static readonly ConditionalWeakTable<Window, AuthoredWidth> AuthoredWidths = new();

    public static void EnsureHooked()
    {
        if (_hooked)
            return;
        _hooked = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    public static void SetConfigured(string? code)
    {
        _configured = UiScalePolicy.Normalize(code);
        var app = Application.Current;
        if (app is null)
            return;

        var dispatcher = app.Dispatcher;
        if (dispatcher.CheckAccess())
            ApplyOpenWindows(app);
        else if (!dispatcher.HasShutdownStarted)
            _ = dispatcher.BeginInvoke(() => ApplyOpenWindows(app));
    }

    static void ApplyOpenWindows(Application app)
    {
        foreach (Window window in app.Windows)
            Apply(window);
    }

    public static void Apply(Window window)
    {
        if (window.Content is not FrameworkElement root)
            return;

        var (dpiScale, width, height) = ReadMonitor(window);
        PublishAuto(UiScalePolicy.Resolve(UiScalePolicy.Auto, dpiScale, height, width));
        var scale = UiScalePolicy.Resolve(_configured, dpiScale, height, width);
        // Capture before mutating. SizeToContent.Height dialogs keep a fixed design width;
        // LayoutTransform would otherwise measure that width divided by the scale and clip
        // rows that cannot shrink. The hwnd also ignores LayoutTransform when sizing to
        // content, so the window height has to be set from the scaled desired size.
        var authored = CaptureAuthoredWidth(window);
        var widthChanged = FitAuthoredWidth(window, authored, scale);
        var scaleMatches = HasScale(root.LayoutTransform, scale);
        if (!widthChanged && scaleMatches && ScaledHeightFits(window, root, authored))
            return;

        if (!scaleMatches)
        {
            root.LayoutTransform = Math.Abs(scale - 1) < 0.001
                ? Transform.Identity
                : new ScaleTransform(scale, scale);
        }

        FitScaledHeight(window, root, authored);
    }

    static AuthoredWidth CaptureAuthoredWidth(Window window)
    {
        if (AuthoredWidths.TryGetValue(window, out var authored))
            return authored;

        authored = new AuthoredWidth(window.Width, window.MinWidth, window.MaxWidth, window.SizeToContent);
        AuthoredWidths.Add(window, authored);
        return authored;
    }

    /// <summary>
    /// Grows a height-sized dialog so its pre-transform layout slot stays at the authored
    /// width. Resizable windows are left alone.
    /// </summary>
    static bool FitAuthoredWidth(Window window, AuthoredWidth authored, double scale)
    {
        if (authored.SizeToContent != SizeToContent.Height)
            return false;
        if (double.IsNaN(authored.Width) || authored.Width <= 0)
            return false;

        var targetWidth = authored.Width * scale;
        var targetMin = ScalePositive(authored.MinWidth, scale);
        var targetMax = ScalePositive(authored.MaxWidth, scale);

        var changed = false;
        // Drop a stale minimum before shrinking, or the new width is clamped to the old scale.
        if (window.MinWidth > targetWidth)
            changed |= Assign(window.MinWidth, targetMin, value => window.MinWidth = value);
        if (window.MaxWidth < targetWidth)
            changed |= Assign(window.MaxWidth, targetMax, value => window.MaxWidth = value);

        changed |= Assign(window.Width, targetWidth, value => window.Width = value);
        changed |= Assign(window.MinWidth, targetMin, value => window.MinWidth = value);
        changed |= Assign(window.MaxWidth, targetMax, value => window.MaxWidth = value);
        return changed;
    }

    static bool ScaledHeightFits(Window window, FrameworkElement root, AuthoredWidth authored)
    {
        if (authored.SizeToContent is not (SizeToContent.Height or SizeToContent.WidthAndHeight))
            return true;
        if (root.DesiredSize.Height <= 1 || window.ActualHeight <= 1)
            return false;

        var chrome = WindowChrome(window);
        return Math.Abs(window.ActualHeight - (root.DesiredSize.Height + chrome.Height)) <= 2;
    }

    static void FitScaledHeight(Window window, FrameworkElement root, AuthoredWidth authored)
    {
        if (authored.SizeToContent is not (SizeToContent.Height or SizeToContent.WidthAndHeight))
            return;

        var chrome = WindowChrome(window);
        var outerWidth = window.Width;
        if (double.IsNaN(outerWidth) || outerWidth <= 1)
            outerWidth = window.ActualWidth;
        var clientWidth = outerWidth - chrome.Width;
        if (clientWidth <= 1)
            return;

        root.Measure(new Size(clientWidth, double.PositiveInfinity));
        var target = root.DesiredSize.Height + chrome.Height;
        if (double.IsNaN(target) || double.IsInfinity(target) || target <= 1)
            return;

        if (window.SizeToContent != SizeToContent.Manual)
            window.SizeToContent = SizeToContent.Manual;
        Assign(window.Height, target, value => window.Height = value);
    }

    static (double Width, double Height) WindowChrome(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return (0, 0);
            if (!GetWindowRect(hwnd, out var outer) || !GetClientRect(hwnd, out var client))
                return (0, 0);

            var dpi = VisualTreeHelper.GetDpi(window);
            var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
            var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
            var width = (outer.Right - outer.Left - Math.Max(0, client.Right)) / scaleX;
            var height = (outer.Bottom - outer.Top - Math.Max(0, client.Bottom)) / scaleY;
            return (Math.Max(0, width), Math.Max(0, height));
        }
        catch
        {
            return (0, 0);
        }
    }

    static double ScalePositive(double value, double scale)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return value;
        return value * scale;
    }

    static bool Assign(double current, double next, Action<double> set)
    {
        if (double.IsNaN(next) || double.IsInfinity(next))
            return false;
        if (Math.Abs(current - next) <= 0.5)
            return false;
        set(next);
        return true;
    }

    sealed class AuthoredWidth(double width, double minWidth, double maxWidth, SizeToContent sizeToContent)
    {
        public double Width { get; } = width;
        public double MinWidth { get; } = minWidth;
        public double MaxWidth { get; } = maxWidth;
        public SizeToContent SizeToContent { get; } = sizeToContent;
    }

    static void PublishAuto(double scale)
    {
        if (_publishedAuto && Math.Abs(_recommendedAutoScale - scale) < 0.001)
            return;
        _publishedAuto = true;
        _recommendedAutoScale = scale;
        AutoRecommendationChanged?.Invoke();
    }

    static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ReferenceEquals(e.OriginalSource, window))
            return;

        window.LocationChanged -= OnWindowMoved;
        window.LocationChanged += OnWindowMoved;
        window.DpiChanged -= OnWindowDpiChanged;
        window.DpiChanged += OnWindowDpiChanged;
        Apply(window);
    }

    static void OnWindowMoved(object? sender, EventArgs e)
    {
        if (sender is Window window)
            Apply(window);
    }

    static void OnWindowDpiChanged(object sender, DpiChangedEventArgs e)
    {
        if (sender is Window window)
            Apply(window);
    }

    static bool HasScale(Transform transform, double scale)
    {
        if (Math.Abs(scale - 1) < 0.001)
            return transform is null || transform == Transform.Identity || IsScale(transform, 1);
        return IsScale(transform, scale);
    }

    static bool IsScale(Transform transform, double scale) =>
        transform is ScaleTransform existing
        && Math.Abs(existing.ScaleX - scale) < 0.001
        && Math.Abs(existing.ScaleY - scale) < 0.001;

    static (double dpiScale, int pixelWidth, int pixelHeight) ReadMonitor(Window window)
    {
        var dpiScale = 1.0;
        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            if (dpi.DpiScaleY > 0)
                dpiScale = dpi.DpiScaleY;
        }
        catch
        {
            /* window not yet connected to a screen */
        }

        var width = 0;
        var height = 0;
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    width = info.rcMonitor.Right - info.rcMonitor.Left;
                    height = info.rcMonitor.Bottom - info.rcMonitor.Top;
                }
            }
        }
        catch
        {
            /* fall through to the primary screen */
        }

        if (height <= 0)
            height = (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpiScale);
        if (width <= 0)
            width = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpiScale);

        return (dpiScale, width, height);
    }

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }
}
