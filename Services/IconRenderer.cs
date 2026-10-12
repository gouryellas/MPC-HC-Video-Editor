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
        // taskbar and a light one on a dark title bar; the reel reads on
        // either. `rounded` is kept for the callers that still pass it.
        _ = rounded;

        var centre = new Point(32, 32);

        // The rim: a machined edge, lit from the upper left. The stops run
        // light, dark, light again rather than fading one way — that second
        // light band two thirds round is what makes metal look like metal
        // rather than like a grey gradient.
        var chrome = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0.35, 1),
            GradientStops =
            {
                new GradientStop(Hex("#FDFEFF"), 0.00),
                new GradientStop(Hex("#C3CAD4"), 0.18),
                new GradientStop(Hex("#6E7783"), 0.42),
                new GradientStop(Hex("#9AA4B1"), 0.58),
                new GradientStop(Hex("#454C57"), 0.78),
                new GradientStop(Hex("#C9D1DB"), 1.00)
            }
        };
        chrome.Freeze();

        group.Children.Add(new GeometryDrawing(chrome, null, new EllipseGeometry(centre, 26, 26)));

        // The body: dark steel, and only steel. Mixing the accent into it —
        // even a quarter of it — turns the reel into a bronze casting, because
        // dark metal is almost neutral and any hue at all reads as the alloy
        // rather than as a tint. The theme gets the ring below instead.
        var face = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.34, 0.28),
            Center = new Point(0.34, 0.28),
            RadiusX = 0.95,
            RadiusY = 0.95,
            GradientStops =
            {
                new GradientStop(Hex("#5A636F"), 0.00),
                new GradientStop(Hex("#2A303A"), 0.55),
                new GradientStop(Hex("#14181F"), 1.00)
            }
        };
        face.Freeze();

        group.Children.Add(new GeometryDrawing(face, null, new EllipseGeometry(centre, 23, 23)));

        // The one coloured part: a band where the body meets the rim. This is
        // what still answers to the theme, and it is the only place a hue can
        // sit on a metal object without deciding what the metal is.
        var band = new Pen(ToBrush(palette.IconBody), 2.2);
        band.Freeze();

        var bandRing = new EllipseGeometry(centre, 22.2, 22.2);
        bandRing.Freeze();
        group.Children.Add(new GeometryDrawing(null, band, bandRing));

        // Five spoke holes, each with a thin lit edge on its upper side: a
        // hole in a solid thing shows the thickness it was cut through.
        var well = ToBrush("#0B0E13");
        var lip = new Pen(new SolidColorBrush(Color.FromArgb(0x8C, 0x9E, 0xA8, 0xB4)), 0.9);
        lip.Freeze();

        for (var i = 0; i < 5; i++)
        {
            var radians = (-90 + i * 72) * Math.PI / 180;
            var at = new Point(32 + 15.4 * Math.Cos(radians), 32 + 15.4 * Math.Sin(radians));
            var hole = new EllipseGeometry(at, 5.5, 6.1);
            hole.Freeze();

            group.Children.Add(new GeometryDrawing(well, lip, hole));
        }

        // The hub, the same metal as the rim, with the spindle hole through it.
        group.Children.Add(new GeometryDrawing(chrome, null, new EllipseGeometry(centre, 7, 7)));
        group.Children.Add(new GeometryDrawing(well, null, new EllipseGeometry(centre, 2.7, 2.7)));

        // The specular: a short arc of reflected light across the top left,
        // which is the single thing that reads as "polished" at a glance.
        var gleam = new StreamGeometry();
        using (var ctx = gleam.Open())
        {
            ctx.BeginFigure(new Point(13.5, 20.5), false, false);
            ctx.ArcTo(new Point(45, 11), new Size(26, 26), 0, false, SweepDirection.Clockwise, true, false);
        }
        gleam.Freeze();

        var shine = new Pen(new SolidColorBrush(Color.FromArgb(0x7A, 0xFF, 0xFF, 0xFF)), 2.2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        shine.Freeze();

        group.Children.Add(new GeometryDrawing(null, shine, gleam));

        group.Freeze();
        return group;
    }

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
