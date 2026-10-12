using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Draws the film-camera icon in the current theme's colors.
/// </summary>
/// <remarks>
/// <para>
/// The artwork is one small vector, so it is drawn on demand rather than
/// shipped as an .ico per theme. Adding a theme is then a palette entry and
/// nothing else — no new asset, no build step, and no chance of the icon and
/// the interface disagreeing about what the accent color is.
/// </para>
/// <para>
/// This cannot reach the executable's own icon. <c>ApplicationIcon</c> is
/// compiled into the exe's Win32 resources, which is what Explorer and a pinned
/// shortcut read; nothing at runtime can change it. What this does cover is the
/// window (title bar, Alt-Tab, the running taskbar button) and the tray.
/// </para>
/// </remarks>
public static class IconRenderer
{
    /// <summary>
    /// The camera, on a 64-unit grid, as WPF geometry.
    /// </summary>
    /// <remarks>
    /// Deliberately the same coordinates as the .ico generator, so the icon
    /// baked into the executable and the one drawn here are the same drawing
    /// rather than two that merely resemble each other.
    /// </remarks>
    private static DrawingGroup BuildDrawing(ThemePalette palette, bool rounded)
    {
        var group = new DrawingGroup();

        // No tile behind it. A filled square puts a dark block on a light
        // taskbar and a light one on a dark title bar; the orb reads on
        // either. `rounded` is kept for the callers that still pass it.
        _ = rounded;

        var hue = Hex(palette.IconBody);
        var centre = new Point(32, 30);
        const double radius = 24;

        // What the orb is sitting on, faded from the middle outwards. A flat
        // ellipse read as a second object at the larger sizes, because a
        // shadow with an edge is not a shadow; this one has no edge to see.
        var contact = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x38, 0, 0, 0), 0.00),
                new GradientStop(Color.FromArgb(0x18, 0, 0, 0), 0.55),
                new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1.00)
            }
        };
        contact.Freeze();

        group.Children.Add(new GeometryDrawing(contact, null,
            new EllipseGeometry(new Point(32, 58), 16, 3.4)));

        // The glass itself: lit from the upper left, deepening to almost black
        // at the lower right. Glass takes a colour the way metal does not, so
        // this is the theme's own icon hue rather than a neutral with a tint
        // added — amber on Graphite, cyan on Midnight, and so on.
        var glass = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.33, 0.26),
            Center = new Point(0.33, 0.26),
            RadiusX = 0.95,
            RadiusY = 0.95,
            GradientStops =
            {
                new GradientStop(Blend(hue, Colors.White, 0.62), 0.00),
                new GradientStop(hue, 0.42),
                new GradientStop(Shade(hue, 0.46), 0.82),
                new GradientStop(Shade(hue, 0.20), 1.00)
            }
        };
        glass.Freeze();

        group.Children.Add(new GeometryDrawing(glass, null, new EllipseGeometry(centre, radius, radius)));

        // A lit edge all the way round, which is what stops a sphere reading
        // as a flat circle once the gradient is too small to see.
        var rim = new Pen(new SolidColorBrush(Color.FromArgb(0x7A, 0xFF, 0xFF, 0xFF)), 1.0);
        rim.Freeze();
        group.Children.Add(new GeometryDrawing(null, rim, new EllipseGeometry(centre, radius - 0.5, radius - 0.5)));

        // The gloss: the top of the sphere reflecting the sky, cut off by a
        // flatter curve across the middle. Two arcs, not a blurred ellipse —
        // this is the shape the reflection actually has.
        var gloss = new StreamGeometry();
        using (var ctx = gloss.Open())
        {
            ctx.BeginFigure(new Point(32 - 22, 28), true, true);
            ctx.ArcTo(new Point(32 + 22, 28), new Size(22, 22), 0, false, SweepDirection.Clockwise, true, false);
            ctx.ArcTo(new Point(32 - 22, 28), new Size(27, 27), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        gloss.Freeze();

        var sheen = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0xC4, 0xFF, 0xFF, 0xFF), 0.00),
                new GradientStop(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF), 1.00)
            }
        };
        sheen.Freeze();

        group.Children.Add(new GeometryDrawing(sheen, null, gloss));

        // The specular: the small bright spot where the light source itself is
        // reflected, up and left of centre.
        var spot = new EllipseGeometry(new Point(23, 18), 6.4, 3.6)
        {
            Transform = new RotateTransform(-28, 23, 18)
        };
        spot.Freeze();

        var spotLight = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0xD8, 0xFF, 0xFF, 0xFF), 0.00),
                new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.00)
            }
        };
        spotLight.Freeze();

        group.Children.Add(new GeometryDrawing(spotLight, null, spot));

        // The play mark, slightly right of centre so it sits optically in the
        // middle — a triangle centred by its bounding box always looks left.
        var play = new StreamGeometry();
        using (var ctx = play.Open())
        {
            ctx.BeginFigure(new Point(26, 19), true, true);
            ctx.LineTo(new Point(45, 30), true, false);
            ctx.LineTo(new Point(26, 41), true, false);
        }
        play.Freeze();

        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)), null, play));

        // Light bouncing back up into the bottom of the glass. Shorter and
        // fainter than it first was: struck across the full width at 256 it
        // stopped being a reflection and became a smile.
        var bounce = new StreamGeometry();
        using (var ctx = bounce.Open())
        {
            ctx.BeginFigure(new Point(20, 45.5), false, false);
            ctx.ArcTo(new Point(44, 45.5), new Size(19, 19), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        bounce.Freeze();

        var bounced = new Pen(new SolidColorBrush(Color.FromArgb(0x3E, 0xFF, 0xFF, 0xFF)), 1.4)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        bounced.Freeze();

        group.Children.Add(new GeometryDrawing(null, bounced, bounce));

        group.Freeze();
        return group;
    }

    /// <summary>
    /// The same hue at a fraction of its brightness. Multiplying the channels
    /// keeps the hue where it was, which darkening towards grey would not.
    /// </summary>
    private static Color Shade(Color colour, double amount) =>
        Color.FromRgb((byte)(colour.R * amount), (byte)(colour.G * amount), (byte)(colour.B * amount));

    /// <summary>A palette colour as a <see cref="Color"/>.</summary>
    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>
    /// Two colours mixed, <paramref name="amount"/> of the way from the first
    /// to the second.
    /// </summary>
    private static Color Blend(Color from, Color to, double amount) =>
        Color.FromRgb((byte)(from.R + (to.R - from.R) * amount),
                      (byte)(from.G + (to.G - from.G) * amount),
                      (byte)(from.B + (to.B - from.B) * amount));

    /// <summary>
    /// A frozen brush from a palette hex string. Named for what it returns
    /// rather than for the color it carries: a method called <c>Color</c>
    /// would shadow the <see cref="Color"/> type this casts to.
    /// </summary>
    private static SolidColorBrush ToBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>Renders the icon at one size.</summary>
    private static BitmapSource Render(ThemePalette palette, int size, bool rounded)
    {
        var drawing = BuildDrawing(palette, rounded);

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.PushTransform(new ScaleTransform(size / 64.0, size / 64.0));
            ctx.DrawDrawing(drawing);
            ctx.Pop();
        }

        var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    /// <summary>
    /// The mark drawn at one size, for generating icon files.
    /// </summary>
    /// <remarks>
    /// Drawn at the size asked for rather than scaled from another one: the
    /// geometry is resolution-independent, and a 256 enlarged from a 64 is
    /// four times the pixels and none of the detail.
    /// </remarks>
    public static BitmapSource At(ThemePalette palette, int size, bool rounded = false) =>
        Render(palette, size, rounded);

    /// <summary>
    /// The window icon for a theme, at the sizes WPF picks between.
    /// </summary>
    public static BitmapFrame WindowIcon(ThemePalette palette)
    {
        // 64 is a good compromise: WPF downsamples it for the title bar and
        // uses it directly for Alt-Tab, and a single frame avoids building an
        // encoder for something that is redrawn in milliseconds anyway.
        return BitmapFrame.Create(Render(palette, 64, rounded: true));
    }

    /// <summary>
    /// A tray icon for a theme.
    /// </summary>
    /// <remarks>
    /// Goes through an in-memory .ico rather than <c>Icon.FromHandle</c>: that
    /// route hands out a GDI handle the caller must destroy, and a tray icon
    /// swapped on every theme change is exactly where a leaked handle would
    /// accumulate unnoticed.
    /// </remarks>
    public static System.Drawing.Icon TrayIcon(ThemePalette palette, int size = 32)
    {
        var frame = BitmapFrame.Create(Render(palette, size, rounded: false));

        using var png = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(frame);
        encoder.Save(png);
        var data = png.ToArray();

        using var ico = new MemoryStream();
        using (var w = new BinaryWriter(ico, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)1);
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(data.Length);
            w.Write(22);
            w.Write(data);
        }

        ico.Position = 0;
        return new System.Drawing.Icon(ico);
    }

    /// <summary>Applies the theme's icon to a window.</summary>
    public static void ApplyTo(Window window, ThemePalette palette)
    {
        try { window.Icon = WindowIcon(palette); }
        catch { /* an icon that will not draw is not worth failing a window over */ }
    }

    /// <summary>A description of the palette, for logging and tests.</summary>
    public static string Describe(ThemePalette palette) =>
        string.Format(CultureInfo.InvariantCulture, "{0}: bg {1}, body {2}, detail {3}",
            palette.Key, palette.IconBackground, palette.IconBody, palette.IconDetail);
}
