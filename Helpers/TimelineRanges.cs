using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;

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
    /// White, the default for <see cref="MarkBrush"/> — every mark, whatever
    /// the cut behind it is doing.
    /// </summary>
    /// <remarks>
    /// One colour rather than four by default. The marks used to be amber,
    /// teal, salmon and red by state, which turned the bar into a colour key
    /// the moment more than a couple of cuts existed. White reads at two
    /// pixels against every theme's track, which is why it is the default
    /// rather than simply a choice among others.
    ///
    /// The states still worth carrying here are carried by shape instead, which
    /// needs no key: a checked cut gets a tick, and an open bookmark gets its
    /// opening bracket and no closing one. Inversion is not among them — it
    /// changes what is written, not what will be written, and the row says it
    /// in a word.
    /// </remarks>
    private static readonly Brush DefaultMarkBrush = Frozen("#FFFFFF");

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

    public static readonly DependencyProperty BracketStyleProperty =
        DependencyProperty.Register(nameof(BracketStyle), typeof(SeekbarBracketStyle), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(SeekbarBracketStyle.Classic, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkBrushProperty =
        DependencyProperty.Register(nameof(MarkBrush), typeof(Brush), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(DefaultMarkBrush, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SnapPointsProperty =
        DependencyProperty.Register(nameof(SnapPoints), typeof(IEnumerable), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSnapPointsChanged));

    public static readonly DependencyProperty DragBookmarkProperty =
        DependencyProperty.Register(nameof(DragBookmark), typeof(Bookmark), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DragIsStartProperty =
        DependencyProperty.Register(nameof(DragIsStart), typeof(bool), typeof(TimelineRanges),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DragSecondsProperty =
        DependencyProperty.Register(nameof(DragSeconds), typeof(double), typeof(TimelineRanges),
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

    /// <summary>How a timestamp pair is marked. <see cref="SeekbarBracketStyle.None"/> draws nothing at all.</summary>
    public SeekbarBracketStyle BracketStyle
    {
        get => (SeekbarBracketStyle)GetValue(BracketStyleProperty);
        set => SetValue(BracketStyleProperty, value);
    }

    /// <summary>
    /// What a bracket, a cut's number and its tick are painted. Not the
    /// waiting-bookmark red — <see cref="IncompleteBrush"/> stays fixed
    /// regardless of this.
    /// </summary>
    public Brush MarkBrush
    {
        get => (Brush)GetValue(MarkBrushProperty);
        set => SetValue(MarkBrushProperty, value);
    }

    /// <summary>
    /// Times where a scan found the video changing, drawn as a tick along the
    /// foot of the bar so what a drag will snap to is visible before it does.
    /// </summary>
    public IEnumerable? SnapPoints
    {
        get => (IEnumerable?)GetValue(SnapPointsProperty);
        set => SetValue(SnapPointsProperty, value);
    }

    private static void OnSnapPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (TimelineRanges)d;

        // The collection is filled after it is bound — a scan takes a while —
        // so following the property alone would leave the ticks undrawn until
        // something else happened to invalidate the control.
        if (e.OldValue is INotifyCollectionChanged oldPoints)
            oldPoints.CollectionChanged -= self.OnSnapPointsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newPoints)
            newPoints.CollectionChanged += self.OnSnapPointsCollectionChanged;

        self.InvalidateVisual();
    }

    private void OnSnapPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    /// <summary>
    /// The bookmark one of whose brackets is mid-drag, or null the rest of
    /// the time. Set from code-behind, not bound — this is input state the
    /// View owns, not data the ViewModel holds.
    /// </summary>
    /// <remarks>
    /// Nothing on <see cref="Bookmark"/> itself changes while this is set:
    /// a drag is read straight off the pointer and drawn here, and only
    /// written back to the bookmark — once — when the drag ends. Doing
    /// otherwise would fire the clip preview's ffmpeg extraction and the
    /// undo-history recorder on every pixel the pointer crosses rather than
    /// once for the whole gesture.
    /// </remarks>
    public Bookmark? DragBookmark
    {
        get => (Bookmark?)GetValue(DragBookmarkProperty);
        set => SetValue(DragBookmarkProperty, value);
    }

    /// <summary>Which of <see cref="DragBookmark"/>'s two brackets is being dragged.</summary>
    public bool DragIsStart
    {
        get => (bool)GetValue(DragIsStartProperty);
        set => SetValue(DragIsStartProperty, value);
    }

    /// <summary>Where the dragged bracket is right now, in seconds.</summary>
    public double DragSeconds
    {
        get => (double)GetValue(DragSecondsProperty);
        set => SetValue(DragSecondsProperty, value);
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
    /// A bookmark still waiting for its closing timestamp always strokes with
    /// this, whatever <see cref="MarkBrush"/> is set to.
    /// </summary>
    private static readonly Pen IncompletePen = FrozenPen(IncompleteBrush, StemWidth);

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
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
        // Asked for nothing. The cut list is the only place a cut shows, and
        // that holds for the number and the tick as much as for the bracket
        // itself — a number with nothing bracketing it reads as a mistake
        // rather than as the style that was chosen.
        if (BracketStyle == SeekbarBracketStyle.None) return;

        var width = ActualWidth;
        var height = ActualHeight;
        var total = DurationSeconds;

        // Without a known duration there is nothing to scale against.
        if (Bookmarks == null || total <= 0 || width <= 0 || height <= 0) return;

        // Built once per pass rather than held as static fields: the brush
        // comes from a DynamicResource now, which can change under this
        // control, and a pen built from a brush that has since moved on
        // would keep painting the colour it had when it was first touched.
        var markBrush = MarkBrush;
        var markPen = FrozenPen(markBrush, StemWidth);
        var checkPen = FrozenPen(markBrush, 1.8);

        DrawSnapTicks(dc, markBrush, width, height, total);

        foreach (var b in Bookmarks.OfType<Bookmark>())
        {
            // The dragged end reads from the pointer, not the model — see
            // DragBookmark's remarks. The other end, and every other
            // bookmark, reads its own properties exactly as before.
            var dragging = ReferenceEquals(b, DragBookmark);
            var liveStart = dragging && DragIsStart ? DragSeconds : b.StartSeconds;
            var liveEnd = dragging && !DragIsStart ? DragSeconds : b.EndSeconds;

            var startX = Math.Clamp(liveStart / total, 0, 1) * width;
            var open = b.IsIncomplete || liveEnd <= liveStart;

            // Opened and not yet closed: the opening bracket on its own says so,
            // where a second one would claim an end it has not got. Nothing on
            // the other side of it to crowd, so its reach is never shortened.
            if (open)
            {
                DrawBracket(dc, IncompleteBrush, IncompletePen, startX, width, height, opening: true, cutWidth: double.PositiveInfinity);
                continue;
            }

            var endX = Math.Clamp(liveEnd / total, 0, 1) * width;

            // How much room the two brackets actually have to reach into
            // before they would start drawing over each other — see
            // DrawBracket's remarks on why this is passed down rather than
            // left as a fixed pixel count per style.
            var cutWidth = Math.Max(0, endX - startX);

            DrawBracket(dc, markBrush, markPen, startX, width, height, opening: true, cutWidth);
            DrawBracket(dc, markBrush, markPen, endX, width, height, opening: false, cutWidth);
            DrawLabel(dc, b, markBrush, checkPen, startX, endX, height);
        }
    }

    /// <summary>How tall a snap tick stands, as a fraction of the bar.</summary>
    private const double SnapTickFraction = 0.28;

    /// <summary>Width of a snap tick.</summary>
    private const double SnapTickWidth = 1;

    /// <summary>
    /// How solid a snap tick is drawn.
    /// </summary>
    /// <remarks>
    /// Faint on purpose. These are not marks the user made — they are what a
    /// machine thought it noticed — and a scan of a busy film can find
    /// hundreds. At full strength they would read as a second set of cuts and
    /// bury the brackets that matter.
    /// </remarks>
    private const double SnapTickOpacity = 0.4;

    /// <summary>
    /// Draws a short tick at the foot of the bar for every edit point.
    /// </summary>
    /// <remarks>
    /// Along the bottom, where the brackets' feet do not reach and the
    /// position wash is only a background — so they say where a bracket would
    /// land without competing with the brackets already there.
    /// </remarks>
    private void DrawSnapTicks(DrawingContext dc, Brush markBrush, double width, double height, double total)
    {
        if (SnapPoints is null) return;

        var tickHeight = height * SnapTickFraction;
        var top = height - tickHeight;

        dc.PushOpacity(SnapTickOpacity);
        try
        {
            foreach (var seconds in SnapPoints.OfType<double>())
            {
                if (seconds <= 0 || seconds >= total) continue;

                var x = Math.Clamp(seconds / total, 0, 1) * width;
                x = Math.Clamp(x - SnapTickWidth / 2, 0, Math.Max(0, width - SnapTickWidth));
                dc.DrawRectangle(markBrush, null, new Rect(x, top, SnapTickWidth, tickHeight));
            }
        }
        finally
        {
            dc.Pop();
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
    private void DrawLabel(DrawingContext dc, Bookmark b, Brush markBrush, Pen checkPen,
                           double startX, double endX, double height)
    {
        var label = new FormattedText(
            b.Index.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            LabelTypeface, LabelSize, markBrush,
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
            DrawCheck(dc, checkPen, left, height);
            dc.DrawText(label, new Point(left + CheckSize + CheckGap, (height - label.Height) / 2));
        }
        else if (CheckSize <= free)
        {
            DrawCheck(dc, checkPen, middle - CheckSize / 2, height);
        }
    }

    /// <summary>
    /// Draws a tick whose left edge sits at <paramref name="x"/>, centred in
    /// the height available.
    /// </summary>
    private static void DrawCheck(DrawingContext dc, Pen checkPen, double x, double height)
    {
        var top = (height - CheckSize) / 2;

        // Down to the corner, then up the long stroke. The corner sits below
        // centre and the tail finishes above the start, which is what makes the
        // shape read as a tick at this size instead of as a check box.
        var start = new Point(x, top + CheckSize * 0.55);
        var corner = new Point(x + CheckSize * 0.36, top + CheckSize * 0.86);
        var end = new Point(x + CheckSize, top + CheckSize * 0.14);

        dc.DrawLine(checkPen, start, corner);
        dc.DrawLine(checkPen, corner, end);
    }

    /// <summary>
    /// Draws one end of a range, in whichever of the four styles is chosen.
    /// </summary>
    /// <param name="cutWidth">
    /// How many pixels wide the cut itself is — the room the two brackets
    /// have to reach into before they would start drawing on top of each
    /// other. Infinite for the lone bracket of an open bookmark, which has
    /// no other end to crowd.
    /// </param>
    /// <remarks>
    /// Whatever the style, it is drawn at the mark and nothing between a pair
    /// of them: a band the length of the cut was a lot of paint for two
    /// numbers, burying whatever shared the bar with it, and on a busy edit
    /// the timeline became a row of blocks with the gaps — the parts being
    /// thrown away — as the only thing left legible. A line joining each pair
    /// was the same idea at lower volume and had the same problem.
    ///
    /// On a cut too narrow for a style's usual reach, that reach is shortened
    /// to half of what is actually there instead of being drawn at full size
    /// regardless — the two ends then meet in the cut's own middle rather
    /// than overshoot each other and read as one tangled mark instead of two.
    /// </remarks>
    private void DrawBracket(DrawingContext dc, Brush brush, Pen pen, double x,
                             double width, double height, bool opening, double cutWidth)
    {
        switch (BracketStyle)
        {
            case SeekbarBracketStyle.Lines:
                DrawStem(dc, brush, x, width, height);
                break;

            case SeekbarBracketStyle.Chevrons:
                DrawChevron(dc, pen, x, width, height, opening, cutWidth);
                break;

            case SeekbarBracketStyle.Classic:
            default:
                var stemX = DrawStem(dc, brush, x, width, height);
                DrawFeet(dc, brush, stemX, width, height, opening, cutWidth);
                break;
        }
    }

    /// <summary>Draws the upright and returns its clamped left edge, for a foot to line up against.</summary>
    private static double DrawStem(DrawingContext dc, Brush brush, double x, double width, double height)
    {
        // Keep the upright inside the control at either extreme, so a cut that
        // starts at zero or ends at the duration still shows a full mark.
        var stemX = Math.Clamp(x - StemWidth / 2, 0, Math.Max(0, width - StemWidth));
        dc.DrawRectangle(brush, null, new Rect(stemX, 0, StemWidth, height));
        return stemX;
    }

    /// <summary>
    /// Feet point into the range: right from an opening bracket, left from a
    /// closing one, which is what makes a pair read as enclosing.
    /// </summary>
    private static void DrawFeet(DrawingContext dc, Brush brush, double stemX,
                                 double width, double height, bool opening, double cutWidth)
    {
        // Half of what the cut actually has, once that is less than a full
        // foot — see DrawBracket's remarks.
        var footLength = Math.Min(FootLength, cutWidth / 2);
        if (footLength <= 0) return;

        var footX = opening ? stemX + StemWidth : stemX - footLength;
        footX = Math.Clamp(footX, 0, Math.Max(0, width - footLength));

        dc.DrawRectangle(brush, null, new Rect(footX, 0, footLength, FootWeight));
        dc.DrawRectangle(brush, null, new Rect(footX, height - FootWeight, footLength, FootWeight));
    }

    /// <summary>How far a chevron's arms reach from its vertex.</summary>
    private const double ChevronReach = 6;

    /// <summary>
    /// An angled mark whose vertex sits exactly at the mark's time — the same
    /// thing the stem's upright does for the other two styles — with its two
    /// arms opening into the range: &lt; for an opening mark, &gt; for a
    /// closing one.
    /// </summary>
    private static void DrawChevron(DrawingContext dc, Pen pen, double x,
                                    double width, double height, bool opening, double cutWidth)
    {
        // Half of what the cut actually has, once that is less than a full
        // arm — see DrawBracket's remarks. Below that a chevron's two arms
        // would reach past the opposite mark rather than stopping at it.
        var reach = Math.Min(ChevronReach, cutWidth / 2);

        var vertexX = Math.Clamp(x, 0, width);
        var armX = Math.Clamp(opening ? vertexX + reach : vertexX - reach, 0, width);
        var mid = height / 2;

        dc.DrawLine(pen, new Point(vertexX, mid), new Point(armX, 0));
        dc.DrawLine(pen, new Point(vertexX, mid), new Point(armX, height));
    }
}
