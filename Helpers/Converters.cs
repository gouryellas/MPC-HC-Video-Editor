using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MpcHcVideoEditor.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>
/// A <see cref="Models.Rotation"/> as degrees clockwise, for turning the
/// preview frames the way the finished clip will be turned.
/// </summary>
public class RotationToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Models.Rotation r
            ? r switch
            {
                Models.Rotation.Clockwise => 90.0,
                Models.Rotation.Counterclockwise => -90.0,
                Models.Rotation.UpsideDown => 180.0,
                _ => 0.0
            }
            : 0.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// True to -1, false to 1 — a scale factor that mirrors the preview frames the
/// way the flip filter will mirror the clip.
/// </summary>
/// <remarks>
/// Flip here is vertical inversion, so the caller applies this to ScaleY.
/// </remarks>
public class BoolToFlipScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? -1.0 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Shows an element only for one kind of toolbar entry.
/// </summary>
/// <remarks>
/// One converter with a Kind rather than one class per kind: the template needs
/// the same test three times over, and three near-identical classes would be
/// three places to change the day a fourth kind appears.
/// </remarks>
public class ToolbarKindToVisibilityConverter : IValueConverter
{
    /// <summary>The kind this instance shows, named in the resource that creates it.</summary>
    public Models.ToolbarItemKind Kind { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Models.ToolbarItemKind k && k == Kind ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Dims the button being dragged, so it can be told from the ones moving out of
/// its way.
/// </summary>
public class DragOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? 0.45 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Looks a <see cref="Style"/> up by resource key, for the toolbar buttons —
/// whose colours belong to a style rather than to the item that names it.
/// </summary>
/// <remarks>
/// The toolbar is built from data, and a brush stored in that data would be
/// resolved once and stay resolved: switching theme would leave the four
/// operation buttons painted in the colours of the theme before. Naming a style
/// instead keeps the DynamicResource references inside it, which is what
/// follows a theme change.
///
/// Null, not a throw, for a key that is not there: a button with no style is
/// the ordinary case, and a missing one should leave a plain button rather than
/// take the window down.
/// </remarks>
public class ResourceKeyToStyleConverter : IValueConverter
{
    /// <remarks>
    /// The main window before the application, because these styles are
    /// declared there — they are based on the window's own button style, which
    /// application resources cannot reach.
    ///
    /// An item with no style of its own gets the implicit one, looked up by
    /// type, rather than null. Null is not "nothing to say" to WPF: a Style set
    /// explicitly to null suppresses the implicit style, and the ordinary
    /// toolbar buttons came out with no padding, no margins and no template at
    /// all — a row of flat rectangles jammed against each other.
    /// </remarks>
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var window = Application.Current?.MainWindow;

        if (value is string key && key.Length > 0)
            return window?.TryFindResource(key) as Style
                ?? Application.Current?.TryFindResource(key) as Style;

        return window?.TryFindResource(typeof(Button)) as Style
            ?? Application.Current?.TryFindResource(typeof(Button)) as Style;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// A width above zero, or <see cref="double.NaN"/> — WPF's "as wide as the
/// content" — for anything else.
/// </summary>
/// <remarks>
/// The icon-only toolbar buttons are a fixed 36 wide; the rest are as wide as
/// their label, which is what stopped "Convert video" being clipped. Zero is
/// how an item says it has no opinion.
/// </remarks>
public class PositiveToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d && d > 0 ? d : double.NaN;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// A playback speed as a short badge — <c>2x</c>, <c>1.25x</c>, <c>0.5x</c>.
/// Empty at normal speed.
/// </summary>
/// <remarks>
/// <see cref="Models.Bookmark.SpeedDisplay"/> is prose — "double speed", "half
/// speed" — which belongs in a row that has room for a sentence. Over a
/// 76-pixel frame there is room for three characters.
///
/// Trailing zeros are dropped, so 1.50 reads as 1.5x and 2.00 as 2x.
/// </remarks>
public class SpeedToBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double s && Math.Abs(s - 1.0) > 0.001
            ? s.ToString("0.##", CultureInfo.InvariantCulture) + "x"
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Shows an element only when the speed is not normal. Normal speed is the
/// absence of a setting, and a badge reading "1x" on most clips is a label for
/// the thing not happening.
/// </summary>
public class AlteredSpeedToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double s && Math.Abs(s - 1.0) > 0.001 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Shows an element only when a number is above zero — a fade length, a count,
/// a duration.
/// </summary>
public class PositiveToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d && d > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}
