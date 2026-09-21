using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Draws each bookmark onto the timeline as a pair of brackets, so where the
/// cuts fall is visible at a glance without painting over the bar.
/// </summary>
/// <remarks>
/// Drawn rather than composed from XAML elements. Positioning depends on the
/// control's own width, which a Canvas-based ItemsControl can only reach
/// through converters fed by the container's ActualWidth — awkward, and it
/// re-lays-out on every resize. Rendering directly re-reads the width each
/// pass and needs no per-item visual.
/// </remarks>
public class TimelineRanges : FrameworkElement
{
    /// <summary>
    /// Every mark, whatever the cut behind it is doing.
    /// </summary>
    /// <remarks>
    /// One colour rather than four. The marks used to be amber, teal, salmon
    /// and red by state, which turned the bar into a colour key the moment more
    /// than a couple of cuts existed. White reads at two pixels against every
    /// theme's track.
    ///
    /// The states still worth carrying here are carried by shape instead, which
    /// needs no key: a checked cut gets a tick, and an open bookmark gets its
    /// opening bracket and no closing one. Inversion is not among them — it
    /// changes what is written, not what will be written, and the row says it
    /// in a word.
    /// </remarks>
    private static readonly Brush MarkerBrush = Frozen("#FFFFFF");

    /// <summary>
    /// A bookmark still waiting for its closing timestamp.
    /// </summary>
    /// <remarks>
    /// The one place colour still earns its keep. A lone opening bracket is
    /// already the shape of an unfinished cut, but on a busy bar it is easy to
    /// read as one half of a pair whose other half is off somewhere; red says
    /// at a glance that this one is waiting on you, and it is the state that
    /// stops the cut being acted on at all.
    /// </remarks>
    private static readonly Brush IncompleteBrush = Frozen("#F44747");

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public static readonly DependencyProperty BookmarksProperty =
        DependencyProperty.Register(nameof(Bookmarks), typeof(IEnumerable), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnBookmarksChanged));

    public static readonly DependencyProperty DurationSecondsProperty =
        DependencyProperty.Register(nameof(DurationSeconds), typeof(double), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Bookmarks
    {
        get => (IEnumerable?)GetValue(BookmarksProperty);
        set => SetValue(BookmarksProperty, value);
    }

    /// <summary>Total video length, the denominator for every position.</summary>
    public double DurationSeconds
    {
        get => (double)GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    private static void OnBookmarksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (TimelineRanges)d;
        self.Rewire(e.OldValue as IEnumerable, e.NewValue as IEnumerable);
    }

    /// <summary>
    /// Follows both the collection and each bookmark, so dragging a speed
    /// slider or checking a box repaints without anything else prompting it.
    /// </summary>
    private void Rewire(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= OnCollectionChanged;
        if (oldValue != null)
            foreach (var item in oldValue.OfType<Bookmark>())
                item.PropertyChanged -= OnBookmarkChanged;

        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += OnCollectionChanged;
        if (newValue != null)
            foreach (var item in newValue.OfType<Bookmark>())
                item.PropertyChanged += OnBookmarkChanged;

        InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (var item in e.OldItems.OfType<Bookmark>())
                item.PropertyChanged -= OnBookmarkChanged;
        if (e.NewItems != null)
            foreach (var item in e.NewItems.OfType<Bookmark>())
                item.PropertyChanged += OnBookmarkChanged;

        InvalidateVisual();
    }

    private void OnBookmarkChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only what is actually drawn. IsFlipped is not in the list: it used to
        // change a bracket's colour and now changes nothing here, since the row
        // says it in a word.
        //
        // IsSelected is, because the tick follows it. Index is too: deleting a
        // cut renumbers every one after it, and the number between the brackets
        // has to follow or the bar ends up labelled with the old order.
        if (e.PropertyName is nameof(Bookmark.StartSeconds)
                           or nameof(Bookmark.EndSeconds)
                           or nameof(Bookmark.IsIncomplete)
                           or nameof(Bookmark.IsSelected)
                           or nameof(Bookmark.Index))
            InvalidateVisual();
    }

    /// <summary>Thickness of a bracket's upright, in pixels.</summary>
    private const double StemWidth = 2;

    /// <summary>How far a bracket's feet reach into its own range.</summary>
    private const double FootLength = 5;

    /// <summary>Thickness of a foot.</summary>
    private const double FootWeight = 2;

    /// <summary>Point size of the number sitting between a pair of brackets.</summary>
    private const double LabelSize = 11;

    /// <summary>
    /// Clearance the number needs beyond its own width before it is drawn.
    /// </summary>
    /// <remarks>
    /// Without it a number can touch the brackets either side of it on a short
    /// cut, which reads worse than no number at all.
    /// </remarks>
    private const double LabelPadding = 3;

    private static readonly Typeface LabelTypeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    /// <summary>Width and height of the tick drawn on a checked cut.</summary>
    private const double CheckSize = 8;

    /// <summary>Gap between the tick and the number it precedes.</summary>
    private const double CheckGap = 3;

    /// <summary>
    /// The tick's stroke, built once. Drawn rather than set as a character.
    /// </summary>
    /// <remarks>
    /// Two strokes of a pen, not "✓" in a font: at eight pixels a glyph is at
    /// the mercy of hinting and of whether the face has the character at all,
    /// and this has to match brackets that are already drawn by hand. Round
    /// caps and join so the corner reads as a tick rather than as a chevron.
    /// </remarks>
    private static readonly Pen CheckPen = FrozenPen();

    private static Pen FrozenPen()
    {
        var pen = new Pen(MarkerBrush, 1.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var total = DurationSeconds;

        // Without a known duration there is nothing to scale against.
        if (Bookmarks == null || total <= 0 || width <= 0 || height <= 0) return;

        foreach (var b in Bookmarks.OfType<Bookmark>())
        {
            var startX = Math.Clamp(b.StartSeconds / total, 0, 1) * width;
            var open = b.IsIncomplete || b.EndSeconds <= b.StartSeconds;

            // Opened and not yet closed: the opening bracket on its own says so,
            // where a second one would claim an end it has not got.
            DrawBracket(dc, open ? IncompleteBrush : MarkerBrush, startX, width, height, opening: true);

            if (open) continue;

            var endX = Math.Clamp(b.EndSeconds / total, 0, 1) * width;
            DrawBracket(dc, MarkerBrush, endX, width, height, opening: false);
            DrawLabel(dc, b, startX, endX, height);
        }
    }

    /// <summary>
    /// Writes what is known about a cut between its brackets: a tick if it is
    /// checked, then its number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which bracket belongs to which row is otherwise guesswork once the cuts
    /// are close together, and the row is where everything else about a cut is
    /// written.
    /// </para>
    /// <para>
    /// Three outcomes by how much room there is, rather than one. A short cut
    /// that cannot hold both drops the number and keeps the tick: the number
    /// only says which row this is, while the tick says the next action is
    /// going to act on it, and of the two that is the one worth the pixels.
    /// Neither is shrunk or clipped to fit — an unreadable mark in a
    /// two-pixel space is noise, and the brackets still say where the cut is.
    /// </para>
    /// </remarks>
    private void DrawLabel(DrawingContext dc, Bookmark b, double startX, double endX, double height)
    {
        var label = new FormattedText(
            b.Index.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            LabelTypeface, LabelSize, MarkerBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // The brackets own the ends; only what is left between their feet is
        // free to write in.
        var free = endX - startX - (StemWidth + FootLength) * 2 - LabelPadding * 2;
        var middle = (startX + endX) / 2;

        if (!b.IsSelected)
        {
            if (label.Width > free) return;
            dc.DrawText(label, new Point(middle - label.Width / 2, (height - label.Height) / 2));
            return;
        }

        var both = CheckSize + CheckGap + label.Width;
        if (both <= free)
        {
            var left = middle - both / 2;
            DrawCheck(dc, left, height);
            dc.DrawText(label, new Point(left + CheckSize + CheckGap, (height - label.Height) / 2));
        }
        else if (CheckSize <= free)
        {
            DrawCheck(dc, middle - CheckSize / 2, height);
        }
    }

    /// <summary>
    /// Draws a tick whose left edge sits at <paramref name="x"/>, centred in
    /// the height available.
    /// </summary>
    private static void DrawCheck(DrawingContext dc, double x, double height)
    {
        var top = (height - CheckSize) / 2;

        // Down to the corner, then up the long stroke. The corner sits below
        // centre and the tail finishes above the start, which is what makes the
        // shape read as a tick at this size instead of as a check box.
        var start = new Point(x, top + CheckSize * 0.55);
        var corner = new Point(x + CheckSize * 0.36, top + CheckSize * 0.86);
        var end = new Point(x + CheckSize, top + CheckSize * 0.14);

        dc.DrawLine(CheckPen, start, corner);
        dc.DrawLine(CheckPen, corner, end);
    }

    /// <summary>
    /// Draws one end of a range as a bracket — an upright with a foot at the
    /// top and bottom, both reaching in towards the range it belongs to.
    /// </summary>
    /// <remarks>
    /// Two brackets and nothing between them. A band the length of the cut was
    /// a lot of paint for two numbers: it buried whatever shared the bar with
    /// it, and on a busy edit the timeline became a row of blocks with the
    /// gaps — the parts being thrown away — as the only thing left legible. A
    /// line joining each pair was the same idea at lower volume and had the
    /// same problem. Facing feet read as "from here to there" on their own.
    /// </remarks>
    private static void DrawBracket(DrawingContext dc, Brush brush, double x,
                                    double width, double height, bool opening)
    {
        // Keep the upright inside the control at either extreme, so a cut that
        // starts at zero or ends at the duration still shows a full bracket.
        var stemX = Math.Clamp(x - StemWidth / 2, 0, Math.Max(0, width - StemWidth));
        dc.DrawRectangle(brush, null, new Rect(stemX, 0, StemWidth, height));

        // Feet point into the range: right from an opening bracket, left from a
        // closing one, which is what makes a pair read as enclosing.
        var footX = opening ? stemX + StemWidth : stemX - FootLength;
        footX = Math.Clamp(footX, 0, Math.Max(0, width - FootLength));

        dc.DrawRectangle(brush, null, new Rect(footX, 0, FootLength, FootWeight));
        dc.DrawRectangle(brush, null, new Rect(footX, height - FootWeight, FootLength, FootWeight));
    }
}
