using System.Windows;

namespace MechabellumModManager.Helpers;

/// <summary>
/// Bridges Window DataContext into DataGridColumn.Header bindings.
/// Columns are not in the visual tree, so RelativeSource/Ancestor bindings fail silently.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(
            nameof(Data),
            typeof(object),
            typeof(BindingProxy),
            new UIPropertyMetadata(null));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
