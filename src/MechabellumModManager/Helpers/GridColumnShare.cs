using System.Windows;
using System.Windows.Controls;

namespace MechabellumModManager.Helpers;

/// <summary>
/// Star-like share used when the mod grids assign leftover width themselves.
/// Zero means the column keeps the fixed width declared in XAML.
/// </summary>
public static class GridColumnShare
{
    public static readonly DependencyProperty WeightProperty = DependencyProperty.RegisterAttached(
        "Weight",
        typeof(double),
        typeof(GridColumnShare),
        new PropertyMetadata(0d));

    public static void SetWeight(DependencyObject element, double value) =>
        element.SetValue(WeightProperty, value);

    public static double GetWeight(DependencyObject element) =>
        (double)element.GetValue(WeightProperty);
}
