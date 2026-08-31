using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Draws the film-camera icon in the current theme's colours.
/// </summary>
/// <remarks>
/// <para>
/// The artwork is one small vector, so it is drawn on demand rather than
/// shipped as an .ico per theme. Adding a theme is then a palette entry and
/// nothing else — no new asset, no build step, and no chance of the icon and
/// the interface disagreeing about what the accent colour is.
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
        var background = Colour(palette.IconBackground);
        var body = Colour(palette.IconBody);
        var detail = Colour(palette.IconDetail);

        var group = new DrawingGroup();

        var backdrop = rounded
            ? (Geometry)new RectangleGeometry(new Rect(0, 0, 64, 64), 13, 13)
            : new RectangleGeometry(new Rect(0, 0, 64, 64));
        group.Children.Add(new GeometryDrawing(background, null, backdrop));

        foreach (var cx in new[] { 22.0, 40.0 })
        {
            group.Children.Add(new GeometryDrawing(detail, null,
                new EllipseGeometry(new Point(cx, 19), 8.5, 8.5)));
            group.Children.Add(new GeometryDrawing(background, null,
                new EllipseGeometry(new Point(cx, 19), 3, 3)));
        }

        group.Children.Add(new GeometryDrawing(body, null,
            new RectangleGeometry(new Rect(9, 30, 34, 20), 4, 4)));

        var tail = new StreamGeometry();
        using (var ctx = tail.Open())
        {
            ctx.BeginFigure(new Point(45, 36), true, true);
            ctx.LineTo(new Point(55, 31), false, false);
            ctx.LineTo(new Point(55, 45), false, false);
            ctx.LineTo(new Point(45, 40), false, false);
        }
        tail.Freeze();
        group.Children.Add(new GeometryDrawing(detail, null, tail));

        var slot = new SolidColorBrush(background.Color) { Opacity = 0.55 };
        slot.Freeze();
        group.Children.Add(new GeometryDrawing(slot, null,
            new RectangleGeometry(new Rect(15, 37, 12, 3), 1.5, 1.5)));

        group.Freeze();
        return group;
    }

    private static SolidColorBrush Colour(string hex)
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
