using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Draws each bookmark onto the timeline as a range bar spanning its start to
/// its end, so the shape of the edit is visible at a glance.
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
    /// <summary>Complete cuts. Amber, matching the duration text in the list.</summary>
    private static readonly Brush RangeBrush = Frozen("#DCDCAA");

    /// <summary>Checked cuts — the ones an action would act on.</summary>
    private static readonly Brush SelectedBrush = Frozen("#4EC9B0");

    /// <summary>Cuts marked for inversion, matching the [F] prefix colour.</summary>
    private static readonly Brush FlippedBrush = Frozen("#CE9178");

    /// <summary>A lone opening timestamp: no range yet, so a thin tick.</summary>
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
        if (e.PropertyName is nameof(Bookmark.StartSeconds)
                           or nameof(Bookmark.EndSeconds)
                           or nameof(Bookmark.IsIncomplete)
                           or nameof(Bookmark.IsSelected)
                           or nameof(Bookmark.IsFlipped))
            InvalidateVisual();
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

            if (b.IsIncomplete || b.EndSeconds <= b.StartSeconds)
            {
                // No range to draw yet — mark where it was opened.
                dc.DrawRectangle(IncompleteBrush, null,
                    new Rect(Math.Min(startX, width - 2), 0, 2, height));
                continue;
            }

            var endX = Math.Clamp(b.EndSeconds / total, 0, 1) * width;

            // A very short cut would otherwise round away to nothing.
            var barWidth = Math.Max(2, endX - startX);
            if (startX + barWidth > width) startX = Math.Max(0, width - barWidth);

            var brush = b.IsFlipped ? FlippedBrush
                      : b.IsSelected ? SelectedBrush
                      : RangeBrush;

            dc.DrawRoundedRectangle(brush, null,
                new Rect(startX, 0, barWidth, height), 1, 1);
        }
    }
}
