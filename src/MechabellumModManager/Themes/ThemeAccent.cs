using System.Windows.Media;
using MechabellumModManager.Models;

namespace MechabellumModManager.Themes;

/// <summary>
/// Live accent. Official / normal mode is amber gold; the beta branch uses ice blue.
/// Brushes stay unfrozen so every StaticResource consumer updates together.
/// </summary>
public static class ThemeAccent
{
    public readonly record struct Palette(Color Base, Color Hot, Color Pressed);

    public static Palette Normal { get; } = new(
        Color.FromRgb(0xD6, 0xAA, 0x63),
        Color.FromRgb(0xE4, 0xC1, 0x84),
        Color.FromRgb(0xC0, 0x96, 0x4E));

    public static Palette Expedition { get; } = new(
        Color.FromRgb(0x78, 0xB9, 0xDE),
        Color.FromRgb(0x9F, 0xCF, 0xE9),
        Color.FromRgb(0x5A, 0x9B, 0xC4));

    public static event EventHandler? Changed;

    public static Palette For(GameBranch branch) =>
        branch == GameBranch.Beta ? Expedition : Normal;

    public static void Apply(GameBranch branch)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var palette = For(branch);
        Paint(app, "AccentAmberBrush", palette.Base);
        Paint(app, "AccentAmberHotBrush", palette.Hot);
        Paint(app, "AccentPressedBrush", palette.Pressed);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    static void Paint(System.Windows.Application app, string key, Color color)
    {
        if (app.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = color;
    }
}
