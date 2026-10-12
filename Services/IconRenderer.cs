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
        var body = ToBrush(palette.IconBody);
        var detail = ToBrush(palette.IconDetail);

        var group = new DrawingGroup();

        // No tile behind it. The reel is the mark, and a filled square would
        // put a dark block on a light taskbar and a light one on a dark title
        // bar — the shape reads on either when nothing is behind it. `rounded`
        // is kept because the tray and window callers still pass it, and a
        // future backdrop would want it.
        _ = rounded;

        // The disc, with its five spoke holes and the hub cut out of it. One
        // geometry with an even-odd rule rather than holes painted over the
        // top in the background colour: painted holes are only invisible while
        // the thing behind them is the colour they were painted, which on a
        // transparent icon is never.
        var disc = new GeometryGroup { FillRule = FillRule.EvenOdd };
        disc.Children.Add(new EllipseGeometry(new Point(32, 32), 29, 29));

        for (var i = 0; i < 5; i++)
        {
            var angle = -90 + i * 72;
            var radians = angle * Math.PI / 180;

            disc.Children.Add(new EllipseGeometry(
                new Point(32 + 16 * Math.Cos(radians), 32 + 16 * Math.Sin(radians)), 8, 8));
        }

        disc.Children.Add(new EllipseGeometry(new Point(32, 32), 4.5, 4.5));
        disc.Freeze();
        group.Children.Add(new GeometryDrawing(body, null, disc));

        // The rim, a shade darker, so the disc has an edge at sizes where the
        // spokes have closed up into a blur.
        var rim = new EllipseGeometry(new Point(32, 32), 26.5, 26.5);
        rim.Freeze();

        var edge = new Pen(detail, 4);
        edge.Freeze();
        group.Children.Add(new GeometryDrawing(null, edge, rim));

        // The hub ring, which is what keeps the middle from reading as a hole
        // in a doughnut.
        var hub = new EllipseGeometry(new Point(32, 32), 8, 8);
        hub.Freeze();

        var hubPen = new Pen(detail, 2.5);
        hubPen.Freeze();
        group.Children.Add(new GeometryDrawing(null, hubPen, hub));

        group.Freeze();
        return group;
    }

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
