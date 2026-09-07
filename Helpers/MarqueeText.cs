using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// A single-line text display that scrolls its content into view when it is
/// wider than the space it has been given, and sits still like a plain
/// <see cref="TextBlock"/> when it is not.
/// </summary>
/// <remarks>
/// The status bar reports what an operation produced, and those messages carry
/// the output file name — "Created &lt;name&gt; — The finished file is 1.1s
/// longer than the marked range (0:04 against 0:03)." A long name pushed the
/// explanation off the end of the bar, where a TextBlock simply clips it, so
/// the part that mattered was the part the user never saw.
///
/// The travel is a hold at the start, a constant-speed run to the end, a hold
/// there, then the same run back — rather than a wrapping ticker. Both ends of
/// the message come to rest long enough to read, and the text is never split
/// across the seam of a loop.
/// </remarks>
public class MarqueeText : Decorator
{
    /// <summary>Scroll rate, in device-independent pixels per second.</summary>
    /// <remarks>
    /// Roughly reading pace. Slower is easier to follow but leaves the tail of
    /// a long message half a minute away; this is the compromise, and it is the
    /// one number to change if the scroll feels wrong.
    /// </remarks>
    private const double PixelsPerSecond = 60;

    /// <summary>How long the text rests at each end before moving on.</summary>
    private static readonly TimeSpan EndPause = TimeSpan.FromSeconds(2);

    /// <summary>Overflow below this is left alone: a pixel or two of clipping is not worth animating.</summary>
    private const double OverflowThreshold = 2;

    private readonly TextBlock _text = new()
    {
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.None,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TranslateTransform _shift = new();

    /// <summary>Width the text wants, measured unconstrained.</summary>
    private double _naturalWidth;

    /// <summary>Overflow the running animation was built for; NaN when nothing is running.</summary>
    private double _animatedOverflow = double.NaN;

    public MarqueeText()
    {
        // Everything outside the arranged width is the point of the exercise:
        // without this the child would paint across its neighbours in the
        // status bar instead of disappearing behind the edge.
        ClipToBounds = true;

        _text.RenderTransform = _shift;
        Child = _text;
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(MarqueeText),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // A Decorator has no text properties of its own, so without these the
    // familiar TextBlock attributes would not compile against this element.
    // Each is inheritable, so the value set here reaches the child unaided —
    // these only put the names back within reach of XAML.

    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(typeof(MarqueeText));

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly DependencyProperty FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner(typeof(MarqueeText));

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly DependencyProperty FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner(typeof(MarqueeText));

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public static readonly DependencyProperty FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner(typeof(MarqueeText));

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (MarqueeText)d;
        self._text.Text = e.NewValue as string ?? string.Empty;

        // A new message starts its own run from the left, however far through
        // the previous one the animation happened to be.
        self.StopMarquee();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        // Unconstrained width: what the text needs is what decides whether it
        // has to scroll, so it must not be measured against the space available.
        _text.Measure(new Size(double.PositiveInfinity, constraint.Height));
        _naturalWidth = _text.DesiredSize.Width;

        var width = double.IsInfinity(constraint.Width)
            ? _naturalWidth
            : Math.Min(_naturalWidth, constraint.Width);

        return new Size(width, _text.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        _text.Arrange(new Rect(0, 0, _naturalWidth, arrangeSize.Height));
        UpdateMarquee(arrangeSize.Width);
        return arrangeSize;
    }

    /// <summary>
    /// Starts, restarts or stops the scroll to match the space now available.
    /// </summary>
    private void UpdateMarquee(double viewportWidth)
    {
        var overflow = _naturalWidth - viewportWidth;

        if (overflow <= OverflowThreshold)
        {
            StopMarquee();
            return;
        }

        // Arrange runs far more often than the text or the width actually
        // change; rebuilding the animation each time would keep resetting the
        // scroll to the left and it would never get anywhere.
        if (Math.Abs(overflow - _animatedOverflow) < 0.5) return;

        _animatedOverflow = overflow;

        var travel = TimeSpan.FromSeconds(overflow / PixelsPerSecond);
        var frames = new DoubleAnimationUsingKeyFrames();

        var at = TimeSpan.Zero;
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(at)));
        at += EndPause;
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(at)));
        at += travel;
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(at)));
        at += EndPause;
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(at)));
        at += travel;
        frames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(at)));

        frames.Duration = at;
        frames.RepeatBehavior = RepeatBehavior.Forever;

        _shift.BeginAnimation(TranslateTransform.XProperty, frames);
    }

    /// <summary>Removes any running animation and puts the text back at the left.</summary>
    private void StopMarquee()
    {
        if (double.IsNaN(_animatedOverflow)) return;

        _shift.BeginAnimation(TranslateTransform.XProperty, null);
        _shift.X = 0;
        _animatedOverflow = double.NaN;
    }
}
