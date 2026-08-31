using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Pushes a <see cref="ThemePalette"/> into the application's resources, so
/// every <c>DynamicResource</c> in the XAML repaints.
/// </summary>
/// <remarks>
/// <para>
/// The brush keys are the palette's own property names, discovered by
/// reflection rather than listed here. A new colour is then one property on the
/// record and one <c>DynamicResource</c> in the XAML — there is no third place
/// to remember to update, and no way for this file to fall behind the palette.
/// </para>
/// <para>
/// Brushes are frozen. An unfrozen brush handed to controls on the UI thread is
/// mutable shared state, and freezing also lets WPF skip change tracking on
/// several hundred references.
/// </para>
/// </remarks>
public static class ThemeService
{
    private static readonly PropertyInfo[] ColourProperties =
        typeof(ThemePalette)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Where(p => p.Name is not (nameof(ThemePalette.Key) or nameof(ThemePalette.Display)))
            .ToArray();

    /// <summary>The palette currently applied.</summary>
    public static ThemePalette Current { get; private set; } = ThemePalette.Graphite;

    /// <summary>Raised after <see cref="Apply"/> changes the palette.</summary>
    public static event Action<ThemePalette>? Changed;

    /// <summary>
    /// Applies a palette. Safe to call before any window exists, which is what
    /// startup does — the resources are in place before the first window is
    /// measured, so nothing flashes in the previous theme first.
    /// </summary>
    public static void Apply(ThemePalette palette)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        foreach (var property in ColourProperties)
        {
            if (property.GetValue(palette) is not string hex) continue;

            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                resources[property.Name] = brush;

                // The raw colour too: a few places need a Color rather than a
                // Brush, and deriving one at the point of use would mean
                // parsing the same string again.
                resources[property.Name + "Color"] = brush.Color;
            }
            catch
            {
                // A malformed hex in the palette should not take the app down;
                // that key simply keeps whatever it had.
            }
        }

        Current = palette;
        Changed?.Invoke(palette);
    }

    /// <summary>Applies the theme named in settings, or the default.</summary>
    public static void ApplyFromKey(string? key) => Apply(ThemePalette.FromKey(key));

    /// <summary>
    /// A palette brush by role name, for the handful of places that build
    /// controls in code rather than XAML.
    /// </summary>
    /// <remarks>
    /// Falls back to the current text colour rather than throwing: a mistyped
    /// key should show up as an oddly-coloured label, not take a menu down
    /// while it is being built.
    /// </remarks>
    public static Brush Brush(string role)
    {
        if (Application.Current?.Resources[role] is Brush brush) return brush;
        if (Application.Current?.Resources[nameof(ThemePalette.TextPrimary)] is Brush fallback) return fallback;
        return Brushes.Gray;
    }
}
