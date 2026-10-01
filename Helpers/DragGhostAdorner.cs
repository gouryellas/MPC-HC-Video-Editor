using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// A copy of the thing being dragged, carried under the pointer.
/// </summary>
/// <remarks>
/// Drawn with a <see cref="VisualBrush"/> of the element itself rather than a
/// rebuilt facsimile, so a button being dragged looks like that button —
/// including its colour, which the four operation buttons have and nothing else
/// would reproduce.
///
/// An adorner rather than a Popup: it lives in the window's own adorner layer,
/// so it is clipped to the window, moves with it, and cannot be mistaken by the
/// hit test for something droppable. It sets
/// <see cref="UIElement.IsHitTestVisible"/> false for that last reason — a ghost
/// that answered the hit test would sit between the pointer and every drop
/// target, which is to say it would be the only thing the pointer ever found.
/// </remarks>
public sealed class DragGhostAdorner : Adorner
{
    private readonly Rect _bounds;
    private readonly Brush _face;
    private Point _at;

    /// <param name="layerHost">The element whose adorner layer carries the ghost.</param>
    /// <param name="source">The element being dragged, copied for the picture.</param>
    public DragGhostAdorner(UIElement layerHost, FrameworkElement source) : base(layerHost)
    {
        IsHitTestVisible = false;
        Opacity = 0.75;

        _bounds = new Rect(0, 0, source.ActualWidth, source.ActualHeight);
        _face = new VisualBrush(source) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
    }

    /// <summary>Moves the ghost so its middle sits under the pointer.</summary>
    public void MoveTo(Point at)
    {
        _at = new Point(at.X - _bounds.Width / 2, at.Y - _bounds.Height / 2);

        // The adorner layer redraws from the render pass, so nudging the
        // position has to ask for one.
        (Parent as AdornerLayer)?.Update(AdornedElement);
        InvalidateVisual();
    }

    /// <summary>
    /// The outline drawn round the ghost, so a translucent copy over a busy
    /// panel still reads as a separate thing being carried.
    /// </summary>
    /// <remarks>
    /// Built once, and built from a brush of its own. The first version asked
    /// for <c>Brushes.White</c> and then set its opacity — and the shared
    /// brushes are frozen, so that threw from inside a render pass, which takes
    /// the whole application down rather than failing the drag. It did so on
    /// any press that moved a pixel, which is to say on an ordinary click.
    /// </remarks>
    private static readonly Pen Edge = BuildEdge();

    private static Pen BuildEdge()
    {
        var brush = new SolidColorBrush(Colors.White) { Opacity = 0.35 };
        brush.Freeze();

        var pen = new Pen(brush, 1);
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(_at, _bounds.Size);

        dc.DrawRectangle(_face, null, rect);
        dc.DrawRectangle(null, Edge, rect);
    }
}
