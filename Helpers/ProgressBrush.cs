using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Maps 0–100 to the progress bar's fill colour in discrete bands: red below
/// 25, orange to 50, yellow to 75, blue below 100, and a solid green once the
/// job is complete. Banded rather than interpolated — the colour is meant to
/// read as a coarse "how far along is this" at a glance, which a continuous
/// ramp does not give you.
/// </summary>
public class ProgressToBrushConverter : IValueConverter
{
    public static readonly Color Red    = Color.FromRgb(0xD1, 0x1B, 0x1B);
    public static readonly Color Orange = Color.FromRgb(0xE8, 0x6C, 0x0F);
    public static readonly Color Yellow = Color.FromRgb(0xE8, 0xCE, 0x0F);
    public static readonly Color Blue   = Color.FromRgb(0x2F, 0x86, 0xD8);
    public static readonly Color Green  = Color.FromRgb(0x2E, 0xA0, 0x43);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var pct = value switch
        {
            double d => d,
            int i => i,
            _ => 0d
        };

        var brush = new SolidColorBrush(ColorAt(Math.Clamp(pct, 0, 100)));
        brush.Freeze();
        return brush;
    }

    /// <summary>The band <paramref name="pct"/> falls in.</summary>
    public static Color ColorAt(double pct) => pct switch
    {
        >= 100 => Green,
        >= 75  => Blue,
        >= 50  => Yellow,
        >= 25  => Orange,
        _      => Red,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
