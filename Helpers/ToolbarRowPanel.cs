using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Lays out one row of the action toolbar: buttons at their natural width,
/// expanders taking whatever is left over, and a wrap to another line when the
/// window is too narrow to hold the row at all.
/// </summary>
/// <remarks>
/// <para>
/// None of the stock panels does this. A <see cref="WrapPanel"/> wraps but
/// cannot stretch an item to fill the leftover space, so an expander would be a
/// fixed-width gap that happens to be called something else. A
/// <see cref="Grid"/> with a star column stretches but never wraps, so a window
/// narrower than the row would cut the end off it — and the window's minimum
/// width is the menu bar's now, which is narrower than a full toolbar, so that
/// is not a hypothetical.
/// </para>
/// <para>
/// The two behaviours are not in conflict, because they apply at different
/// times: leftover width is only leftover if there is any, and a row that has
/// overflowed has none to share. So a line that fits divides its slack between
/// its expanders, and a line that does not fit wraps and gives its expanders
/// nothing.
/// </para>
/// <para>
/// Row breaks are not handled here. They split the collection into separate
/// panels one level up, so each of these only ever lays out one row — it is
/// the difference between a break the user asked for and a wrap forced by the
/// window, which is why the two are not the same mechanism.
/// </para>
/// </remarks>
public sealed class ToolbarRowPanel : Panel
{
    /// <summary>How wide an expander is before it is given anything.</summary>
    /// <remarks>
    /// Not zero, so a row whose expanders have nothing to share still shows
    /// them as the gaps they are, and so the arrow glyphs the Customize dialog
    /// draws over them have something to sit on.
    /// </remarks>
    private const double ExpanderMinimum = 8;

    protected override Size MeasureOverride(Size available)
    {
        double lineWidth = 0, lineHeight = 0, widest = 0, total = 0;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = child.DesiredSize;
            var width = IsExpander(child) ? ExpanderMinimum : size.Width;

            // Start another line when this one cannot take it. The first item
            // on a line is placed whatever its width, since moving it down
            // would not make it fit either.
            if (lineWidth > 0 && lineWidth + width > available.Width)
            {
                widest = Math.Max(widest, lineWidth);
                total += lineHeight;
                lineWidth = 0;
                lineHeight = 0;
            }

            lineWidth += width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        widest = Math.Max(widest, lineWidth);
        total += lineHeight;

        return new Size(double.IsInfinity(available.Width) ? widest : Math.Min(widest, available.Width),
                        total);
    }

    protected override Size ArrangeOverride(Size final)
    {
        // Group into lines first, so the leftover width on each is known before
        // anything is placed — an expander's width is whatever the rest of its
        // own line did not use.
        var lines = new List<List<UIElement>>();
        var line = new List<UIElement>();
        double lineWidth = 0;

        foreach (UIElement child in InternalChildren)
        {
            var width = IsExpander(child) ? ExpanderMinimum : child.DesiredSize.Width;

            if (line.Count > 0 && lineWidth + width > final.Width)
            {
                lines.Add(line);
                line = new List<UIElement>();
                lineWidth = 0;
            }

            line.Add(child);
            lineWidth += width;
        }

        if (line.Count > 0) lines.Add(line);

        double y = 0;

        foreach (var row in lines)
        {
            double fixedWidth = 0, height = 0;
            var expanders = 0;

            foreach (var child in row)
            {
                if (IsExpander(child)) expanders++;
                else fixedWidth += child.DesiredSize.Width;

                height = Math.Max(height, child.DesiredSize.Height);
            }

            // Shared equally. Two expanders in a row is how something is
            // centred, and that only works if they come out the same size.
            var slack = Math.Max(0, final.Width - fixedWidth);
            var each = expanders > 0 ? Math.Max(ExpanderMinimum, slack / expanders) : 0;

            double x = 0;

            foreach (var child in row)
            {
                var width = IsExpander(child) ? each : child.DesiredSize.Width;
                child.Arrange(new Rect(x, y, width, height));
                x += width;
            }

            y += height;
        }

        return new Size(final.Width, y);
    }

    /// <summary>
    /// True for the elements standing in for an expander — asked of the item
    /// behind the element rather than of the element itself, which is a
    /// generated container that knows nothing about toolbars.
    /// </summary>
    private static bool IsExpander(UIElement child) =>
        child is FrameworkElement { DataContext: ToolbarItem { Kind: ToolbarItemKind.Expander } };
}
