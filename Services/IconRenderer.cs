using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Draws the application's mark: a film reel with a play triangle through it
/// and a sweep behind it.
/// </summary>
/// <remarks>
/// <para>
/// The artwork is one small vector, drawn on demand rather than shipped as a
/// picture per size, so every size is drawn rather than resampled.
/// </para>
/// <para>
/// Its colours are fixed, and deliberately do not follow the theme. A logo is
/// one thing wherever it appears — a cyan reel in the taskbar, the About box
/// and the readme, whatever palette the window behind it happens to be using.
/// The palette is still taken as an argument so every caller reads as it did,
/// and so a themed variant remains possible without touching them.
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
    /// <summary>The reel, and every light part of the mark.</summary>
    public const string Shell = "#F4F6F9";

    /// <summary>The play triangle and the sweep.</summary>
    public const string Accent = "#17B9EF";

    /// <summary>The field the mark sits on.</summary>
    public const string Field = "#15191F";

    /// <summary>
    /// The mark, on a 64-unit grid, as WPF geometry.
    /// </summary>
    /// <remarks>
    /// Deliberately the same coordinates as the .ico generator, so the icon
    /// baked into the executable and the one drawn here are the same drawing
    /// rather than two that merely resemble each other.
    /// </remarks>
    private static DrawingGroup BuildDrawing(ThemePalette palette, bool rounded)
    {
        var group = new DrawingGroup();

        var accent = ToBrush(Accent);
        var centre = new Point(31, 31);

        // A dark disc under everything. The reel is white, and white on the
        // white of an Explorer window or a light taskbar left nothing but an
        // outline and the triangle — the body of the mark was simply absent.
        // The artwork this is taken from sits on a dark field for the same
        // reason, so the field comes with it.
        //
        // A disc rather than a square: `rounded` asked whether to round the
        // corners of a tile, and a circle has none to round.
        _ = rounded;

        group.Children.Add(new GeometryDrawing(ToBrush(Field), null,
            new EllipseGeometry(centre, 31, 31)));

        // The sweep behind the reel, which is the thing that stops this being
        // a plain circle: an arc of the accent passing under and round to the
        // right, wider than the reel so it shows past the edge.
        var sweep = new StreamGeometry();
        using (var ctx = sweep.Open())
        {
            ctx.BeginFigure(new Point(9.5, 44), false, false);
            ctx.ArcTo(new Point(55, 36), new Size(29.5, 29.5), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        sweep.Freeze();

        var sweepPen = new Pen(accent, 3.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        sweepPen.Freeze();

        group.Children.Add(new GeometryDrawing(null, sweepPen, sweep));

        // The reel: a rim, a hub, and six spokes between them, all one colour,
        // with a dark hairline so it still has an edge on a white taskbar.
        var shell = new GeometryGroup { FillRule = FillRule.Nonzero };

        var rim = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(centre, 25, 25),
            new EllipseGeometry(centre, 18.5, 18.5));

        shell.Children.Add(rim);
        shell.Children.Add(new EllipseGeometry(centre, 7.5, 7.5));

        for (var i = 0; i < 6; i++)
        {
            var spoke = new RectangleGeometry(new Rect(centre.X - 2.7, centre.Y - 19.5, 5.4, 14.5))
            {
                Transform = new RotateTransform(i * 60, centre.X, centre.Y)
            };

            shell.Children.Add(spoke);
        }

        shell.Freeze();

        var reel = ToBrush(Shell);
        group.Children.Add(new GeometryDrawing(reel, null, shell));

        // A faint lighter edge round the field, so the disc has a boundary on
        // a black taskbar as well as on a white one.
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), 1.0);
        edge.Freeze();

        var boundary = new EllipseGeometry(centre, 30.5, 30.5);
        boundary.Freeze();
        group.Children.Add(new GeometryDrawing(null, edge, boundary));

        // The play mark through the middle, large enough to cover the hub —
        // it is the thing being looked for at 16 pixels, where the spokes have
        // closed up into a ring.
        var play = new StreamGeometry();
        using (var ctx = play.Open())
        {
            ctx.BeginFigure(new Point(25, 21), true, true);
            ctx.LineTo(new Point(43, 31), true, false);
            ctx.LineTo(new Point(25, 41), true, false);
        }
        play.Freeze();

        group.Children.Add(new GeometryDrawing(accent, null, play));

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
