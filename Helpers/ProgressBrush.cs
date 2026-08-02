using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Maps 0–100 to a continuous colour ramp: black at 0, through red, orange
/// and yellow, to green at 100. Interpolated rather than banded, so the bar
/// shifts smoothly as work progresses instead of jumping at thresholds.
/// </summary>
public class ProgressToBrushConverter : IValueConverter
{
    /// <summary>Ramp stops as (percent, colour), in ascending order.</summary>
    private static readonly (double Stop, Color Colour)[] Ramp =
    {
        (0,   Color.FromRgb(0x00, 0x00, 0x00)),   // black
        (1,   Color.FromRgb(0xD1, 0x1B, 0x1B)),   // red
        (15,  Color.FromRgb(0xE8, 0x6C, 0x0F)),   // orange
        (50,  Color.FromRgb(0xE8, 0xCE, 0x0F)),   // yellow
        (100, Color.FromRgb(0x2E, 0xA0, 0x43)),   // green
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var pct = value switch
        {
            double d => d,
            int i => i,
            _ => 0d
        };
        return new SolidColorBrush(ColourAt(Math.Clamp(pct, 0, 100)));
    }

    /// <summary>Linear interpolation between the two ramp stops surrounding <paramref name="pct"/>.</summary>
    public static Color ColourAt(double pct)
    {
        for (int i = 1; i < Ramp.Length; i++)
        {
            if (pct > Ramp[i].Stop) continue;

            var (lowStop, low) = Ramp[i - 1];
            var (highStop, high) = Ramp[i];

            var span = highStop - lowStop;
            var t = span <= 0 ? 0 : (pct - lowStop) / span;

            return Color.FromRgb(
                (byte)(low.R + (high.R - low.R) * t),
                (byte)(low.G + (high.G - low.G) * t),
                (byte)(low.B + (high.B - low.B) * t));
        }

        return Ramp[^1].Colour;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
