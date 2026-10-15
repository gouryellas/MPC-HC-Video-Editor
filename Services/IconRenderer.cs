using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Draws the application's mark: "MVE" on a black field, the V picked out in
/// the accent cyan.
/// </summary>
/// <remarks>
/// <para>
/// The artwork is one small vector, drawn on demand rather than shipped as a
/// picture per size, so every size is drawn rather than resampled.
/// </para>
/// <para>
/// Its colours are fixed, and deliberately do not follow the theme. A logo is
/// one thing wherever it appears — this mark in the taskbar, the About box
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
    /// <summary>The M and the E.</summary>
    public const string Shell = "#FFFFFF";

    /// <summary>The V — "Video" — the one letter worth naming on its own.</summary>
    public const string Accent = "#17B9EF";

    /// <summary>The field the mark sits on.</summary>
    public const string Field = "#000000";

    /// <summary>
    /// Bahnschrift at its heaviest named weight. Ships with Windows itself —
    /// no font has to be installed or embedded for this to render the same on
    /// every machine it runs on — and its letterforms are cut sharp and
    /// upright rather than Arial Black's rounder terminals, which is the
    /// difference that was actually asked for once the two sat side by side.
    /// </summary>
    private static readonly Typeface MarkTypeface =
        new(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

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

        // Square in every context the mark appears in now — there is no
        // circle or tile shape left for `rounded` to choose between. Kept as
        // a parameter rather than removed: WindowIcon and TrayIcon still ask
        // for it, and a future variant that does want the distinction should
        // not have to change both call sites to get it back.
        _ = rounded;

        // Each letter built and measured on its own, rather than the three
        // laid out as one string, for two reasons: the V needs its own
        // brush, and the tight, almost-touching tracking in the reference
        // is tighter than any font's own kerning offers — it has to be
        // placed by hand.
        var m = Letter("M");
        var v = Letter("V");
        var e = Letter("E");

        // 86%: the letters overlap their neighbour's natural advance width
        // by 14%, which is what turns three separate characters into one
        // mark at a glance, closest at the sizes small enough that the gaps
        // would otherwise be the only thing legible.
        const double advance = 0.86;

        var mX = 0.0;
        var vX = mX + m.Width * advance;
        var eX = vX + v.Width * advance;

        // Bounds of the word as a whole, each letter's local bounds offset by
        // where it actually sits — not measured after placing, which is what
        // produced a word two grid-units wider than the frame it was meant to
        // fit: "MVE" tracked this tight is a wide shape, roughly 2.5:1, and
        // nothing about a 64-unit font size makes that span 64 units.
        var ink = Offset(m.Geometry.Bounds, mX, 0);
        ink.Union(Offset(v.Geometry.Bounds, vX, 0));
        ink.Union(Offset(e.Geometry.Bounds, eX, 0));

        // The black field is a box around the letters, not the square frame
        // they sit in — the reference this is built from has the black fill
        // itself as a wide rectangle, with the frame's own top and bottom
        // left transparent rather than filled in to meet it. <c>boxFill</c>
        // is how much of that box the letters occupy, so the field keeps the
        // same proportions as the reference (roughly 2.5:1) instead of being
        // squared off — the trade this makes on purpose, asked for over the
        // squared version this used to be.
        const double boxFill = 0.80;
        var boxWidth = ink.Width / boxFill;
        var boxHeight = ink.Height / boxFill;

        // Fit that box to the frame by its width, the axis that binds for a
        // shape this wide, with a sliver of margin either side.
        const double frameFill = 0.94;
        var scale = frameFill * 64 / boxWidth;
        var centre = new Point(ink.Left + ink.Width / 2, ink.Top + ink.Height / 2);

        var fieldWidth = boxWidth * scale;
        var fieldHeight = boxHeight * scale;
        group.Children.Add(new GeometryDrawing(ToBrush(Field), null,
            new RectangleGeometry(new Rect((64 - fieldWidth) / 2, (64 - fieldHeight) / 2, fieldWidth, fieldHeight))));

        group.Children.Add(new GeometryDrawing(ToBrush(Shell), null, Place(m.Geometry, mX, scale, centre)));
        group.Children.Add(new GeometryDrawing(ToBrush(Accent), null, Place(v.Geometry, vX, scale, centre)));
        group.Children.Add(new GeometryDrawing(ToBrush(Shell), null, Place(e.Geometry, eX, scale, centre)));

        group.Freeze();
        return group;
    }

    private static Rect Offset(Rect rect, double dx, double dy)
    {
        rect.Offset(dx, dy);
        return rect;
    }

    /// <summary>
    /// One letter, moved to its place in the word, scaled to fit the frame,
    /// and that result centred in it — composed as a single transform so a
    /// letter is never measured in one position and drawn in another.
    /// </summary>
    private static Geometry Place(Geometry letter, double wordX, double scale, Point wordCentre)
    {
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(wordX, 0));
        transform.Children.Add(new TranslateTransform(-wordCentre.X, -wordCentre.Y));
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new TranslateTransform(32, 32));

        var placed = letter.Clone();
        placed.Transform = transform;
        placed.Freeze();
        return placed;
    }

    /// <summary>One character's outline and how far a neighbour's advance should be measured from it.</summary>
    private readonly record struct LetterGeometry(Geometry Geometry, double Width);

    /// <summary>
    /// Builds one letter's outline at a fixed, generous size — large enough
    /// that it is cut down to fit rather than scaled up into softness,
    /// whatever size this is ultimately rendered at.
    /// </summary>
    private static LetterGeometry Letter(string character)
    {
        var text = new FormattedText(
            character, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            MarkTypeface, 64, Brushes.Black, 1.0);

        var geometry = text.BuildGeometry(new Point(0, 0));
        geometry.Freeze();
        return new LetterGeometry(geometry, text.WidthIncludingTrailingWhitespace);
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

    /// <summary>
    /// Writes a multi-resolution .ico — <c>app.ico</c>, <c>tray.ico</c> — cut
    /// from this same drawing, one entry per size asked for.
    /// </summary>
    /// <remarks>
    /// Every entry is drawn at its own size and stored as PNG, the format
    /// <see cref="TrayIcon"/> already writes for a single frame; this is that
    /// same container holding several. PNG-in-ICO is readable since Vista,
    /// carries its own alpha, and is a fraction of the size a BMP frame set
    /// would be.
    /// </remarks>
    public static void SaveIco(string path, ThemePalette palette, params int[] sizes)
    {
        var frames = sizes.Select(size =>
        {
            var frame = BitmapFrame.Create(Render(palette, size, rounded: false));
            using var png = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);
            encoder.Save(png);
            return (Size: size, Data: png.ToArray());
        }).ToList();

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);

        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)frames.Count);

        var offset = 6 + frames.Count * 16;
        foreach (var f in frames)
        {
            w.Write((byte)(f.Size >= 256 ? 0 : f.Size));
            w.Write((byte)(f.Size >= 256 ? 0 : f.Size));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(f.Data.Length);
            w.Write(offset);
            offset += f.Data.Length;
        }

        foreach (var f in frames) w.Write(f.Data);
    }

    /// <summary>A description of the palette, for logging and tests.</summary>
    public static string Describe(ThemePalette palette) =>
        string.Format(CultureInfo.InvariantCulture, "{0}: bg {1}, body {2}, detail {3}",
            palette.Key, palette.IconBackground, palette.IconBody, palette.IconDetail);
}
