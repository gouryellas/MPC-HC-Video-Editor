using System.Globalization;
using System.Windows;
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
