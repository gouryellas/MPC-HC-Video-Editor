using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// A colour wheel with a brightness slider, for picking a colour on the spot.
/// </summary>
/// <remarks>
/// Hue runs round the wheel and saturation out from the centre; the slider
/// below sets brightness. <see cref="ColorChanged"/> fires as the colour
/// moves, with the colour as #RRGGBB.
/// </remarks>
public sealed class ColorWheelPicker : StackPanel
{
    private const int Size = 180;

    private readonly Image _wheel;
    private readonly Ellipse _marker;
    private readonly Slider _brightness;
    private readonly Border _preview;
    private readonly TextBlock _hex;

    private double _hue, _saturation, _value = 1;
    private bool _quiet;

    /// <summary>Raised with the new colour as #RRGGBB.</summary>
    public event Action<string>? ColorChanged;

    public ColorWheelPicker(string startHex)
    {
        _wheel = new Image { Width = Size, Height = Size, Source = DrawWheel(1), Cursor = Cursors.Cross };

        _marker = new Ellipse
        {
            Width = 12, Height = 12,
            Stroke = Brushes.White, StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        var canvas = new Canvas { Width = Size, Height = Size };
        canvas.Children.Add(_wheel);
        canvas.Children.Add(_marker);
        canvas.MouseLeftButtonDown += (_, e) => { canvas.CaptureMouse(); PickAt(e.GetPosition(canvas)); };
        canvas.MouseMove += (_, e) => { if (canvas.IsMouseCaptured) PickAt(e.GetPosition(canvas)); };
        canvas.MouseLeftButtonUp += (_, _) => canvas.ReleaseMouseCapture();
        Children.Add(canvas);

        _brightness = new Slider { Minimum = 0, Maximum = 1, Value = 1, Margin = new Thickness(0, 10, 0, 0) };
        _brightness.ValueChanged += (_, _) =>
        {
            _value = _brightness.Value;
            _wheel.Source = DrawWheel(_value);
            Changed();
        };
        Children.Add(_brightness);

        _preview = new Border
        {
            Width = 28, Height = 18,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeService.Brush(nameof(ThemePalette.BorderBrush))
        };
        _hex = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = ThemeService.Brush(nameof(ThemePalette.TextPrimary))
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(_preview);
        row.Children.Add(_hex);
        Children.Add(row);

        // Starts on the colour already chosen, without reporting it back.
        _quiet = true;
        if (TryParse(startHex, out var start))
        {
            ToHsv(start, out _hue, out _saturation, out _value);
            _brightness.Value = _value;
            _wheel.Source = DrawWheel(_value);
        }
        Changed();
        _quiet = false;
    }

    private void PickAt(Point p)
    {
        double dx = p.X - Size / 2.0, dy = p.Y - Size / 2.0;
        var radius = Size / 2.0;
        _hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
        _saturation = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / radius);
        Changed();
    }

    private void Changed()
    {
        var radius = Size / 2.0;
        var angle = _hue * Math.PI / 180;
        Canvas.SetLeft(_marker, radius + Math.Cos(angle) * _saturation * radius - _marker.Width / 2);
        Canvas.SetTop(_marker, radius + Math.Sin(angle) * _saturation * radius - _marker.Height / 2);

        var colour = FromHsv(_hue, _saturation, _value);
        _marker.Stroke = _value > 0.5 ? Brushes.Black : Brushes.White;
        _preview.Background = new SolidColorBrush(colour);
        var hex = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
        _hex.Text = hex;

        if (!_quiet) ColorChanged?.Invoke(hex);
    }

    private static BitmapSource DrawWheel(double value)
    {
        var pixels = new byte[Size * Size * 4];
        var radius = Size / 2.0;
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            double dx = x + 0.5 - radius, dy = y + 0.5 - radius;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > radius) continue;

            var hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
            var c = FromHsv(hue, distance / radius, value);
            var i = (y * Size + x) * 4;

            // A pixel's worth of soft edge, so the rim is not jagged.
            var alpha = (byte)(Math.Clamp(radius - distance, 0, 1) * 255);
            pixels[i] = (byte)(c.B * alpha / 255);
            pixels[i + 1] = (byte)(c.G * alpha / 255);
            pixels[i + 2] = (byte)(c.R * alpha / 255);
            pixels[i + 3] = alpha;
        }

        var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Pbgra32, null, pixels, Size * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255),
                             (byte)Math.Round((b + m) * 255));
    }

    private static void ToHsv(Color colour, out double h, out double s, out double v)
    {
        double r = colour.R / 255.0, g = colour.G / 255.0, b = colour.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;
        if (delta == 0) h = 0;
        else if (max == r) h = 60 * ((g - b) / delta % 6);
        else if (max == g) h = 60 * ((b - r) / delta + 2);
        else h = 60 * ((r - g) / delta + 4);
        if (h < 0) h += 360;
    }

    /// <summary>Reads #RRGGBB.</summary>
    public static bool TryParse(string? hex, out Color colour)
    {
        colour = default;
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        if (!int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return false;
        colour = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
}
