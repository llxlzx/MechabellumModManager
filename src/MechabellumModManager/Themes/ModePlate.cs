using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MechabellumModManager.Themes;

/// <summary>
/// Shared avatar frame. Top-left and bottom-right take the mode accent;
/// top-right and bottom-left stay silver chamfers. Structure, metal, and
/// transparency are identical for gold and ice — only the accent brush changes.
/// </summary>
public class ModePlate : ContentControl
{
    public static readonly DependencyProperty ChamferProperty = DependencyProperty.Register(
        nameof(Chamfer),
        typeof(double),
        typeof(ModePlate),
        new FrameworkPropertyMetadata(7d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BandProperty = DependencyProperty.Register(
        nameof(Band),
        typeof(double),
        typeof(ModePlate),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Chamfer
    {
        get => (double)GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    public double Band
    {
        get => (double)GetValue(BandProperty);
        set => SetValue(BandProperty, value);
    }

    public ModePlate()
    {
        Loaded += (_, _) => ThemeAccent.Changed += OnAccentChanged;
        Unloaded += (_, _) => ThemeAccent.Changed -= OnAccentChanged;
        SizeChanged += (_, _) =>
        {
            if (ActualWidth < 8 || ActualHeight < 8)
                return;
            var cut = Math.Min(Chamfer, Math.Min(ActualWidth, ActualHeight) / 3);
            Clip = CreateChamfer(0, 0, ActualWidth, ActualHeight, cut);
        };
    }

    void OnAccentChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 8 || h < 8)
            return;

        var cut = Math.Min(Chamfer, Math.Min(w, h) / 3);
        var band = Math.Min(Band, Math.Min(w, h) / 8);
        var accent = FindBrush("AccentAmberBrush", Color.FromRgb(0xD6, 0xAA, 0x63));
        var metal = FindBrush("MetalBandBrush", Color.FromRgb(0xC5, 0xCD, 0xD4));
        var seam = FindBrush("MetalSeamBrush", Color.FromRgb(0x07, 0x0D, 0x12));
        var fill = FindBrush("BgDeepBrush", Color.FromRgb(0x0D, 0x15, 0x1B));

        var outer = CreateChamfer(0, 0, w, h, cut);
        var afterBand = CreateChamfer(band, band, w - 2 * band, h - 2 * band, Math.Max(0, cut - band));
        var seamOuter = band + 0.75;
        var afterSeam = CreateChamfer(seamOuter, seamOuter, w - 2 * seamOuter, h - 2 * seamOuter, Math.Max(0, cut - seamOuter));
        var lineInset = band + 2;
        var innerLine = CreateChamfer(lineInset, lineInset, w - 2 * lineInset, h - 2 * lineInset, Math.Max(0, cut - lineInset));

        dc.DrawGeometry(fill, null, afterSeam);
        dc.DrawGeometry(metal, null, new CombinedGeometry(GeometryCombineMode.Exclude, outer, afterBand));
        dc.DrawGeometry(seam, null, new CombinedGeometry(GeometryCombineMode.Exclude, afterBand, afterSeam));

        var ring = new CombinedGeometry(GeometryCombineMode.Exclude, outer, afterSeam);
        var reach = Math.Max(6, cut);
        dc.DrawGeometry(accent, null, new CombinedGeometry(GeometryCombineMode.Intersect, ring, CornerWedge(w, h, cut, reach, topLeft: true)));
        dc.DrawGeometry(accent, null, new CombinedGeometry(GeometryCombineMode.Intersect, ring, CornerWedge(w, h, cut, reach, topLeft: false)));
        dc.DrawGeometry(null, new Pen(accent, 1), innerLine);
    }

    static Brush FindBrush(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;
        return new SolidColorBrush(fallback);
    }

    public static StreamGeometry CreateChamfer(double x, double y, double w, double h, double cut)
    {
        cut = Math.Max(0, Math.Min(cut, Math.Min(Math.Max(w, 0), Math.Max(h, 0)) / 2));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(x + cut, y), true, true);
            context.LineTo(new Point(x + w - cut, y), true, false);
            context.LineTo(new Point(x + w, y + cut), true, false);
            context.LineTo(new Point(x + w, y + h - cut), true, false);
            context.LineTo(new Point(x + w - cut, y + h), true, false);
            context.LineTo(new Point(x + cut, y + h), true, false);
            context.LineTo(new Point(x, y + h - cut), true, false);
            context.LineTo(new Point(x, y + cut), true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    static StreamGeometry CornerWedge(double w, double h, double cut, double reach, bool topLeft)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (topLeft)
            {
                context.BeginFigure(new Point(0, cut + reach), true, true);
                context.LineTo(new Point(0, 0), true, false);
                context.LineTo(new Point(cut + reach, 0), true, false);
            }
            else
            {
                context.BeginFigure(new Point(w - cut - reach, h), true, true);
                context.LineTo(new Point(w, h), true, false);
                context.LineTo(new Point(w, h - cut - reach), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
