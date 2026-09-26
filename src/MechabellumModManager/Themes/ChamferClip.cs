using System.Windows;
using System.Windows.Media;

namespace MechabellumModManager.Themes;

/// <summary>Clips an element to a small 45° chamfer so panels stay thin, not armored.</summary>
public static class ChamferClip
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.RegisterAttached(
        "Size",
        typeof(double),
        typeof(ChamferClip),
        new PropertyMetadata(0d, OnSizeChanged));

    public static void SetSize(UIElement element, double value) => element.SetValue(SizeProperty, value);

    public static double GetSize(UIElement element) => (double)element.GetValue(SizeProperty);

    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        element.SizeChanged -= Update;
        if (e.NewValue is double size && size > 0)
            element.SizeChanged += Update;
        Update(element, EventArgs.Empty);
    }

    static void Update(object sender, EventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        var cut = GetSize(element);
        var w = element.ActualWidth;
        var h = element.ActualHeight;
        if (cut <= 0 || w <= 1 || h <= 1)
        {
            element.Clip = null;
            return;
        }

        element.Clip = ModePlate.CreateChamfer(0, 0, w, h, Math.Min(cut, Math.Min(w, h) / 2));
    }
}
